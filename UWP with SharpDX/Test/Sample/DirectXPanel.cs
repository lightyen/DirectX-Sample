using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using DirectXToolkit;
using QRCoder;

namespace MyGame {
    public class DirectXPanel {

        private SwapChainPanel TargetSwapChainPanel;

        private SwapChain SwapChain;

        public Size2 SwapChainSize {
            get; private set;
        }

        private Size2F ActualSize;
        private Size2F NewWindowSize;
        private Size2F Dpi;

        private Adapter CurrentAdapter;
        public DeviceInfo DeviceInfo {
            get; private set;
        }

        private SemaphoreSlim ExitSem;

        private SharpDX.Direct3D11.Device D3D11Device;
        private DeviceContext MainContext;
        private SharpDX.Direct2D1.DeviceContext D2DDeviceContext;
        private Texture2D offScreenSurface;
        private DepthStencilView depthView;
        private RenderTargetView renderTargetView;

        private VertexShader vertexShader;
        private PixelShader pixelShader;

        Stopwatch fpsTimer = new Stopwatch();
        int fpsCounter = 0;
        string fpsString = "0";

        private SharpDX.DirectWrite.TextFormat infoTextFormat;
        private SharpDX.DirectWrite.TextFormat textFormat;
        private SharpDX.Direct2D1.SolidColorBrush textBrush;

        private Task<(SharpDX.Direct3D11.Resource, ShaderResourceView)> CreateTextureTask;

        enum RunMode {
            Immediate = 0,
            Deferred = 1,
        }

        RunMode Mode = RunMode.Deferred;

        bool TearingSupport = false; // 支援關閉垂直同步
        public bool Running { get; private set; }

        InputLayout VertexLayout;

        public DirectXPanel() {
            FindAdapter();
            CreateDevice();
            if (CurrentAdapter == null) {
                throw new SharpDXException(Result.Fail, "找不到適合的Adapter");
            }
        }

        public void CreateSwapChain(Size2 swapChainSize, SwapChainPanel panel) {
            if (swapChainSize.Width * swapChainSize.Height <= 0) {
                throw new SharpDXException(Result.Fail, "DirectXPanel初始化Size有誤");
            }
            CreateSwapChain(swapChainSize);
            SetSwapChainTarget(panel);
            TargetSwapChainPanel = panel;
        }

        private void SetSwapChainTarget(SwapChainPanel panel) {
            // 把Xaml的SwapChainPanel與DirectX的SwapChain連結起來
            using (ISwapChainPanelNative swapChainPanelNative = ComObject.As<ISwapChainPanelNative>(panel)) {
                swapChainPanelNative.SwapChain = SwapChain;
            }
        }

        private void FindAdapter() {
            using (var factory = new Factory1())
            using (var factory5 = factory.QueryInterface<Factory5>()) {

                // 測試看是否支援關閉垂直同步
                int allowTearing = 0;
                IntPtr data = System.Runtime.InteropServices.Marshal.AllocHGlobal(4);
                factory5.CheckFeatureSupport(SharpDX.DXGI.Feature.PresentAllowTearing, data, sizeof(int));
                allowTearing = System.Runtime.InteropServices.Marshal.PtrToStructure<int>(data);
                System.Runtime.InteropServices.Marshal.FreeHGlobal(data);
                if (allowTearing == 1) TearingSupport = true;

                // 找一個繪圖介面
                var list = factory.Adapters.ToList();
                var query = from adp in list
                            where adp.Description.VendorId == 0x10DE || adp.Description.VendorId == 0x1022 || adp.Description.VendorId == 0x8086
                            select adp;
                query.OrderBy((x) => {
                    if (x.Description.VendorId == 0x10DE) return 0;
                    if (x.Description.VendorId == 0x1022) return 1;
                    if (x.Description.VendorId == 0x8086) return 2;
                    return 3;
                });
                CurrentAdapter = query.ElementAtOrDefault(0);
            }
        }

