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
        private SwapChain swapChain;
        private RenderTargetView renderTargetView;
        private VertexShader vertexShader;
        private PixelShader pixelShader;

        public AppSwapChainPanel() {
            this.InitializeComponent();

            DXGIFactory1 = new Factory1();
            if (DXGIFactory1 == null) {
                return;
            }

            dpi = Windows.Graphics.Display.DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel;

            var list = new Factory1().Adapters.ToList();
            var query = from a in list
                        where a.Description.VendorId == 0x10DE || a.Description.VendorId == 0x1022 || a.Description.VendorId == 0x8086
                        select a;
            query.OrderBy((x) => {
                if (x.Description.VendorId == 0x10DE) return 0;
                if (x.Description.VendorId == 0x1022) return 1;
                if (x.Description.VendorId == 0x8086) return 2;
                return 3;
            });

            CurrentAdapter = query.ElementAtOrDefault(0);

            Loaded += (a, b) => {
                if (CurrentAdapter != null) CreateDirectX();
            };
        }

        void CreateDirectX() {

            CreateDevice();
            CreateSwapChain();

            CreateRenderTargetView();
            SetViewport();
            LoadShader();
            PreparePipeline();

            //Render();
        }

        void CreateDevice() {
            FeatureLevel[] featureLevels = new FeatureLevel[] {
                FeatureLevel.Level_11_1,
                FeatureLevel.Level_11_0,
                FeatureLevel.Level_10_1,
                FeatureLevel.Level_10_0,
            };
            D3D11Device = new SharpDX.Direct3D11.Device(CurrentAdapter, DeviceCreationFlags.Debug, featureLevels);
            var desc = CurrentAdapter.Description;
            Debug.WriteLine(desc.Description);
            Debug.WriteLine($"vender = {desc.VendorId:X4}");
            Debug.WriteLine($"Shared Memory: {desc.SharedSystemMemory} bytes");
            Debug.WriteLine($"Video Memory: {desc.DedicatedVideoMemory} bytes");
            Debug.WriteLine($"device: {desc.DeviceId}");
        }

        void CreateSwapChain() {

            SwapChainDescription1 swapChainDescription = new SwapChainDescription1() {
                // Double buffer.
                BufferCount = 2,
                // BGRA 32bit pixel format.
                Format = Format.B8G8R8A8_UNorm,
                // Unlike in CoreWindow swap chains, the dimensions must be set.
                Height = (int)(ActualHeight * dpi),
                Width = (int)(ActualWidth * dpi),
                // Default multisampling.
                SampleDescription = new SampleDescription(1, 0),
                // In case the control is resized, stretch the swap chain accordingly.
                Scaling = Scaling.Stretch,
                // No support for stereo display.
                //Stereo = false,
                // Sequential displaying for double buffering.
                SwapEffect = SwapEffect.FlipSequential,
                // This swapchain is going to be used as the back buffer.
                Usage = Usage.RenderTargetOutput | Usage.BackBuffer,
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
            D3D11Device.ImmediateContext.OutputMerger.SetRenderTargets(renderTargetView);
        }

        void SetViewport() {
            D3D11Device.ImmediateContext.Rasterizer.SetViewport(0, 0, (float)(ActualWidth * dpi), (float)(ActualHeight * dpi), 0.0f, 1.0f);
        }

        async void LoadShader() {
            var VertexShaderByteCode = await LoadShaderCodeFromFile(new Uri("ms-appx:///Shader/VertexShader.cso"));
            var PixelShaderByteCode = await LoadShaderCodeFromFile(new Uri("ms-appx:///Shader/PixelShader.cso"));

            vertexShader = new VertexShader(D3D11Device, VertexShaderByteCode);

            InputElement[] layout = new InputElement[] {
                new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 0),
            };

            InputLayout VertexLayout = new InputLayout(D3D11Device, VertexShaderByteCode, layout);
            D3D11Device.ImmediateContext.InputAssembler.InputLayout = VertexLayout;          
            pixelShader = new PixelShader(D3D11Device, PixelShaderByteCode);
        }

        void PreparePipeline() {
            //https://github.com/sharpdx/SharpDX-Samples/blob/master/Desktop/Direct3D11/MiniCube/Program.cs
            var vertices = SharpDX.Direct3D11.Buffer.Create(D3D11Device, BindFlags.VertexBuffer, new[] {
                new Vector4(0.0f, 0.5f, 0.5f, 1.0f),
                new Vector4(0.5f, -0.5f, 0.5f, 1.0f),
                new Vector4(-0.5f, -0.5f, 0.5f, 1.0f)
            });

            //SimpleVertex[] vertices = new SimpleVertex[]
            //{
            //    new SimpleVertex { Position = new Vector3(0.0f, 0.5f, 0.5f) },
            //    new SimpleVertex { Position = new Vector3(0.5f, -0.5f, 0.5f) },
            //    new SimpleVertex { Position = new Vector3(-0.5f, -0.5f, 0.5f) },
            //};

            //BufferDescription desc = new BufferDescription {
            //    Usage = ResourceUsage.Default,
            //    SizeInBytes = Utilities.SizeOf<SimpleVertex>() * 3,
            //    BindFlags = BindFlags.VertexBuffer,
            //    CpuAccessFlags = 0,
            //};
            //var datastream = DataStream.Create(vertices, true, false);

            //var vertexBuffer = new SharpDX.Direct3D11.Buffer(D3D11Device, datastream, desc);

            D3D11Device.ImmediateContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(vertices, Utilities.SizeOf<Vector4>(), 0));
            // Set primitive topology
            D3D11Device.ImmediateContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;

            D3D11Device.ImmediateContext.VertexShader.Set(vertexShader);
            D3D11Device.ImmediateContext.PixelShader.Set(pixelShader);

            D3D11Device.ImmediateContext.OutputMerger.SetTargets(renderTargetView);
        }

        public void Render() {
            if (D3D11Device.ImmediateContext != null) {
                // Clear Screen to Teal.
                D3D11Device.ImmediateContext.ClearRenderTargetView(renderTargetView, Color.Teal);

                // 畫一個三角形
                D3D11Device.ImmediateContext.Draw(3, 0);

                swapChain.Present(0, PresentFlags.None);
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
        public Vector3 Position {
            get; set;
        }
    }
}
