using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using MyGame;

namespace Sample {
    /// <summary>
    /// 可以在本身使用或巡覽至框架內的空白頁面。
    /// </summary>
    public sealed partial class AppSwapChainPanel : SwapChainPanel {

        public AppSwapChainPanel() {
            InitializeComponent();

            Loaded += (a, b) => {

                if (App.Current.Resources[nameof(DirectXPanel)] is DirectXPanel xPanel) {
                    //xPanel.Initialize(new SharpDX.Size2(1920, 1080), this);
                    //xPanel.SetView((float)ActualWidth, (float)ActualHeight);
                    //Task.Run(async () => {
                    //    await xPanel.Start();
                    //});
                }
            };


            var window = Windows.UI.Core.CoreWindow.GetForCurrentThread();
            window.KeyDown += async (o, e) => {
                if (e.VirtualKey == Windows.System.VirtualKey.Escape) {
                    if (App.Current.Resources[nameof(DirectXPanel)] is DirectXPanel xPanel) {
                        await xPanel.Stop();
                    } 
                    Windows.UI.Xaml.Application.Current.Exit();
                }
            };

            SizeChanged += (o, e) => {
                if (App.Current.Resources[nameof(DirectXPanel)] is DirectXPanel xPanel) {
                    xPanel?.SetView((float)ActualWidth, (float)ActualHeight);
                }
            };
        }
    }

    
}
