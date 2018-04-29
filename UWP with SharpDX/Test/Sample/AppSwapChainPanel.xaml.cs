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

        private DirectXPanel xPanel;

        public AppSwapChainPanel() {
            InitializeComponent();

            Loaded += (a, b) => {
                xPanel = new DirectXPanel(new SharpDX.Size2(1920, 1080), this);
                xPanel.SetView((float)ActualWidth, (float)ActualHeight);

                Task.Run(async () => {
                    await xPanel.Start();
                });

                var window = Windows.UI.Core.CoreWindow.GetForCurrentThread();
                window.KeyDown += async (o, e) => {
                    if (e.VirtualKey == Windows.System.VirtualKey.Escape) {
                        await xPanel.Stop();
                        Windows.UI.Xaml.Application.Current.Exit();
                    }
                };
            };

            SizeChanged += (a, b) => {
                xPanel?.SetView((float)ActualWidth, (float)ActualHeight);
            };
        }

        public void Update(string msg) {
            xPanel.UpdateQRCode(msg);
        }

        public void Update(Windows.Storage.StorageFile file) {
            xPanel.UpdateFile(file);
        }
    }

    
}
