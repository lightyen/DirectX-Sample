using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using MyGame;
using QRCoder;

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

                        using (var qrGenerator = new QRCodeGenerator()) {
                            var data = qrGenerator.CreateQrCode("hello world", QRCodeGenerator.ECCLevel.Q);
                            BitmapByteQRCode qrCode = new BitmapByteQRCode(data);
                            byte[] qrCodeAsBitmapByteArr = qrCode.GetGraphic(30);
                            xPanel.Test(qrCodeAsBitmapByteArr);
                        }

                        await xPanel.Start();
                    });
                };

            Unloaded += (a, b) => {
                xPanel.Stop();
            };

            SizeChanged += (a, b) => {
                xPanel?.SetView((float)ActualWidth, (float)ActualHeight);
            };
        }
    }
}
