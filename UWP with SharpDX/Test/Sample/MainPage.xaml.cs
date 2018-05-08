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
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Media.Imaging;
using MyGame;

namespace Sample
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged {

        public event PropertyChangedEventHandler PropertyChanged;
        private string msg;
        private FileOpenPicker open_picker;
        private FileSavePicker save_picker;

        public MainPage() {

            this.InitializeComponent();

            open_picker = new FileOpenPicker {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.PicturesLibrary
            };

            open_picker.FileTypeFilter.Add(".jpg");
            open_picker.FileTypeFilter.Add(".jpeg");
            open_picker.FileTypeFilter.Add(".png");
            open_picker.FileTypeFilter.Add(".gif");
            open_picker.FileTypeFilter.Add(".bmp");
            open_picker.FileTypeFilter.Add(".dds");
            open_picker.CommitButtonText = "送啦";

            save_picker = new FileSavePicker {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary
            };

            save_picker.FileTypeChoices.Add("PNG", new List<string>() { ".png" });
            save_picker.FileTypeChoices.Add("JPEG", new List<string>() { ".jpg", ".jpeg", ".jpe", ".jfif" });
            save_picker.SuggestedFileName = "未命名";
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

            if (await open_picker.PickSingleFileAsync() is Windows.Storage.StorageFile file) {
                if (App.Current.Resources[nameof(DirectXPanel)] is DirectXPanel xPanel) {
                    xPanel.UpdateFile(file);
                }
            }
        }

        private async void SaveFile_Click(object sender, RoutedEventArgs e) {
            if (await save_picker.PickSaveFileAsync() is Windows.Storage.StorageFile file) {
                if (App.Current.Resources[nameof(DirectXPanel)] is DirectXPanel xPanel) {
                    if (xPanel.SaveFile(file).Failure) {
                        FlyoutBase.ShowAttachedFlyout(MyGrid);
                        Windows.System.Threading.ThreadPoolTimer.CreateTimer((timer) => {
                            var acti = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                                FlyoutBase.GetAttachedFlyout(MyGrid).Hide();
                            });
                        }, TimeSpan.FromMilliseconds(1600));
                        //var dialog = new Windows.UI.Popups.MessageDialog("Save Texture Failed");
                        //await dialog.ShowAsync();
                    }
                }
            }
        }
    }
}
