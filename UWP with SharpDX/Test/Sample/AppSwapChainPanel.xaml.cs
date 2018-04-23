using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Windows.UI.Xaml.Controls;
using Windows.Storage;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;

namespace Sample {
    /// <summary>
    /// 可以在本身使用或巡覽至框架內的空白頁面。
    /// </summary>
    public sealed partial class AppSwapChainPanel : SwapChainPanel {

        private Size2F Dpi;
        private Size2F ActualSize;

        enum RunMode {
            Immediate = 0,
            Deferred = 1,
        }

        RunMode Mode = RunMode.Deferred;

        private Adapter CurrentAdapter;
        private DeviceInfo deviceInfo;
        private SharpDX.Direct3D11.Device D3D11Device;
        private DeviceContext ImmediateContext;
        private DeviceContext Context;
        private SwapChain1 swapChain1;
        private RenderTargetView renderTargetView;
        private SharpDX.Direct2D1.DeviceContext D2DDeviceContext;
        private VertexShader vertexShader;
        private PixelShader pixelShader;
        InputLayout VertexLayout;

        public bool IsExit;
        public bool TearingSupport; // 支援關閉垂直同步

        Stopwatch fpsTimer = new Stopwatch();
        int fpsCounter = 0;

        private SharpDX.DirectWrite.TextFormat infoTextFormat;
        private SharpDX.DirectWrite.TextFormat textFormat;
        private SharpDX.Direct2D1.SolidColorBrush textBrush;

        public AppSwapChainPanel() {
            InitializeComponent();

            Unloaded += (a, b) => {
                IsExit = true;
            };

            Loaded += async (a, b) => {

                await Task.Run(async () => {

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


                    if (CurrentAdapter != null) await CreateDirectX();
                });
            };

            SizeChanged += (a, b) => {
                SetViewport(Context);
            };
        }

        async Task CreateDirectX() {
            CreateDevice();
            await LoadShader();
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                CreateSwapChain();
                DeviceContext deferredContext;
                if (Mode == RunMode.Deferred) {
                    deferredContext = new DeviceContext(D3D11Device);
                    CreateRenderTargetView(deferredContext);
                    SetViewport(deferredContext);
                    PreparePipeline(deferredContext);
                    Context = deferredContext;
                } else {
                    CreateRenderTargetView(ImmediateContext);
                    SetViewport(ImmediateContext);
                    PreparePipeline(ImmediateContext);
                    Context = ImmediateContext;
                }
            });

