using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using MyGame;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DirectXToolkit;

namespace Sample {
    /// <summary>
    /// 可以在本身使用或巡覽至框架內的空白頁面。
    /// </summary>
    public sealed partial class AppSwapChainPanel : SwapChainPanel, INotifyPropertyChanged {

        private bool initialized = false;
        public bool DirectXEnabled {
            get { return initialized; } private set { initialized = value; NotifyPropertyChanged(nameof(DirectXEnabled)); }
        }

        public AppSwapChainPanel() {
            InitializeComponent();

            Loaded += (a, b) => {

                

                if (App.Current.Resources[nameof(DirectXPanel)] is DirectXPanel xPanel) {

                    var window = Windows.UI.Core.CoreWindow.GetForCurrentThread();
                    window.KeyDown += async (o, e) => {
                        if (e.VirtualKey == Windows.System.VirtualKey.Escape) {
                            await xPanel.Stop();
                            Windows.UI.Xaml.Application.Current.Exit();
                        }
                    };

                    var os = Environment.OSVersion;
                    if (os.Platform != PlatformID.Win32NT) return;
                    if (os.Version < new Version("6.1.7601.0")) return;

                    try {
                        xPanel.CreateSwapChain(new SharpDX.Size2(1920, 1080), this);
                        xPanel.SetView((float)ActualWidth, (float)ActualHeight);
                        DirectXEnabled = true;
                    } catch (Exception) {

                    }

                    if (initialized) {
                        window.PointerPressed += (o, e) => {
                            this.Start();
                        };

                        SizeChanged += (o, e) => {
                            xPanel?.SetView((float)ActualWidth, (float)ActualHeight);
                        };
                    }
                }
            };
        }

        public void Start() {
            if (!initialized) return;
            if (App.Current.Resources[nameof(DirectXPanel)] is DirectXPanel xPanel) {
                if (!xPanel.Running)
                Task.Run(async () => {
                    await xPanel.Start();
                });
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName]string propertyName = "") {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }


}
