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

        private Factory1 DXGIFactory1;
        private Adapter CurrentAdapter;

        private SharpDX.Direct3D11.Device D3D11Device;
        private DeviceContext ImmediateContext;
        private SwapChain1 swapChain1;
        private RenderTargetView renderTargetView;
        private VertexShader vertexShader;
        private PixelShader pixelShader;
        public bool IsExit;
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
                SetViewport();
            };
        }

        async Task CreateDirectX() {
            CreateDevice();
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                CreateSwapChain();
            });
            CreateRenderTargetView();
            await LoadShader();
            PreparePipeline();

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
            Debug.WriteLine($"vender = {desc.VendorId:X4}");
            Debug.WriteLine($"Shared Memory: {desc.SharedSystemMemory} bytes");
            Debug.WriteLine($"Video Memory: {desc.DedicatedVideoMemory} bytes");
            Debug.WriteLine($"device: {desc.DeviceId}");
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
                Format = Format.R8G8B8A8_UNorm,
                Height = 1080,
                Width = 1920,
            };

            Dictionary<int, int> count_quality_levels = new Dictionary<int, int>();

            for (int i = 1; i <= 32; i = i * 2) {
                var quality = D3D11Device.CheckMultisampleQualityLevels(Format.R32G32B32A32_Float, i);
                count_quality_levels.Add(i, quality);
            }

            

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
                SetViewport();
            }
        }

        Texture2D BackBuffer;
        Texture2D offScreenSurface;
        DeviceContext deferedContext;

        void CreateRenderTargetView() {

            offScreenSurface = new Texture2D(D3D11Device, new Texture2DDescription {
                Format = Format.R32G32B32A32_Float,
                Width = 1920,
                Height = 1080,
                BindFlags = BindFlags.RenderTarget,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = new SampleDescription(4, 0),
            });

            renderTargetView = new RenderTargetView(D3D11Device, offScreenSurface, new RenderTargetViewDescription {
                Format = Format.R32G32B32A32_Float,
                Dimension = RenderTargetViewDimension.Texture2DMultisampled,
            });

            deferedContext = new DeviceContext(D3D11Device);

            BackBuffer = SharpDX.Direct3D11.Resource.FromSwapChain<Texture2D>(swapChain1, 0);
            //renderTargetView = new RenderTargetView(D3D11Device, backBuffer);
            //Utilities.Dispose(ref backBuffer);

            ImmediateContext.OutputMerger.SetTargets(renderTargetView);
        }

        void SetViewport() {
            if (ImmediateContext != null) {
                ImmediateContext.Rasterizer.SetViewport(0, 0, (float)(Math.Floor(ActualWidth)), (float)(Math.Floor(ActualHeight)));

                
            }
        }

        async Task LoadShader() {
            var VertexShaderByteCode = await LoadShaderCodeFromFile(new Uri("ms-appx:///Shader/VertexShader.cso"));
            var PixelShaderByteCode = await LoadShaderCodeFromFile(new Uri("ms-appx:///Shader/PixelShader.cso"));

            vertexShader = new VertexShader(D3D11Device, VertexShaderByteCode);

            InputElement[] layout = new InputElement[] {
                new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 0),
                new InputElement("COLOR", 0, Format.R32G32B32A32_Float, 16, 0),
            };

            InputLayout VertexLayout = new InputLayout(D3D11Device, VertexShaderByteCode, layout);
            ImmediateContext.InputAssembler.InputLayout = VertexLayout;          
            pixelShader = new PixelShader(D3D11Device, PixelShaderByteCode);

            ImmediateContext.VertexShader.Set(vertexShader);
            ImmediateContext.PixelShader.Set(pixelShader);
        }

        void PreparePipeline() {

            float width = 1920.0f;
            float height = 1080.0f;

            var vertices = new SimpleVertex[] {
                new SimpleVertex { Position = new Vector4(0.0f, height, 1.0f, 1.0f), Color = new Vector4(1.0f, 0.0f, 0.0f, 1.0f)},
                
                new SimpleVertex { Position = new Vector4(0.0f, 0.0f, 1.0f, 1.0f), Color = new Vector4(0.0f, 0.0f, 1.0f, 1.0f)},

                new SimpleVertex { Position = new Vector4(width, 0.0f, 1.0f, 1.0f), Color = new Vector4(0.0f, 1.0f, 0.0f, 1.0f)},

                new SimpleVertex { Position = new Vector4(width, height -200.0f, 1.0f, 1.0f), Color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f)},
            };

            // CreateBuffer
            var vertexBuffer = SharpDX.Direct3D11.Buffer.Create(D3D11Device, BindFlags.VertexBuffer, vertices);

            // SetVertexBuffers
            ImmediateContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(vertexBuffer, Utilities.SizeOf<SimpleVertex>(), 0));


            uint[] indices = new uint[] { 0, 2, 1, 0, 3, 2 };
            var indexBuffer = SharpDX.Direct3D11.Buffer.Create(D3D11Device, BindFlags.IndexBuffer, indices);
            ImmediateContext.InputAssembler.SetIndexBuffer(indexBuffer, Format.R32_UInt, 0);

            var staticContantBuffer = new SharpDX.Direct3D11.Buffer(D3D11Device, Utilities.SizeOf<Matrix>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            ImmediateContext.VertexShader.SetConstantBuffer(0, staticContantBuffer);
            
            var mat = Matrix.Translation(-width/2.0f, -height/2.0f, 0.0f) * Matrix.OrthoLH(width, height, -1.0f, 1.0f);
            ImmediateContext.UpdateSubresource(ref mat, staticContantBuffer);

            // Set primitive topology
            ImmediateContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
        }

        

        void Update() {
            // 更新資料
            // 並沒有
        }

        void Render() {
            if (ImmediateContext != null) {

                // https://msdn.microsoft.com/en-us/library/windows/desktop/bb205120(v=vs.85).aspx
                // 把renderTargetView綁定到Output-Merger Stage
                ImmediateContext.OutputMerger.SetRenderTargets(renderTargetView);
                // 填滿背景色
                ImmediateContext.ClearRenderTargetView(renderTargetView, Color.Black);
                // 畫一個三角形
                ImmediateContext.DrawIndexed(3 * 2, 0, 0);

                var index = SharpDX.Direct3D11.Resource.CalculateSubResourceIndex(0, 0, 1);
                ImmediateContext.ResolveSubresource(offScreenSurface, index, BackBuffer, index, Format.R32G32B32A32_Float);

                // 把畫好的結果輸出到螢幕上！
                swapChain1.Present(1, PresentFlags.None);
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