        private void CreateDevice() {
            FeatureLevel[] featureLevels = new FeatureLevel[] {
                FeatureLevel.Level_11_1,
                FeatureLevel.Level_11_0,
            };
            DeviceCreationFlags flags = DeviceCreationFlags.BgraSupport;
#if DEBUG
            flags |= DeviceCreationFlags.Debug;
#endif
            D3D11Device = new SharpDX.Direct3D11.Device(CurrentAdapter, flags, featureLevels);

            var desc = CurrentAdapter.Description;
            DeviceInfo = new DeviceInfo {
                Description = desc.Description,
                VendorId = desc.VendorId,
                DeviceId = desc.DeviceId,
                DedicatedVideoMemory = desc.DedicatedVideoMemory,
                FeatureLevel = D3D11Device.FeatureLevel
            };
        }

        private void CreateSwapChain(Size2 size) {
            // https://docs.microsoft.com/en-us/windows/uwp/gaming/multisampling--multi-sample-anti-aliasing--in-windows-store-apps
            SwapChainDescription1 swapChainDescription = new SwapChainDescription1() {
                Usage = Usage.RenderTargetOutput | Usage.BackBuffer,
                BufferCount = 2,
                SwapEffect = SwapEffect.FlipSequential, // UWP 只能用 flip model
                SampleDescription = new SampleDescription(1, 0), // 在flip model下SwapChain不能開multi-sampling，只能另闢蹊徑
                Scaling = Scaling.Stretch,
                Flags = SwapChainFlags.AllowModeSwitch | SwapChainFlags.AllowTearing,
                Format = Format.B8G8R8A8_UNorm,
                Width = size.Width,
                Height = size.Height,
            };

            Dictionary<int, int> count_quality_levels = new Dictionary<int, int>();

            for (int i = 1; i <= SharpDX.Direct3D11.Device.MultisampleCountMaximum; i++) {
                var quality = D3D11Device.CheckMultisampleQualityLevels(Format.B8G8R8A8_UNorm, i);
                count_quality_levels.Add(i, quality);
            }

            D3D11Device.CheckThreadingSupport(out bool supportConcurentResources, out bool supportCommandList);

            // 建立SwapChain
            using (SharpDX.DXGI.Device3 dxgiDevice3 = D3D11Device.QueryInterface<SharpDX.DXGI.Device3>()) {
                using (Factory2 dxgiFactory2 = dxgiDevice3.Adapter.GetParent<Factory2>()) {
                    SwapChain = new SwapChain1(dxgiFactory2, D3D11Device, ref swapChainDescription);
                    SwapChainSize = size;
                }
            }
        }

