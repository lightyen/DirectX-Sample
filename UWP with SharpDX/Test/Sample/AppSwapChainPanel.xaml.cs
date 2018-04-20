using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Diagnostics;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Storage;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;


namespace Sample
{
    /// <summary>
    /// 可以在本身使用或巡覽至框架內的空白頁面。
    /// </summary>
    public sealed partial class AppSwapChainPanel : SwapChainPanel {

        private double dpi;

        enum RunMode {
            Immediate = 0,
            Deferred = 1,
        }

        RunMode Mode = RunMode.Deferred;

        private Factory1 DXGIFactory1;
        private Adapter CurrentAdapter;

        private SharpDX.Direct3D11.Device D3D11Device;
        private DeviceContext ImmediateContext;
        private DeviceContext Context;
        private SwapChain1 swapChain1;
        private RenderTargetView renderTargetView;
        private SharpDX.Direct2D1.DeviceContext1 D2DDeviceContext;
        private VertexShader vertexShader;
        private PixelShader pixelShader;
        InputLayout VertexLayout;

        public bool IsExit;
        public bool TearingSupport; // 支援關閉垂直同步

        Stopwatch clock = new Stopwatch();
        Stopwatch fpsTimer = new Stopwatch();
        int fpsCounter = 0;

        public AppSwapChainPanel() {
            this.InitializeComponent();

            dpi = Windows.Graphics.Display.DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel;

            this.Unloaded += (a, b) => {
                IsExit = true;
            };

            Loaded += async (a, b) => {

                await Task.Run(async () => {

                    DXGIFactory1 = new Factory1();
                    if (DXGIFactory1 == null) {
                        return;
                    }

                    using (var factory5 = DXGIFactory1.QueryInterface<Factory5>()) {
                        int allowTearing = 0;
                        IntPtr data = System.Runtime.InteropServices.Marshal.AllocHGlobal(4);
                        factory5.CheckFeatureSupport(SharpDX.DXGI.Feature.PresentAllowTearing, data, sizeof(int));
                        allowTearing = System.Runtime.InteropServices.Marshal.PtrToStructure<int>(data);
                        System.Runtime.InteropServices.Marshal.FreeHGlobal(data);
                        if (allowTearing == 1) TearingSupport = true;
                    }

                        var list = new Factory1().Adapters.ToList();
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
                FeatureLevel.Level_11_1,
                FeatureLevel.Level_11_0,
            };
            D3D11Device = new SharpDX.Direct3D11.Device(CurrentAdapter, DeviceCreationFlags.Debug, featureLevels);
            ImmediateContext = D3D11Device.ImmediateContext;
            var desc = CurrentAdapter.Description;
            Debug.WriteLine(desc.Description);
            switch (desc.VendorId) {
                case 0x8086:
                    Debug.WriteLine($"Intel");
                    break;
                case 0x10DE:
                    Debug.WriteLine($"NVIDIA");
                    break;
                case 0x1022:
                    Debug.WriteLine($"AMD");
                    break;
                default:
                    Debug.WriteLine($"vender = {desc.VendorId:X4}");
                    break;
            }

            var s = desc.DedicatedVideoMemory / (1 << 20);
            var t = 'M';
            if (s > 1024) {
                s /= 1024;
                t = 'G';
            }
            Debug.WriteLine($"顯示記憶體大小 : {s}{t}");
            Debug.WriteLine($"DirectX使用版本: { Enum.GetName(typeof(FeatureLevel), D3D11Device.FeatureLevel) }");
            Debug.WriteLine($"裝置識別碼     : {desc.DeviceId:X4}");
        }

        void CreateSwapChain() {
            // https://docs.microsoft.com/en-us/windows/uwp/gaming/multisampling--multi-sample-anti-aliasing--in-windows-store-apps
            SwapChainDescription1 swapChainDescription = new SwapChainDescription1() {
                Usage = Usage.RenderTargetOutput,
                BufferCount = 2,
                SwapEffect = SwapEffect.FlipSequential, // UWP 只能用 flip model
                Stereo = false,
                SampleDescription = new SampleDescription(1, 0), // 在flip model下SwapChain不能開multi-sampling，只能另闢蹊徑
                Scaling = Scaling.Stretch,
                Flags = SwapChainFlags.AllowModeSwitch | SwapChainFlags.AllowTearing,
                Format = Format.R8G8B8A8_UNorm,
                Height = 1080,
                Width = 1920,
            };

            Dictionary<int, int> count_quality_levels = new Dictionary<int, int>();

            for (int i = 1; i <= 32; i = i * 2) {
                var quality = D3D11Device.CheckMultisampleQualityLevels(Format.R32G32B32A32_Float, i);
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

        Texture2D BackBuffer;
        Texture2D offScreenSurface;
        DepthStencilView depthView;

        void CreateRenderTargetView(DeviceContext context) {

            offScreenSurface = new Texture2D(D3D11Device, new Texture2DDescription {
                Format = Format.R32G32B32A32_Float,
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
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.DepthStencil,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None,
            });

            depthView = new DepthStencilView(D3D11Device, depthBuffer);

            renderTargetView = new RenderTargetView(D3D11Device, offScreenSurface, new RenderTargetViewDescription {
                Format = Format.R32G32B32A32_Float,
                Dimension = RenderTargetViewDimension.Texture2DMultisampled,
            });

            using (var backbuffer = swapChain1.GetBackBuffer<Texture2D>(0))
            using (var Surface = backbuffer.QueryInterface<Surface2>())
            using (var dxgiDevice = D3D11Device.QueryInterface<SharpDX.DXGI.Device3>())
            using (var factory = new SharpDX.Direct2D1.Factory2()) {

                var d2ddevice = new SharpDX.Direct2D1.Device1(factory, dxgiDevice);
                D2DDeviceContext = new SharpDX.Direct2D1.DeviceContext1(d2ddevice, SharpDX.Direct2D1.DeviceContextOptions.None);

                using (var bitmap = new SharpDX.Direct2D1.Bitmap(D2DDeviceContext, Surface, new SharpDX.Direct2D1.BitmapProperties {
                    DpiX = factory.DesktopDpi.Width,
                    DpiY = factory.DesktopDpi.Height,
                    PixelFormat = new SharpDX.Direct2D1.PixelFormat(Format.Unknown, SharpDX.Direct2D1.AlphaMode.Premultiplied),
                })) {

                }

                //var gsd = new SharpDX.Direct2D1.RenderTarget(fac, Surface, new SharpDX.Direct2D1.RenderTargetProperties() {
                //    PixelFormat = new SharpDX.Direct2D1.PixelFormat(Format.Unknown, SharpDX.Direct2D1.AlphaMode.Premultiplied),
                //    DpiX = fac.DesktopDpi.Width,
                //    DpiY = fac.DesktopDpi.Height,
                //});
            }

            BackBuffer = SharpDX.Direct3D11.Resource.FromSwapChain<Texture2D>(swapChain1, 0);
            renderTargetView = new RenderTargetView(D3D11Device, BackBuffer);
            Utilities.Dispose(ref BackBuffer);

            context.OutputMerger.SetTargets(depthView, renderTargetView);
        }

        void SetViewport(DeviceContext context) {
            if (context != null) {
                context.Rasterizer.SetViewport(0, 0, (float)(Math.Floor(ActualWidth)), (float)(Math.Floor(ActualHeight)));
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

            clock.Start();
            fpsTimer.Start();
        }

        void Update() {
            // 更新資料
            // 並沒有
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

                // 畫一個三角形
                Context.DrawIndexed(3 * 2, 0, 0);

                // https://docs.microsoft.com/en-us/windows/uwp/gaming/multisampling--multi-sample-anti-aliasing--in-windows-store-apps
                //BackBuffer = SharpDX.Direct3D11.Resource.FromSwapChain<Texture2D>(swapChain1, 0);
                //var index = SharpDX.Direct3D11.Resource.CalculateSubResourceIndex(0, 0, 1);
                //Context.ResolveSubresource(offScreenSurface, index, BackBuffer, index, Format.R32G32B32A32_Float);
                //Utilities.Dispose(ref BackBuffer);

                BackBuffer = SharpDX.Direct3D11.Resource.FromSwapChain<Texture2D>(swapChain1, 0);

                if (Mode == RunMode.Deferred) {
                    // TRUE means restore the state of the device context to its pre-command list condition when recording is completed.
                    var commandList = Context.FinishCommandList(true);
                        ImmediateContext.ExecuteCommandList(commandList, false);
                    commandList.Dispose();
                    commandList = null;
                }

                // 把畫好的結果輸出到螢幕上！
                // syncInterval = 0 表示不同步
                swapChain1.Present(0, TearingSupport ? PresentFlags.AllowTearing : PresentFlags.None);
            }
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

        void ClearResource() {


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
}
