using System;
using System.IO;
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
        /// 從記憶體建立貼圖資源
        /// </summary>
        /// <param name="device"></param>
        /// <param name="data"></param>
        /// <param name="containerFormatGuid"></param>
        /// <param name="texture"></param>
        /// <param name="textureView"></param>
        /// <returns></returns>
        public static Result CreateTextureFromData(byte[] data, Guid containerFormatGuid, Direct3D11.Device device, out Direct3D11.Resource texture, out Direct3D11.ShaderResourceView textureView) {
            Result result = Result.Fail;
            using (var memoryStream = new MemoryStream(data, false))
            {
                result = CreateTextureFromMemory(memoryStream, containerFormatGuid, device, out texture, out textureView);
            }
            return result;
        }

        private static Result CreateTextureFromMemory(MemoryStream stream, Guid containerFormatGuid, Direct3D11.Device device, out Direct3D11.Resource texture, out Direct3D11.ShaderResourceView textureView) {
            return CreateTextureFromStream(stream, containerFormatGuid, device, out texture, out textureView);
        }

        private static Result CreateTextureFromStream(Stream stream, Guid containerFormatGuid, Direct3D11.Device device, out Direct3D11.Resource texture, out Direct3D11.ShaderResourceView textureView) {
            texture = null;
            textureView = null;
            Result result = Result.Fail;

            if (stream.CanRead) {
                using (var decoder = new BitmapDecoder(ImagingFactory, containerFormatGuid))
                using (var wicstream = new WICStream(ImagingFactory, stream)) {

                    decoder.Initialize(wicstream, DecodeOptions.CacheOnDemand);

                    using (var frame = decoder.GetFrame(0)) {
                        result = CreateTextureFromWIC(device, frame,
                                0, ResourceUsage.Default, BindFlags.ShaderResource, CpuAccessFlags.None, ResourceOptionFlags.None, LoadFlags.Default,
                                out texture, out textureView);
                    }
                }
            }

            return result;
        }

        private static Task<Result> CreateTextureFromFile(Windows.Storage.StorageFile file, Direct3D11.Device device, out Direct3D11.Resource texture, out Direct3D11.ShaderResourceView textureView) {
            return InternalCreateTextureFromFile(file, device, out texture, out textureView);
        }

        private static async Task<Result> InternalCreateTextureFromFile(Windows.Storage.StorageFile file, Direct3D11.Device device, out Direct3D11.Resource texture, out Direct3D11.ShaderResourceView textureView) {
            Result result = Result.Fail;
            if (file != null) {

                using (var raStream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read))
                using (var stream = raStream.AsStreamForRead()) {
                    if (stream.Length < 104857600) {
                        var ext = Path.GetExtension(file.Name);
                        switch (ext) {
                            case ".dds":
                                result = CreateTextureFromStream(stream, ContainerFormatGuids.Dds, device, out texture, out textureView);
                                break;
                            case ".png":
                                result = CreateTextureFromStream(stream, ContainerFormatGuids.Png, device, out texture, out textureView);
                                break;
                            case ".jpg":
                            case ".jpeg":
                                result = CreateTextureFromStream(stream, ContainerFormatGuids.Jpeg, device, out texture, out textureView);
                                break;
                            case ".bmp":
                                result = CreateTextureFromStream(stream, ContainerFormatGuids.Bmp, device, out texture, out textureView);
                                break;
                            default:
                                result = Result.Fail;
                                break;
                        }
                    }
                }
            }
            return result;
        }

        private static Result CreateTextureFromFile(byte[] data, string fileExtension, Direct3D11.Device device, out Direct3D11.Resource texture, out Direct3D11.ShaderResourceView textureView) {
            texture = null;
            textureView = null;
            switch (fileExtension) {
                case ".dds":
                    return CreateTextureFromData(data, ContainerFormatGuids.Dds, device, out texture, out textureView);
                case ".png":
                    return CreateTextureFromData(data, ContainerFormatGuids.Png, device, out texture, out textureView);
                case ".jpg":
                case ".jpeg":
                    return CreateTextureFromData(data, ContainerFormatGuids.Jpeg, device, out texture, out textureView);
                case ".bmp":
                    return CreateTextureFromData(data, ContainerFormatGuids.Bmp, device, out texture, out textureView);
                default:
                    return Result.False;
            }
        }

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

                var metareader = frame.MetadataQueryReader;
                var containerFormat = metareader.ContainerFormat;

                try {
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
