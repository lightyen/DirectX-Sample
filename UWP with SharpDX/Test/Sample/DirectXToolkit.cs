using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpDX.Direct3D11;
using SharpDX.Direct3D;
using SharpDX.DXGI;

namespace SharpDX.WIC {

    public class DirectXToolkit {

        private static ImagingFactory imgFactory;
        private static ImagingFactory ImagingFactory {
            get {
                if (imgFactory == null) {
                    imgFactory = new ImagingFactory2();
                }
                return imgFactory;
            }
        }

        /// <summary>
        /// 從檔案建立貼圖資源
        /// </summary>
        /// <param name="file"></param>
        /// <param name="device"></param>
        public static Result CreateTextureFromFile(Windows.Storage.StorageFile file, Direct3D11.Device device, out Direct3D11.Resource texture, out Direct3D11.ShaderResourceView textureView) {
            Result result = Result.Fail;
            texture = null;
            textureView = null;
            if (file != null) {
                var task = file.OpenAsync(Windows.Storage.FileAccessMode.Read).AsTask();
                using (var raStream = task.Result)
                using (var stream = raStream.AsStreamForRead()) {
                    result = CreateTextureFromStream(stream, device, out texture, out textureView);
                }
            }
            return result;
        }

        /// <summary>
        /// 從串流<see cref="System.IO.Stream"/>建立貼圖資源
        /// </summary>
        /// <param name="stream">串流</param>
        /// <param name="device">D3D Device</param>
        public static Result CreateTextureFromStream(Stream stream, Direct3D11.Device device, out Direct3D11.Resource texture, out Direct3D11.ShaderResourceView textureView) {
            texture = null;
            textureView = null;
            Result result = Result.Fail;
            Guid containerFormatGuid;
            if (stream.CanRead) {
                if (stream.Length < 104857600 && stream.Length >= 4) {
                    var temp = new byte[4];
                    stream.Read(temp, 0, 4);
                    stream.Seek(0, SeekOrigin.Begin);
                    if (temp[0] == 0xFF && temp[1] == 0xD8 && temp[2] == 0xFF && temp[3] == 0xE0) {
                        containerFormatGuid = ContainerFormatGuids.Jpeg;
                    } else if (temp[0] == 0x89 && temp[1] == 0x50 && temp[2] == 0x4E && temp[3] == 0x47) {
                        containerFormatGuid = ContainerFormatGuids.Png;
                    } else if (temp[0] == 0x42 && temp[1] == 0x4D) {
                        containerFormatGuid = ContainerFormatGuids.Bmp;
                    } else if (temp[0] == 0x47 && temp[1] == 0x49 && temp[2] == 0x46 && temp[3] == 0x38) {
                        containerFormatGuid = ContainerFormatGuids.Gif;
                    }
                    else {
                        return Result.Fail;
                    }
                }

                using (var decoder = new BitmapDecoder(ImagingFactory, containerFormatGuid))
                using (var wicstream = new WICStream(ImagingFactory, stream)) {
                    try {
                        decoder.Initialize(wicstream, DecodeOptions.CacheOnDemand);
                        using (var frame = decoder.GetFrame(0)) {
                            result = CreateTextureFromWIC(device, frame,
                                    0, ResourceUsage.Default, BindFlags.ShaderResource, CpuAccessFlags.None, ResourceOptionFlags.None, LoadFlags.Default,
                                    out texture, out textureView);
                        }
                    } catch (SharpDXException e) {
                        System.Diagnostics.Debug.WriteLine(e.ToString());
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 從WIC Frame建立貼圖資源
        /// </summary>
        private static Result CreateTextureFromWIC(Direct3D11.Device device, BitmapFrameDecode frame, int maxsize, ResourceUsage usage, BindFlags bind, CpuAccessFlags cpuAccess, ResourceOptionFlags option, LoadFlags load, out Direct3D11.Resource texture, out ShaderResourceView textureView) {

            texture = null;
            textureView = null;

            if (frame.Size.Width <= 0 || frame.Size.Height <= 0) return Result.InvalidArg;

            if (maxsize == 0) {
                switch (device.FeatureLevel) {
                    case FeatureLevel.Level_9_1:
                    case FeatureLevel.Level_9_2:
                        maxsize = 2048 /*D3D_FL9_1_REQ_TEXTURE2D_U_OR_V_DIMENSION*/;
                        break;
                    case FeatureLevel.Level_9_3:
                        maxsize = 4096 /*D3D_FL9_3_REQ_TEXTURE2D_U_OR_V_DIMENSION*/;
                        break;
                    case FeatureLevel.Level_10_0:
                    case FeatureLevel.Level_10_1:
                        maxsize = 8192 /*D3D10_REQ_TEXTURE2D_U_OR_V_DIMENSION*/;
                        break;
                    default:
                        maxsize = Direct3D11.Resource.MaximumTexture2DSize; /*D3D11_REQ_TEXTURE2D_U_OR_V_DIMENSION*/
                        break;
                }
            }

            Size2 frameSize = frame.Size;
            Size2 targetSize;

            if (frameSize.Width > maxsize || frameSize.Height > maxsize) {
                double ratio = Convert.ToDouble(frameSize.Height) / Convert.ToDouble(frameSize.Width);
                if (frameSize.Width > frameSize.Height) {
                    targetSize.Width = maxsize;
                    targetSize.Height = Math.Max(1, Convert.ToInt32(maxsize * ratio));
                } else {
                    targetSize.Height = maxsize;
                    targetSize.Width = Math.Max(1, Convert.ToInt32(maxsize / ratio));
                }
            } else {
                targetSize = frameSize;
            }

            // Determine format
            Guid sourceFormat = frame.PixelFormat;
            Guid targetFormat = sourceFormat;
            Format format = sourceFormat.ConvertToDXGIFormat();
            int bpp = 0;

            if (format == Format.Unknown) {
                if (sourceFormat == PixelFormat.Format96bppRGBFixedPoint) {
                    targetFormat = PixelFormat.Format96bppRGBFloat;
                    format = Format.R32G32B32_Float;
                    bpp = 96;
                } else {
                    targetFormat = sourceFormat.ConvertToNearest();
                    format = targetFormat.ConvertToDXGIFormat();
                    bpp = PixelFormat.GetBitsPerPixel(targetFormat);
                }
                if (format == Format.Unknown)
                    return Result.GetResultFromWin32Error(unchecked((int)0x80070032));
            } else {
                bpp = PixelFormat.GetBitsPerPixel(sourceFormat);
            }

            //if (format == Format.R32G32B32_Float && deviceContext != null && textureView != null) {
            //    // Special case test for optional device support for autogen mipchains for R32G32B32_FLOAT 

            //    var formatSupport = device.CheckFormatSupport(Format.R32G32B32_Float);

            //    if (!formatSupport.HasFlag(FormatSupport.MipAutogen)) {
            //        targetFormat = PixelFormat.Format128bppRGBAFloat;
            //        format = Format.R32G32B32A32_Float;
            //        bpp = 128;
            //    }
            //}

            if (bpp == 0) return Result.Fail;

            if (load.HasFlag(LoadFlags.ForceSrgb)) {
                format = format.MakeSRgb();
            } else if (!load.HasFlag(LoadFlags.ignoreSrgb)) {
                try {
                    var metareader = frame.MetadataQueryReader;
                    var containerFormat = metareader.ContainerFormat;

                    // Check for sRGB colorspace metadata
                    bool sRGB = false;
                    if (metareader.GetMetadataByName("/sRGB/RenderingIntent") != null) {
                        sRGB = true;
                    } else if (metareader.GetMetadataByName("System.Image.ColorSpace") != null) {
                        sRGB = true;
                    }

                    if (sRGB) {
                        format = format.MakeSRgb();
                    }
                } catch (SharpDXException e) {
                    System.Diagnostics.Debug.WriteLine($"GetMetadataByName: {e.Message}");
                }
            }

            var support = device.CheckFormatSupport(format);
            if (support.HasFlag(FormatSupport.Texture2D)) {
                targetFormat = PixelFormat.Format32bppRGBA;
                format = Format.R8G8B8A8_UNorm;
                bpp = 32;
            }

            // 開始轉換.....

            int stride = (targetSize.Width * bpp + 7) / 8;
            int imageSize = stride * targetSize.Height;
            IntPtr temp = System.Runtime.InteropServices.Marshal.AllocCoTaskMem(imageSize);


            if (sourceFormat == targetFormat && frameSize == targetSize) { // 不需要格式轉換 且 不需要改變大小
                frame.CopyPixels(stride, new DataPointer(temp, imageSize));
            } else if (frameSize == targetSize) { // 需要格式轉換
                using (var factory = new ImagingFactory2())
                using (var coverter = new FormatConverter(factory)) {
                    if (coverter.CanConvert(sourceFormat, targetFormat)) {
                        coverter.Initialize(frame, targetFormat, BitmapDitherType.ErrorDiffusion, null, 0, BitmapPaletteType.MedianCut);
                        coverter.CopyPixels(stride, new DataPointer(temp, imageSize));
                    } else {
                        return Result.UnexpectedFailure;
                    }
                }
            } else if (sourceFormat == targetFormat) { // 需要改變大小
                using (var factory = new ImagingFactory2())
                using (var scaler = new BitmapScaler(factory)) {
                    scaler.Initialize(frame, targetSize.Width, targetSize.Height, BitmapInterpolationMode.Fant);
                    var pfScaler = scaler.PixelFormat;
                    if (targetFormat == pfScaler) {
                        scaler.CopyPixels(stride, new DataPointer(temp, imageSize));
                    }
                }
            } else { // 需要格式轉換 且 需要改變大小
                using (var factory = new ImagingFactory2())
                using (var scaler = new BitmapScaler(factory))
                using (var coverter = new FormatConverter(factory)) {
                    scaler.Initialize(frame, targetSize.Width, targetSize.Height, BitmapInterpolationMode.Fant);
                    var pfScaler = scaler.PixelFormat;

                    if (coverter.CanConvert(pfScaler, targetFormat)) {

                        coverter.Initialize(scaler, targetFormat, BitmapDitherType.ErrorDiffusion, null, 0, BitmapPaletteType.MedianCut);
                        coverter.CopyPixels(stride, new DataPointer(temp, imageSize));
                    } else {
                        return Result.UnexpectedFailure;
                    }

                }
            }

            bool autogen = false;
            //if (deviceContext != null && textureView != null) // Must have context and shader-view to auto generate mipmaps
            //{
            //    var formatSupport = device.CheckFormatSupport(format);
            //    if (formatSupport.HasFlag(FormatSupport.MipAutogen)) {
            //        autogen = true;
            //    }
            //}

            var texture2DDescription = new Texture2DDescription() {
                Width = targetSize.Width,
                Height = targetSize.Height,
                MipLevels = autogen ? 0 : 1,
                ArraySize = 1,
                Format = format,
                SampleDescription = new SampleDescription(1, 0),
                Usage = usage,
                CpuAccessFlags = cpuAccess,
            };

            if (autogen) {
                texture2DDescription.BindFlags = bind | BindFlags.RenderTarget;
                texture2DDescription.OptionFlags = option | ResourceOptionFlags.GenerateMipMaps;
            } else {
                texture2DDescription.BindFlags = bind;
                texture2DDescription.OptionFlags = option;
            }

            // 建立Texture2D !!!
            texture = new Texture2D(device, texture2DDescription, new DataBox[] { new DataBox(temp, stride, imageSize) });

            System.Runtime.InteropServices.Marshal.FreeCoTaskMem(temp);

            var SRVDesc = new ShaderResourceViewDescription() {
                Format = format,
                Dimension = ShaderResourceViewDimension.Texture2D,
                Texture2D = new ShaderResourceViewDescription.Texture2DResource() { MipLevels = autogen ? -1 : 1 },
            };

            textureView = new ShaderResourceView(device, texture, SRVDesc);

            return Result.Ok;
        }

    }
}
