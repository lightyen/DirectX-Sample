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
using MyGame;

namespace Sample
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged {

        public event PropertyChangedEventHandler PropertyChanged;
        private string msg;
        private Windows.Storage.Pickers.FileOpenPicker picker;

        public MainPage() {

            this.InitializeComponent();

            picker = new Windows.Storage.Pickers.FileOpenPicker {
                ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail,
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary
            };

            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".gif");
            picker.FileTypeFilter.Add(".bmp");

            picker.CommitButtonText = "送啦";

            this.DataContext = this;

            QRMessage = "https://github.com/lightyen/DirectX-Sample";
        }

        private void Button_Click(object sender, RoutedEventArgs e) {
            if (!string.IsNullOrEmpty(msg)) {
                if (App.Current.Resources[nameof(DirectXPanel)] is DirectXPanel xPanel) {
                    xPanel.UpdateQRCode(msg);
                }
            }
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

        private async void OpenFile_Click(object sender, RoutedEventArgs e) {

            if (await picker.PickSingleFileAsync() is Windows.Storage.StorageFile file) {
                if (App.Current.Resources[nameof(DirectXPanel)] is DirectXPanel xPanel) {
                    xPanel.UpdateFile(file);
                }
            }
        }
    }
}
