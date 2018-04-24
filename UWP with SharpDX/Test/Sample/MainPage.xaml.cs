using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Core;

using System.Threading.Tasks;
using QRCoder;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace Sample
{
    public sealed partial class MainPage : Page {
        public MainPage() {
            this.InitializeComponent();

            var window = CoreWindow.GetForCurrentThread();
            window.KeyDown += (a, b) => {
                if (b.VirtualKey == Windows.System.VirtualKey.Escape) {
                    Application.Current.Exit();
                }
            };
        }

        private async void Button_Click(object sender, RoutedEventArgs e) {
            using (var qrGenerator = new QRCodeGenerator()) {
                var data = qrGenerator.CreateQrCode("hello world", QRCodeGenerator.ECCLevel.Q);
                PngByteQRCode qrCode = new PngByteQRCode(data);
                byte[] qrCodeAsPngByteArr = qrCode.GetGraphic(30);
                
                using (var stream = new InMemoryRandomAccessStream()) {
                    using (var writer = new DataWriter(stream.GetOutputStreamAt(0))) {
                        writer.WriteBytes(qrCodeAsPngByteArr);
                        await writer.StoreAsync();
                    }
                    var image = new BitmapImage();
                    await image.SetSourceAsync(stream);
                    SurfaceImageSource dd = new SurfaceImageSource(400, 400);

                    TestImage.Source = image;
                }
            }
        }
    }
}
