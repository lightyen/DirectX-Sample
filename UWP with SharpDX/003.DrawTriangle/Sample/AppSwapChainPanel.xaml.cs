using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using System.Diagnostics;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Windows.Storage;

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
        private SharpDX.Direct3D11.DeviceContext ImmediateContext;
        private SwapChain swapChain;
        private RenderTargetView renderTargetView;
        private VertexShader vertexShader;
        private PixelShader pixelShader;
        private bool Closed;
        public AppSwapChainPanel() {
            this.InitializeComponent();

            dpi = Windows.Graphics.Display.DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel;

            this.Unloaded += (a, b) => {
                Closed = true;
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
        }

        async Task CreateDirectX() {

            CreateDevice();
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                CreateSwapChain();
                SetViewport();
            });
            CreateRenderTargetView();
            await LoadShader();
            PreparePipeline();

            while (!Closed) {
                Update();
                Render();
            }
        }

        void CreateDevice() {
            FeatureLevel[] featureLevels = new FeatureLevel[] {
                FeatureLevel.Level_11_1,
                FeatureLevel.Level_11_0,
                FeatureLevel.Level_10_1,
                FeatureLevel.Level_10_0,
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

            SwapChainDescription1 swapChainDescription = new SwapChainDescription1() {
                Usage = Usage.RenderTargetOutput,
                BufferCount = 2,
                SwapEffect = SwapEffect.FlipSequential,
                Stereo = false,
                SampleDescription = new SampleDescription(1, 0),
                Scaling = Scaling.Stretch,
                Format = Format.R8G8B8A8_UNorm,
                Height = (int)(ActualHeight),
                Width = (int)(ActualWidth),
            };

            // 建立SwapChain
            using (SharpDX.DXGI.Device3 dxgiDevice3 = D3D11Device.QueryInterface<SharpDX.DXGI.Device3>()) {
                using (Factory2 dxgiFactory2 = dxgiDevice3.Adapter.GetParent<Factory2>()) {
                    using (SwapChain1 swapChain1 = new SwapChain1(dxgiFactory2, D3D11Device, ref swapChainDescription)) {
                        swapChain = swapChain1.QueryInterface<SwapChain>();
                    }
                }
            }

            // 把Xaml的SwapChainPanel與DirectX的SwapChain連結起來
            using (ISwapChainPanelNative swapChainPanelNative = ComObject.As<ISwapChainPanelNative>(this)) {
                swapChainPanelNative.SwapChain = swapChain;
            }
        }

        void CreateRenderTargetView() {
            var backBuffer = SharpDX.Direct3D11.Resource.FromSwapChain<Texture2D>(swapChain, 0);
            renderTargetView = new RenderTargetView(D3D11Device, backBuffer);
            Utilities.Dispose(ref backBuffer);

            ImmediateContext.OutputMerger.SetTargets(renderTargetView);
        }

        void SetViewport() {
            ImmediateContext.Rasterizer.SetViewport(0, 0, (float)(ActualWidth), (float)(ActualHeight), 0.0f, 1.0f);
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

            var vertices = new SimpleVertex[] {
                new SimpleVertex { Position = new Vector4(0.0f, 0.5f, 0.5f, 1.0f), Color = new Vector4(1.0f, 0.0f, 0.0f, 1.0f)},
                new SimpleVertex { Position = new Vector4(0.5f, -0.5f, 0.5f, 1.0f), Color = new Vector4(0.0f, 1.0f, 0.0f, 1.0f)},
                new SimpleVertex { Position = new Vector4(-0.5f, -0.5f, 0.5f, 1.0f), Color = new Vector4(0.0f, 0.0f, 1.0f, 1.0f)},
            };

            var tices = new[] {
                new Vector4(0.0f, 0.5f, 0.5f, 1.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f),
                new Vector4(0.5f, -0.5f, 0.5f, 1.0f), new Vector4(0.0f, 1.0f, 0.0f, 1.0f),
                new Vector4(-0.5f, -0.5f, 0.5f, 1.0f), new Vector4(0.0f, 0.0f, 1.0f, 1.0f),
            };

            // CreateBuffer
            var buffer = SharpDX.Direct3D11.Buffer.Create(D3D11Device, BindFlags.VertexBuffer, tices);

            // SetVertexBuffers
            ImmediateContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(buffer, 4 * 8, 0));

            // Set primitive topology
            ImmediateContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
        }

        void Update() {

        }

        void Render() {
            if (ImmediateContext != null) {
                ImmediateContext.OutputMerger.SetRenderTargets(renderTargetView);
                // Clear Screen to Teal.
                ImmediateContext.ClearRenderTargetView(renderTargetView, Color.Teal);

                // 畫一個三角形
                ImmediateContext.Draw(3, 0);

                swapChain.Present(1, PresentFlags.None);
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
