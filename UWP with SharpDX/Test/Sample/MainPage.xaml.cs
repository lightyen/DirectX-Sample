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
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using QRCoder;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace Sample
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged {

        public event PropertyChangedEventHandler PropertyChanged;
        private string msg;

        public MainPage() {

            this.InitializeComponent();

            this.DataContext = this;

            QRMessage = "https://github.com/lightyen/DirectX-Sample";

            var window = CoreWindow.GetForCurrentThread();
            window.KeyDown += (a, b) => {
                if (b.VirtualKey == Windows.System.VirtualKey.Escape) {
                    Application.Current.Exit();
                }
            };
        }

        private void Button_Click(object sender, RoutedEventArgs e) {
            if (!string.IsNullOrEmpty(msg))
                AppSwapChainPanel.Update(msg);
        }

        public string QRMessage {
            get {
                return msg;
            }
            set {
                msg = value;
                NotifyPropertyChanged(nameof(QRMessage));
            }
        }

        private void NotifyPropertyChanged([CallerMemberName]string propertyName = "") {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