        private void CreateRenderTargetView(DeviceContext context) {

            offScreenSurface = new Texture2D(D3D11Device, new Texture2DDescription {
                Format = Format.B8G8R8A8_UNorm,
                Width = 1920,
                Height = 1080,
                BindFlags = BindFlags.RenderTarget,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = new SampleDescription(4, 0),
            });

            var depthBuffer = new Texture2D(D3D11Device, new Texture2DDescription {
                Format = Format.D32_Float_S8X24_UInt,
                ArraySize = 1,
                MipLevels = 1,
                Width = 1920,
                Height = 1080,
                SampleDescription = new SampleDescription(4, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.DepthStencil,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None,
            });

            depthView = new DepthStencilView(D3D11Device, depthBuffer);

            renderTargetView = new RenderTargetView(D3D11Device, offScreenSurface, new RenderTargetViewDescription {
                Format = Format.B8G8R8A8_UNorm,
                Dimension = RenderTargetViewDimension.Texture2DMultisampled,
            });

            using (var backbuffer = SwapChain.GetBackBuffer<Texture2D>(0))
            using (var Surface = backbuffer.QueryInterface<Surface2>())
            using (var dxgiDevice = D3D11Device.QueryInterface<SharpDX.DXGI.Device3>()) {
                SharpDX.Direct2D1.DebugLevel debugLevel = SharpDX.Direct2D1.DebugLevel.None;
#if DEBUG
                debugLevel |= SharpDX.Direct2D1.DebugLevel.Information;
#endif
                var factory = new SharpDX.Direct2D1.Factory2(SharpDX.Direct2D1.FactoryType.SingleThreaded, debugLevel);

                var d2ddevice = new SharpDX.Direct2D1.Device(factory, dxgiDevice);
                D2DDeviceContext = new SharpDX.Direct2D1.DeviceContext(d2ddevice, SharpDX.Direct2D1.DeviceContextOptions.None);

                Dpi = factory.DesktopDpi;

                using (var bitmap = new SharpDX.Direct2D1.Bitmap(D2DDeviceContext, Surface, new SharpDX.Direct2D1.BitmapProperties {
                    DpiX = 96.0f,
                    DpiY = 96.0f,
                    PixelFormat = new SharpDX.Direct2D1.PixelFormat(Format.Unknown, SharpDX.Direct2D1.AlphaMode.Premultiplied),
                })) {
                    // 將 D2DDeviceContext 的 Target 指向SwapChain 的 Surface
                    D2DDeviceContext.Target = bitmap;
                    D2DDeviceContext.UnitMode = SharpDX.Direct2D1.UnitMode.Pixels;
                }
            }

            PrepareD2D();

            using (var BackBuffer = SwapChain.GetBackBuffer<Texture2D>(0)) {
                renderTargetView = new RenderTargetView(D3D11Device, BackBuffer);
            }

            context.OutputMerger.SetTargets(depthView, renderTargetView);
        }

        private async Task LoadShader() {

            var VertexShaderByteCode = await LoadShaderCodeFromFile(new Uri("ms-appx:///Shader/VertexShader.cso"));
            var PixelShaderByteCode = await LoadShaderCodeFromFile(new Uri("ms-appx:///Shader/PixelShader.cso"));
            vertexShader = new VertexShader(D3D11Device, VertexShaderByteCode);
            pixelShader = new PixelShader(D3D11Device, PixelShaderByteCode);

            InputElement[] layout = new InputElement[] {
                new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 0),
                new InputElement("COLOR", 0, Format.R32G32B32A32_Float, 16, 0),
                new InputElement("TEXCOORD", 0, Format.R32G32_Float, 32, 0),
            };
            VertexLayout = new InputLayout(D3D11Device, VertexShaderByteCode, layout);
        }

        public void SetView(float width, float height) {
            NewWindowSize = new Size2F((float)(Math.Floor(width)), (float)(Math.Floor(height)));
        }

        public void UpdateQRCode(string message) {

            if (CreateTextureTask == null && D3D11Device != null) {
                (SharpDX.Direct3D11.Resource, ShaderResourceView) func(string msg, SharpDX.Direct3D11.Device device) {
                    ShaderResourceView textureView = null;
                    SharpDX.Direct3D11.Resource texture = null;
                    using (var qrGenerator = new QRCodeGenerator()) {
                        var data = qrGenerator.CreateQrCode(msg, QRCodeGenerator.ECCLevel.Q);
                        PngByteQRCode qrCode = new PngByteQRCode(data);
                        var dataBytes = qrCode.GetGraphic(40);
                        using (var mmStream = new MemoryStream(dataBytes)) {

                            DirectXTK.CreateTexture(device, mmStream, out texture, out textureView);
                        }
                        return (texture, textureView);
                    }
                }
                CreateTextureTask = Task.Run(() => { return func(message, D3D11Device); });
            }
        }
       
        private void SetViewport(DeviceContext context) {
            if (ActualSize != NewWindowSize) {
                if (context != null) {
                    ActualSize = NewWindowSize;
                    context.Rasterizer.SetViewport(0, 0, NewWindowSize.Width, NewWindowSize.Height);
                }
            }
        }

        public void UpdateFile(StorageFile file) {
            if (CreateTextureTask == null && D3D11Device != null) {
                (SharpDX.Direct3D11.Resource, ShaderResourceView) func(StorageFile f, SharpDX.Direct3D11.Device device) {
                    DirectXTK.CreateTexture(device, f, out var texture, out var textureView);
                    return (texture, textureView);
                }
                
                CreateTextureTask = Task.Run(() => { return func(file, D3D11Device); });
            }    
        }

