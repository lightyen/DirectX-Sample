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

namespace Sample
{
    /// <summary>
    /// 可以在本身使用或巡覽至框架內的空白頁面。
    /// </summary>
    public sealed partial class AppSwapChainPanel : SwapChainPanel {

        private SharpDX.Direct3D11.Device D3D11Device;
        private SwapChain swapChain;
        private Texture2D backBuffer;
        private DeviceContext context;

        public AppSwapChainPanel() {
            this.InitializeComponent();

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

            Adapter adapter = query.ElementAtOrDefault(0);

            Loaded += (a, b) => {
                if (adapter != null) CreateDirectXSwapChain(adapter);
            };
        }

        public void CreateDirectXSwapChain(Adapter adapter) {

            var desc = adapter.Description;
            Debug.WriteLine(desc.Description);
            Debug.WriteLine($"vender = {desc.VendorId:X4}");
            Debug.WriteLine($"Shared Memory: {desc.SharedSystemMemory} bytes");
            Debug.WriteLine($"Video Memory: {desc.DedicatedVideoMemory} bytes");
            Debug.WriteLine($"device: {desc.DeviceId}");

            FeatureLevel[] featureLevels = new FeatureLevel[] {
                FeatureLevel.Level_11_1,
                FeatureLevel.Level_11_0,
                FeatureLevel.Level_10_1,
            };

            D3D11Device = new SharpDX.Direct3D11.Device(adapter, DeviceCreationFlags.BgraSupport, featureLevels);

            SwapChainDescription1 swapChainDescription = new SwapChainDescription1() {
                // Double buffer.
                BufferCount = 2,
                // BGRA 32bit pixel format.
                Format = Format.B8G8R8A8_UNorm,
                // Unlike in CoreWindow swap chains, the dimensions must be set.
                Height = (int)(ActualHeight),
                Width = (int)(ActualWidth),
                // Default multisampling.
                SampleDescription = new SampleDescription(1, 0),
                // In case the control is resized, stretch the swap chain accordingly.
                Scaling = Scaling.Stretch,
                // No support for stereo display.
                Stereo = false,
                // Sequential displaying for double buffering.
                SwapEffect = SwapEffect.FlipSequential,
                // This swapchain is going to be used as the back buffer.
                Usage = Usage.BackBuffer | Usage.RenderTargetOutput,
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
            
            backBuffer = SharpDX.Direct3D11.Resource.FromSwapChain<Texture2D>(swapChain, 0);

            var renderTarget = new RenderTargetView(D3D11Device, backBuffer);

            context = D3D11Device.ImmediateContext;
            // Clear Screen to Teel Blue.
            context.ClearRenderTargetView(renderTarget, new Color(0xFF887536));
            swapChain.Present(1, PresentFlags.None);
        }
    }
}
