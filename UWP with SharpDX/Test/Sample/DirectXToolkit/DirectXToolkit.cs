using System;
using System.IO;
using SharpDX.Direct3D11;

namespace DirectXToolkit {

    public static partial class DirectXTK {

        /// <summary>
        /// 從檔案建立貼圖資源 Auto Genarate MipMap
        /// </summary>
        /// <param name="device">Direct3D裝置</param>
        /// <param name="file">目標檔案</param>
        /// <param name="texture">回傳的貼圖</param>
        /// <param name="textureView">回傳的貼圖資源</param>
        /// <param name="d3dContext">If a Direct3D 11 device context is provided and the current device supports it for the given pixel format, it will auto-generate mipmaps.</param>
        public static void CreateTexture(Device device, Windows.Storage.StorageFile file, out Resource texture, out ShaderResourceView textureView, DeviceContext d3dContext = null) {
            texture = null;
            textureView = null;
            if (file != null) {
                var task = file.OpenAsync(Windows.Storage.FileAccessMode.Read).AsTask();
                using (var raStream = task.Result)
                using (var stream = raStream.AsStreamForRead()) {
                    CreateTexture(device, stream, out texture, out textureView, d3dContext);
                }
            }
        }

        /// <summary>
        /// 從<see cref="Stream"/>建立貼圖資源 Auto Genarate MipMap
        /// </summary>
        /// <param name="d3dDevice">Direct3D裝置</param>
        /// <param name="stream">目標串流</param>
        /// <param name="d3dContext">If a Direct3D 11 device context is provided and the current device supports it for the given pixel format, it will auto-generate mipmaps.</param>
        public static void CreateTexture(Device d3dDevice, Stream stream, out Resource texture, out ShaderResourceView textureView, DeviceContext d3dContext = null) {
            texture = null;
            textureView = null;
            if (stream.CanRead) {
                if (stream.Length < 104857600 && stream.Length >= 4) {
                    var temp = new byte[4];
                    stream.Read(temp, 0, 4);
                    stream.Seek(0, SeekOrigin.Begin);
                    if (temp[0] == 0x44 && temp[1] == 0x44 && temp[2] == 0x53 && temp[3] == 0x20) {
                        CreateDDSTextureFromStream(d3dDevice, stream, out texture, out textureView, d3dContext);
                    } else {
                        CreateWICTextureFromStream(d3dDevice, stream, out texture, out textureView, d3dContext);
                    }
                }
            }
        }
    }
}