        public Result SaveFile(StorageFile file) {
            Result hr = Result.Fail;
            if (target != null) {
                var task = file.OpenStreamForWriteAsync();
                using (var stream = task.Result) {
                    switch(file.FileType) {
                        case ".jpg":
                        case ".jpeg":
                        case ".jfif":
                        case ".jpe":
                            hr = DirectXTK.SaveTextureToStream(D3D11Device, target, stream, SharpDX.WIC.ContainerFormatGuids.Jpeg, Guid.Empty);
                            break;
                        default:
                            hr = DirectXTK.SaveTextureToStream(D3D11Device, target, stream, SharpDX.WIC.ContainerFormatGuids.Png, Guid.Empty);
                            break;
                    }
                    
                }
                if (!hr.Success) {
                    file.DeleteAsync().AsTask().Wait();
                }
            }
            return hr;
        }

        SharpDX.Direct3D11.Resource target;

        private void Update() {
            // 更新資料
            fpsCounter++;
            if (fpsTimer.ElapsedMilliseconds > 1000) {
                var fps = 1000.0 * fpsCounter / fpsTimer.ElapsedMilliseconds;
                fpsString = $"{fps:F1}";
                fpsCounter = 0;
                fpsTimer.Reset();
                fpsTimer.Start();
            }

            if (CreateTextureTask != null && CreateTextureTask.IsCompletedSuccessfully) {
                var result = CreateTextureTask.Result;
                CreateTextureTask = null;
                if (result.Item1 is SharpDX.Direct3D11.Resource texture && result.Item2 is ShaderResourceView textureView) {
                    target = result.Item1;
                    MainContext?.PixelShader.SetShaderResource(0, textureView);
                }
            }

            SetViewport(MainContext);
        }

        private void Render() {
            if (MainContext != null) {

                // 把renderTargetView綁定到Output-Merger Stage
                MainContext.OutputMerger.SetRenderTargets(renderTargetView);

                // 初始化模板暫存區
                MainContext.ClearDepthStencilView(depthView, DepthStencilClearFlags.Depth, 1.0f, 0);
                // https://msdn.microsoft.com/en-us/library/windows/desktop/bb205120(v=vs.85).aspx

                // 填滿背景色
                MainContext.ClearRenderTargetView(renderTargetView, Color.Black);

                // 畫
                MainContext.DrawIndexed(3 * 2, 0, 0);

                if (Mode == RunMode.Deferred) {
                    // TRUE means restore the state of the device context to its pre-command list condition when recording is completed.
                    var commandList = MainContext.FinishCommandList(true);
                    D3D11Device.ImmediateContext.ExecuteCommandList(commandList, false);
                    commandList.Dispose();
                    commandList = null;
                }

                // https://docs.microsoft.com/en-us/windows/uwp/gaming/multisampling--multi-sample-anti-aliasing--in-windows-store-apps
                //using (var BackBuffer = SwapChain.GetBackBuffer<Texture2D>(0)) {
                //    var index = SharpDX.Direct3D11.Resource.CalculateSubResourceIndex(0, 0, 1);
                //    MainContext.ResolveSubresource(offScreenSurface, index, BackBuffer, index, Format.B8G8R8A8_UNorm);
                //}

                D2DDraw();

                // 把畫好的結果輸出到螢幕上！
                // syncInterval = 0 表示不同步
                SwapChain.Present(0, TearingSupport ? PresentFlags.AllowTearing : PresentFlags.None);
            }
        }

        private void PrepareD2D() {
            var writeFactory = new SharpDX.DirectWrite.Factory();
            textFormat = new SharpDX.DirectWrite.TextFormat(writeFactory, "微軟正黑體", 30.0f);
            infoTextFormat = new SharpDX.DirectWrite.TextFormat(writeFactory, "微軟正黑體", 14.0f);
            textBrush = new SharpDX.Direct2D1.SolidColorBrush(D2DDeviceContext, Color.LightGreen);
        }

        private void D2DDraw() {
            D2DDeviceContext.BeginDraw();
            D2DDeviceContext.DrawText(fpsString, textFormat, new RectangleF(0, ActualSize.Height - 32, 100, 32), textBrush);
            D2DDeviceContext.DrawText(DeviceInfo.ToString(), infoTextFormat, new RectangleF(0, 0, 250, 100), textBrush);
            D2DDeviceContext.EndDraw();
        }

