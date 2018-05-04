using System;
using System.IO;

namespace SharpDX.DirectXToolkit {

    public static partial class DirectXToolkit {
        
        /// <summary>
        /// 從檔案建立貼圖資源
        /// </summary>
        /// <param name="device">Direct3D裝置</param>
        /// <param name="file">目標檔案</param>
        /// <param name="texture">回傳的貼圖</param>
        /// <param name="textureView">回傳的貼圖資源</param>
        public static void CreateTexture(Direct3D11.Device device, Windows.Storage.StorageFile file, out Direct3D11.Resource texture, out Direct3D11.ShaderResourceView textureView) {
            CreateTexture(device, file, null, out texture, out textureView);
        }

        /// <summary>
        /// 從檔案建立貼圖資源 Auto Genarate MipMap
        /// </summary>
        /// <param name="device">Direct3D裝置</param>
        /// <param name="file">目標檔案</param>
        /// <param name="deviceContext">Direct3D裝置描述</param>
        public static void CreateTexture(Direct3D11.Device device, Windows.Storage.StorageFile file, Direct3D11.DeviceContext deviceContext, out Direct3D11.Resource texture, out Direct3D11.ShaderResourceView textureView) {
            texture = null;
            textureView = null;
            if (file != null) {
                var task = file.OpenAsync(Windows.Storage.FileAccessMode.Read).AsTask();
                using (var raStream = task.Result)
                using (var stream = raStream.AsStreamForRead()) {
                    CreateTexture(device, stream, deviceContext, out texture, out textureView);
                }
            }
        }

        /// <summary>
        /// 從<see cref="Stream"/>建立貼圖資源
        /// </summary>
        /// <param name="d3dDevice">Direct3D裝置</param>
        /// <param name="stream">目標串流</param>
        public static void CreateTexture(Direct3D11.Device d3dDevice, Stream stream, out Direct3D11.Resource texture, out Direct3D11.ShaderResourceView textureView) {
            CreateTexture(d3dDevice, stream, null, out texture, out textureView);
        }

        /// <summary>
        /// 從<see cref="Stream"/>建立貼圖資源 Auto Genarate MipMap
        /// </summary>
        /// <param name="d3dDevice">Direct3D裝置</param>
        /// <param name="stream">目標串流</param>
        /// <param name="deviceContext">Direct3D裝置描述</param>
        public static void CreateTexture(Direct3D11.Device d3dDevice, Stream stream, Direct3D11.DeviceContext deviceContext, out Direct3D11.Resource texture, out Direct3D11.ShaderResourceView textureView) {
            texture = null;
            textureView = null;
            if (stream.CanRead) {
                if (stream.Length < 104857600 && stream.Length >= 4) {
                    var temp = new byte[4];
                    stream.Read(temp, 0, 4);
                    stream.Seek(0, SeekOrigin.Begin);
                    if (temp[0] == 0x44 && temp[1] == 0x44 && temp[2] == 0x53 && temp[3] == 0x20) {
                        CreateDDSTextureFromStream(d3dDevice, stream, deviceContext, out texture, out textureView);
                    } else {
                        CreateWICTextureFromStream(d3dDevice, stream, deviceContext, out texture, out textureView);
                    }
                }
            }
        }
    }
}