            while (!IsExit) {
                Update();
                Render();
            }
        }

        void CreateDevice() {
            FeatureLevel[] featureLevels = new FeatureLevel[] {
                FeatureLevel.Level_12_1,
                FeatureLevel.Level_12_0,
                FeatureLevel.Level_11_1,
                FeatureLevel.Level_11_0,
            };
            DeviceCreationFlags flags = DeviceCreationFlags.BgraSupport;
#if DEBUG
            flags |= DeviceCreationFlags.Debug;
#endif
            D3D11Device = new SharpDX.Direct3D11.Device(CurrentAdapter, flags, featureLevels);
            ImmediateContext = D3D11Device.ImmediateContext;
            var desc = CurrentAdapter.Description;
            deviceInfo.Description = desc.Description;
            deviceInfo.VendorId = desc.VendorId;
            deviceInfo.DeviceId = desc.DeviceId;
            deviceInfo.DedicatedVideoMemory = desc.DedicatedVideoMemory;
            deviceInfo.FeatureLevel = D3D11Device.FeatureLevel;
        }

        void CreateSwapChain() {
            // https://docs.microsoft.com/en-us/windows/uwp/gaming/multisampling--multi-sample-anti-aliasing--in-windows-store-apps
            SwapChainDescription1 swapChainDescription = new SwapChainDescription1() {
                Usage = Usage.RenderTargetOutput | Usage.BackBuffer,
                BufferCount = 2,
                SwapEffect = SwapEffect.FlipSequential, // UWP 只能用 flip model
                SampleDescription = new SampleDescription(1, 0), // 在flip model下SwapChain不能開multi-sampling，只能另闢蹊徑
                Scaling = Scaling.Stretch,
                Flags = SwapChainFlags.AllowModeSwitch | SwapChainFlags.AllowTearing,
                Format = Format.R8G8B8A8_UNorm,
                Height = 1080,
                Width = 1920,
            };

            Dictionary<int, int> count_quality_levels = new Dictionary<int, int>();

            for (int i = 1; i <= SharpDX.Direct3D11.Device.MultisampleCountMaximum; i++) {
                var quality = D3D11Device.CheckMultisampleQualityLevels(Format.R8G8B8A8_UNorm, i);
                count_quality_levels.Add(i, quality);
            }

            D3D11Device.CheckThreadingSupport(out bool supportConcurentResources, out bool supportCommandList);

            // 建立SwapChain
            using (SharpDX.DXGI.Device3 dxgiDevice3 = D3D11Device.QueryInterface<SharpDX.DXGI.Device3>()) {
                using (Factory2 dxgiFactory2 = dxgiDevice3.Adapter.GetParent<Factory2>()) {
                    swapChain1 = new SwapChain1(dxgiFactory2, D3D11Device, ref swapChainDescription);
                    swapChain1.QueryInterface<SwapChain>();
                }
            }

            // 把Xaml的SwapChainPanel與DirectX的SwapChain連結起來
            using (ISwapChainPanelNative swapChainPanelNative = ComObject.As<ISwapChainPanelNative>(this)) {
                swapChainPanelNative.SwapChain = swapChain1;
            }
        }

        Texture2D offScreenSurface;
        DepthStencilView depthView;

        void CreateRenderTargetView(DeviceContext context) {

            offScreenSurface = new Texture2D(D3D11Device, new Texture2DDescription {
                Format = Format.R8G8B8A8_UNorm,
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
                Format = Format.R8G8B8A8_UNorm,
                Dimension = RenderTargetViewDimension.Texture2DMultisampled,
            });

            using (var backbuffer = swapChain1.GetBackBuffer<Texture2D>(0))
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
                    DpiX = Dpi.Width,
                    DpiY = Dpi.Height,
                    PixelFormat = new SharpDX.Direct2D1.PixelFormat(Format.Unknown, SharpDX.Direct2D1.AlphaMode.Premultiplied),
                })) {
                    // 將 D2DDeviceContext 的 Target 指向SwapChain 的 Surface
                    D2DDeviceContext.Target = bitmap;
                }
            }

            PrepareD2D();

            using (var BackBuffer = swapChain1.GetBackBuffer<Texture2D>(0)) {
                renderTargetView = new RenderTargetView(D3D11Device, BackBuffer);
            }

            context.OutputMerger.SetTargets(depthView, renderTargetView);
        }

        void SetViewport(DeviceContext context) {
            ActualSize = new Size2F((float)(Math.Floor(ActualWidth)), (float)(Math.Floor(ActualHeight)));
            if (context != null) {
                context.Rasterizer.SetViewport(0, 0, ActualSize.Width, ActualSize.Height);
            }
        }

        async Task LoadShader() {
            var VertexShaderByteCode = await LoadShaderCodeFromFile(new Uri("ms-appx:///Shader/VertexShader.cso"));
            var PixelShaderByteCode = await LoadShaderCodeFromFile(new Uri("ms-appx:///Shader/PixelShader.cso"));

            vertexShader = new VertexShader(D3D11Device, VertexShaderByteCode);
            pixelShader = new PixelShader(D3D11Device, PixelShaderByteCode);

            InputElement[] layout = new InputElement[] {
                new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 0),
                new InputElement("COLOR", 0, Format.R32G32B32A32_Float, 16, 0),
            };
            VertexLayout = new InputLayout(D3D11Device, VertexShaderByteCode, layout);
        }

        void PreparePipeline(DeviceContext context) {

            context.InputAssembler.InputLayout = VertexLayout;
            context.VertexShader.Set(vertexShader);
            context.PixelShader.Set(pixelShader);

            float width = 1920.0f;
            float height = 1080.0f;

            var vertices = new SimpleVertex[] {
                new SimpleVertex { Position = new Vector4(0.0f, height, 1.0f, 1.0f), Color = new Vector4(1.0f, 0.0f, 0.0f, 1.0f)},
                
                new SimpleVertex { Position = new Vector4(0.0f, 0.0f, 1.0f, 1.0f), Color = new Vector4(0.0f, 0.0f, 1.0f, 1.0f)},

                new SimpleVertex { Position = new Vector4(width, 0.0f, 1.0f, 1.0f), Color = new Vector4(0.0f, 1.0f, 0.0f, 1.0f)},

                new SimpleVertex { Position = new Vector4(width, height -200.0f, 1.0f, 1.0f), Color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f)},
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
            
            var mat = Matrix.Translation(-width/2.0f, -height/2.0f, 0.0f) * Matrix.OrthoLH(width, height, -1.0f, 1.0f);
            context.UpdateSubresource(ref mat, staticContantBuffer);

            // Set primitive topology
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;

            fpsTimer.Start();
        }

        string fpsString = "0";

        void Update() {
            // 更新資料
            fpsCounter++;
            if (fpsTimer.ElapsedMilliseconds > 1000) {
                var fps = 1000.0 * fpsCounter / fpsTimer.ElapsedMilliseconds;
                fpsString = $"{fps:F1}";
                fpsCounter = 0;
                fpsTimer.Reset();
                fpsTimer.Start();
            }
        }

        void Render() {
            if (Context != null) {
                
                // 把renderTargetView綁定到Output-Merger Stage
                Context.OutputMerger.SetRenderTargets(renderTargetView);

                // 初始化模板暫存區
                Context.ClearDepthStencilView(depthView, DepthStencilClearFlags.Depth, 1.0f, 0);
                // https://msdn.microsoft.com/en-us/library/windows/desktop/bb205120(v=vs.85).aspx
                
                // 填滿背景色
                Context.ClearRenderTargetView(renderTargetView, Color.Black);

                // 畫
                Context.DrawIndexed(3 * 2, 0, 0);

                if (Mode == RunMode.Deferred) {
                    // TRUE means restore the state of the device context to its pre-command list condition when recording is completed.
                    var commandList = Context.FinishCommandList(true);
                        ImmediateContext.ExecuteCommandList(commandList, false);
                    commandList.Dispose();
                    commandList = null;
                }

                // https://docs.microsoft.com/en-us/windows/uwp/gaming/multisampling--multi-sample-anti-aliasing--in-windows-store-apps
                using (var BackBuffer = swapChain1.GetBackBuffer<Texture2D>(0)) {
                    var index = SharpDX.Direct3D11.Resource.CalculateSubResourceIndex(0, 0, 1);
                    Context.ResolveSubresource(offScreenSurface, index, BackBuffer, index, Format.R8G8B8A8_UNorm);
                }

                D2DDraw();

                // 把畫好的結果輸出到螢幕上！
                // syncInterval = 0 表示不同步
                swapChain1.Present(0, TearingSupport ? PresentFlags.AllowTearing : PresentFlags.None);
            }
        }

        void PrepareD2D() {
            var writeFactory = new SharpDX.DirectWrite.Factory();
            textFormat = new SharpDX.DirectWrite.TextFormat(writeFactory, "微軟正黑體", 26.0f);
            infoTextFormat = new SharpDX.DirectWrite.TextFormat(writeFactory, "微軟正黑體", 14.0f);
            textBrush = new SharpDX.Direct2D1.SolidColorBrush(D2DDeviceContext, Color.ForestGreen);
        }

        void D2DDraw() {
            D2DDeviceContext.BeginDraw();
            D2DDeviceContext.DrawText(deviceInfo.ToString(), infoTextFormat, new RectangleF(0, 0, 250, 100), textBrush);
            D2DDeviceContext.DrawText(fpsString, textFormat, new RectangleF(0, ActualSize.Height - 30, 100, 30), textBrush);
            D2DDeviceContext.EndDraw();
        }

        async Task<byte[]> LoadShaderCodeFromFile(Uri uri) {
            var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
            if (file != null) {
                using (var stream = await file.OpenStreamForReadAsync()) {
                    var bytes = new byte[(int)stream.Length];
                    stream.Read(bytes, 0, (int)stream.Length);
                    return bytes;
                }
            }
            return null;
        }
    }

    public struct SimpleVertex {
        public Vector4 Position {
            get; set;
        }

        public Vector4 Color {
            get; set;
        }
    }

    public struct DeviceInfo {
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
                if (size < (1 << 10)) {
                    sb.Append($"{size} bytes");
                } else if (size < (1 << 20)) {
                    sb.Append($"{size / (1 << 10)} KB");
                } else if (size < (1 << 30)) {
                    sb.Append($"{size / (1 << 20)} MB");
                } else {
                    sb.Append($"{size / (1 << 30)} GB");
                }
                return sb.ToString();
            } else {
                throw new FormatException(String.Format("The format of '{0}' is invalid.", format));
            }
        }
    }
}