        private async Task LoadResource() {
            await LoadShader();
        }

        void PreparePipeline(DeviceContext context) {

            context.InputAssembler.InputLayout = VertexLayout;
            context.VertexShader.Set(vertexShader);
            context.PixelShader.Set(pixelShader);

            float width = 1920.0f;
            float height = 1080.0f;

            var vertices = new SimpleVertex[] {
                new SimpleVertex { Position = new Vector4(0.0f, height * 0.6f, 1.0f, 1.0f), Color = new Vector4(1.0f, 0.0f, 0.0f, 1.0f), TexCoord = new Vector2(0.0f, 0.0f)},

                new SimpleVertex { Position = new Vector4(0.0f, 0.0f, 1.0f, 1.0f), Color = new Vector4(0.0f, 0.0f, 1.0f, 1.0f), TexCoord = new Vector2(0.0f, 1.0f)},

                new SimpleVertex { Position = new Vector4(width * 0.6f, 0.0f, 1.0f, 1.0f), Color = new Vector4(0.0f, 1.0f, 0.0f, 1.0f), TexCoord = new Vector2(1.0f, 1.0f)},

                new SimpleVertex { Position = new Vector4(width * 0.6f, height * 0.6f, 1.0f, 1.0f), Color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f), TexCoord = new Vector2(1.0f, 0.0f)},
            };

            // CreateBuffer 在記憶體
            var vertexBuffer = SharpDX.Direct3D11.Buffer.Create(D3D11Device, BindFlags.VertexBuffer, vertices);

            // SetVertexBuffers
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(vertexBuffer, Utilities.SizeOf<SimpleVertex>(), 0));


            uint[] indices = new uint[] { 0, 2, 1, 0, 3, 2 };
            var indexBuffer = SharpDX.Direct3D11.Buffer.Create(D3D11Device, BindFlags.IndexBuffer, indices);
            context.InputAssembler.SetIndexBuffer(indexBuffer, Format.R32_UInt, 0);

            var staticContantBuffer = new SharpDX.Direct3D11.Buffer(D3D11Device, Utilities.SizeOf<Matrix>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            context.VertexShader.SetConstantBuffer(0, staticContantBuffer);

            var mat = Matrix.Translation(-width * 0.6f / 2.0f, -height * 0.6f / 2.0f, 0.0f) * Matrix.OrthoLH(width, height, -1.0f, 1.0f);
            context.UpdateSubresource(ref mat, staticContantBuffer);

            var sampDesc = new SamplerStateDescription() {
                Filter = Filter.MinMagMipLinear,
                AddressU = TextureAddressMode.Wrap,
                AddressV = TextureAddressMode.Wrap,
                AddressW = TextureAddressMode.Wrap,
                ComparisonFunction = Comparison.Never,
                MinimumLod = 0.0f,
                MaximumLod = 0.0f,
            };

            var samplerLinear = new SamplerState(D3D11Device, sampDesc);
            context.PixelShader.SetSampler(0, samplerLinear);

            // Set primitive topology
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;

            fpsTimer.Start();
        }

        public async Task Start() {

            if (!Running) {
                ExitSem = new SemaphoreSlim(0, 1);

                await LoadResource();

                DeviceContext deferredContext;
                if (Mode == RunMode.Deferred) {
                    deferredContext = new DeviceContext(D3D11Device);
                    MainContext = deferredContext;
                } else {
                    MainContext = D3D11Device.ImmediateContext;
                }

                CreateRenderTargetView(MainContext);
                SetViewport(MainContext);
                PreparePipeline(MainContext);

                Running = true;

                while (Running) {
                    Update();
                    Render();
                }

                ExitSem.Release();
            }
            
        }

        public async Task Stop() {
            if (Running == true) {
                Running = false;
                Clear();
                if (ExitSem != null) {
                    await ExitSem.WaitAsync();
                    ExitSem = null;
                }
            }
        }

        public void Clear() {
            fpsTimer.Stop();
            Utilities.Dispose(ref textBrush);
            Utilities.Dispose(ref textFormat);
            Utilities.Dispose(ref infoTextFormat);
            Utilities.Dispose(ref pixelShader);
            Utilities.Dispose(ref vertexShader);
            Utilities.Dispose(ref renderTargetView);
            Utilities.Dispose(ref offScreenSurface);
            Utilities.Dispose(ref D2DDeviceContext);
            Utilities.Dispose(ref MainContext);
            Utilities.Dispose(ref SwapChain);
            Utilities.Dispose(ref CurrentAdapter);
            Utilities.Dispose(ref D3D11Device);
        }

        private async Task<byte[]> LoadShaderCodeFromFile(Uri uri) {

            byte[] code = null;

            var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
            if (file != null) {
                using (var stream = await file.OpenStreamForReadAsync()) {
                    code = new byte[(int)stream.Length];
                    stream.Read(code, 0, (int)stream.Length);
                }
            }
            return code;
        }

        private async Task<DirectDrawSurface> LoadDDSFromFile(Uri uri) {
            DirectDrawSurface dds = null;
            var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
            if (file != null) {
                using (var stream = await file.OpenStreamForReadAsync()) {
                    dds = new DirectDrawSurface(stream);
                }
            }
            return dds;
        }
    }
    public class DeviceInfo {
        public FeatureLevel FeatureLevel;
        public PointerSize DedicatedVideoMemory;
        public int VendorId;
        public int DeviceId;
        public string Description;

        public string Vender {
            get {
                switch (VendorId) {
                    case 0x8086:
                        return "Intel";
                    case 0x10DE:
                        return "NVIDIA";
                    case 0x1022:
                        return "AMD";
                    default:
                        return $"{VendorId:X4}";
                }
            }
        }

        public string D3DVersion {
            get {
                switch (FeatureLevel) {
                    case SharpDX.Direct3D.FeatureLevel.Level_12_1:
                        return "DirectX 12.1";
                    case SharpDX.Direct3D.FeatureLevel.Level_12_0:
                        return "DirectX 12.0";
                    case SharpDX.Direct3D.FeatureLevel.Level_11_1:
                        return "DirectX 11.1";
                    case SharpDX.Direct3D.FeatureLevel.Level_11_0:
                        return "DirectX 11.0";
                    case SharpDX.Direct3D.FeatureLevel.Level_10_1:
                        return "DirectX 11.1";
                    case SharpDX.Direct3D.FeatureLevel.Level_10_0:
                        return "DirectX 10.0";
                    case SharpDX.Direct3D.FeatureLevel.Level_9_3:
                        return "DirectX 9.3";
                    case SharpDX.Direct3D.FeatureLevel.Level_9_2:
                        return "DirectX 9.2";
                    case SharpDX.Direct3D.FeatureLevel.Level_9_1:
                        return "DirectX 9.1";
                    default:
                        return "DirectX Not Support";
                }
            }
        }

        public override string ToString() {

            StringBuilder sb = new StringBuilder();
            sb.Append($"{D3DVersion}\n");
            sb.Append($"{Vender}\n");
            sb.Append($"{Description}\n");
            sb.Append(string.Format(new PointerSizeFormat(), "{0}", DedicatedVideoMemory));
            return sb.ToString();
        }
    }

    public class PointerSizeFormat : IFormatProvider, ICustomFormatter {
        public object GetFormat(Type formatType) {
            if (formatType == typeof(ICustomFormatter))
                return this;
            else
                return null;
        }

        public string Format(string format, object arg, IFormatProvider formatProvider) {
            if (arg is PointerSize size) {
                StringBuilder sb = new StringBuilder();
                double s = size;
                if (s < (1 << 10)) {
                    sb.Append($"{s} bytes");
                } else if (s < (1 << 20)) {
                    sb.Append($"{s / (1 << 10):F0} KB");
                } else if (s < (1 << 30)) {
                    sb.Append($"{s / (1 << 20):F0} MB");
                } else {
                    sb.Append($"{s / (1 << 30):F0} GB");
                }
                return sb.ToString();
            } else {
                throw new FormatException(String.Format("The format of '{0}' is invalid.", format));
            }
        }
    }
}
