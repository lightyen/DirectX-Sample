using System;
using System.IO;
using SharpDX;
using DXGI = SharpDX.DXGI;
using SharpDX.Direct3D11;
using SharpDX.Direct3D;
using SharpDX.WIC;

namespace DirectXToolkit {

    public static partial class DirectXTK {

        private static ImagingFactory imgFactory;
        private static ImagingFactory ImagingFactory {
            get {
                if (imgFactory == null) {
                    imgFactory = new ImagingFactory2();
                }
                return imgFactory;
            }
        }

        private static bool WIC2 {
            get {
                if (imgFactory.QueryInterface<ImagingFactory2>() is ImagingFactory2 fac2) return true;
                else return false;
            }
        }

        /// <summary>
        /// 從串流<see cref="System.IO.Stream"/>建立貼圖資源(非DDS)
        /// </summary>
        /// <param name="stream">串流</param>
        /// <param name="device">D3D Device</param>
        /// <param name="d3dContext">If a Direct3D 11 device context is provided and the current device supports it for the given pixel format, it will auto-generate mipmaps.</param>
        public static void CreateWICTextureFromStream(Device device, Stream stream, out Resource texture, out ShaderResourceView textureView, DeviceContext d3dContext = null) {
            texture = null;
            textureView = null;
            Guid containerFormatGuid;
            if (stream.CanRead) {
                if (stream.Length < 104857600 && stream.Length >= 8) {
                    var temp = new byte[8];
                    stream.Read(temp, 0, 8);
                    stream.Seek(0, SeekOrigin.Begin);
                    // https://en.wikipedia.org/wiki/List_of_file_signatures
                    if (temp[0] == 0xFF && temp[1] == 0xD8 && temp[2] == 0xFF) {
                        containerFormatGuid = ContainerFormatGuids.Jpeg;
                    } else if (temp[0] == 0x89 && temp[1] == 0x50 && temp[2] == 0x4E && temp[3] == 0x47 && temp[4] == 0x0D && temp[5] == 0x0A && temp[6] == 0x1A && temp[7] == 0x0A) {
                        containerFormatGuid = ContainerFormatGuids.Png;
                    } else if (temp[0] == 0x42 && temp[1] == 0x4D) {
                        containerFormatGuid = ContainerFormatGuids.Bmp;
                    } else if (temp[0] == 0x47 && temp[1] == 0x49 && temp[2] == 0x46 && temp[3] == 0x38 && (temp[4] == 0x37 || temp[4] == 0x39) && temp[5] == 0x61) {
                        containerFormatGuid = ContainerFormatGuids.Gif;
                    } else {
                        return;
                    }
                }

                using (var decoder = new BitmapDecoder(ImagingFactory, containerFormatGuid))
                using (var wicstream = new WICStream(ImagingFactory, stream)) {
                    try {
                        decoder.Initialize(wicstream, DecodeOptions.CacheOnDemand);
                        using (var frame = decoder.GetFrame(0)) {
                            CreateWICTexture(device, d3dContext, frame,
                                    0, ResourceUsage.Default, BindFlags.ShaderResource, CpuAccessFlags.None, ResourceOptionFlags.None, LoadFlags.Default,
                                    out texture, out textureView);
                        }
                    } catch (SharpDXException e) {
                        System.Diagnostics.Debug.WriteLine(e.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// 從WIC Frame建立貼圖資源(非DDS)
        /// </summary>
        /// <param name="d3dContext">If a Direct3D 11 device context is provided and the current device supports it for the given pixel format, it will auto-generate mipmaps.</param>
        private static Result CreateWICTexture(Device device, DeviceContext d3dContext, BitmapFrameDecode frame, int maxsize, ResourceUsage usage, BindFlags bind, CpuAccessFlags cpuAccess, ResourceOptionFlags option, LoadFlags load, out Resource texture, out ShaderResourceView textureView) {

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
                        maxsize = Resource.MaximumTexture2DSize; /*D3D11_REQ_TEXTURE2D_U_OR_V_DIMENSION*/
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

            #region Determine format
            Guid sourceFormat = frame.PixelFormat;
            Guid targetFormat = sourceFormat;
            DXGI.Format format = sourceFormat.ConvertWICToDXGIFormat();
            int bpp = 0;

            if (format == DXGI.Format.Unknown) {
                if (sourceFormat == PixelFormat.Format96bppRGBFixedPoint) {
                    if (WIC2) {
                        targetFormat = PixelFormat.Format96bppRGBFloat;
                        format = DXGI.Format.R32G32B32_Float;
                        bpp = 96;
                    } else {
                        targetFormat = PixelFormat.Format128bppRGBAFloat;
                        format = DXGI.Format.R32G32B32A32_Float;
                        bpp = 128;
                    }
                } else {
                    targetFormat = sourceFormat.ConvertToNearest();
                    format = targetFormat.ConvertWICToDXGIFormat();
                    bpp = PixelFormat.GetBitsPerPixel(targetFormat);
                }
                if (format == DXGI.Format.Unknown)
                    return Result.GetResultFromWin32Error(unchecked((int)0x80070032));
            } else {
                bpp = PixelFormat.GetBitsPerPixel(sourceFormat);
            }

            if (format == DXGI.Format.R32G32B32_Float && d3dContext != null) {
                // Special case test for optional device support for autogen mipchains for R32G32B32_FLOAT
                var formatSupport = device.CheckFormatSupport(format);
                if (!formatSupport.HasFlag(FormatSupport.MipAutogen)) {
                    targetFormat = PixelFormat.Format128bppRGBAFloat;
                    format = DXGI.Format.R32G32B32A32_Float;
                    bpp = 128;
                }
            }
            if (bpp == 0) return Result.Fail;

            if (load.HasFlag(LoadFlags.ForceSrgb)) {
                format = format.MakeSRgb();
            } else if (!load.HasFlag(LoadFlags.ignoreSrgb)) {
                bool sRGB = false;
                try {
                    var metareader = frame.MetadataQueryReader;
                    var containerFormat = metareader.ContainerFormat;

                    if (containerFormat == ContainerFormatGuids.Png) {
                        // Check for sRGB chunk
                        if (metareader.TryGetMetadataByName("/sRGB/RenderingIntent", out var value) == Result.Ok) {
                            sRGB = true;
                        }
                    } else if (metareader.TryGetMetadataByName("System.Image.ColorSpace", out var value) == Result.Ok) {
                        sRGB = true;
                    }

                    if (sRGB) {
                        format = format.MakeSRgb();
                    }
                } catch (SharpDXException) {
                    // BMP, ICO are not supported.
                }
            }

            // Verify our target format is supported by the current device
            var support = device.CheckFormatSupport(format);
            if (!support.HasFlag(FormatSupport.Texture2D)) {
                targetFormat = PixelFormat.Format32bppRGBA;
                format = DXGI.Format.R8G8B8A8_UNorm;
                bpp = 32;
            }
            #endregion


            int stride = (targetSize.Width * bpp + 7) / 8; // round
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

            var autogen = false;

            if (d3dContext != null) {
                var formatSupport = device.CheckFormatSupport(format);
                if (formatSupport.HasFlag(FormatSupport.MipAutogen)) {
                    autogen = true;
                }
            }

            var texture2DDescription = new Texture2DDescription() {
                Width = targetSize.Width,
                Height = targetSize.Height,
                MipLevels = autogen ? 0 : 1,
                ArraySize = 1,
                Format = format,
                SampleDescription = new DXGI.SampleDescription(1, 0),
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

            Result result = Result.Ok;

            // 建立Texture2D !!!
            try {
                if (autogen) {
                    texture = new Texture2D(device, texture2DDescription);
                } else {
                    texture = new Texture2D(device, texture2DDescription, new DataBox[] { new DataBox(temp, stride, imageSize) });
                }
            } catch (SharpDXException e) {
                System.Diagnostics.Debug.WriteLine(e.ToString());
                result = Result.Fail;
            }

            if (result.Success) {

                var SRVDesc = new ShaderResourceViewDescription() {
                    Format = format,
                    Dimension = ShaderResourceViewDimension.Texture2D,
                    Texture2D = new ShaderResourceViewDescription.Texture2DResource() { MipLevels = autogen ? -1 : 1 },
                };

                try {
                    textureView = new ShaderResourceView(device, texture, SRVDesc);
                    if (autogen) {
                        DataBox data = new DataBox(temp, stride, imageSize);
                        d3dContext.UpdateSubresource(data, texture);
                        d3dContext.GenerateMips(textureView);
                    }
                } catch (Exception e) {
                    System.Diagnostics.Debug.WriteLine(e.ToString());
                    Utilities.Dispose(ref texture);
                    result = Result.Fail;
                }
            }

            // 釋放 Unmanaged 資源
            System.Runtime.InteropServices.Marshal.FreeCoTaskMem(temp);

            return result;
        }

        public static Result SaveTextureToStream(Device d3dDevice, Resource source, Stream stream) {
            return SaveTextureToStream(d3dDevice, source, stream, ContainerFormatGuids.Png, Guid.Empty);
        }

        public static Result SaveTextureToStream(Device d3dDevice, Resource source, Stream stream, Guid containerFormat, Guid targetFormatGuid) {

            Result result = Result.Fail;
            if (source == null || d3dDevice == null || stream == null) return Result.InvalidArg;
            result = CreateStagingTexture(d3dDevice.ImmediateContext, source, out Texture2DDescription desc, out Texture2D staging);
            if (!result.Success) return result;

            Guid sourceFormat = desc.Format.ConvertDXGIToWICFormat();
            if (sourceFormat == Guid.Empty) return Result.InvalidArg;

            if (ImagingFactory == null) return Result.NoInterface;

            Guid targetFormat = targetFormatGuid;
            if (targetFormat == Guid.Empty) {
                switch (desc.Format) {
                    case DXGI.Format.R32G32B32A32_Float:
                    case DXGI.Format.R16G16B16A16_Float:
                        if (WIC2) {
                            targetFormat = PixelFormat.Format96bppRGBFloat;
                        } else {
                            targetFormat = PixelFormat.Format24bppBGR;
                        }
                        break;
                    case DXGI.Format.R16G16B16A16_UNorm:
                        targetFormat = PixelFormat.Format48bppBGR;
                        break;
                    case DXGI.Format.B5G5R5A1_UNorm:
                        targetFormat = PixelFormat.Format16bppBGR555;
                        break;
                    case DXGI.Format.B5G6R5_UNorm:
                        targetFormat = PixelFormat.Format16bppBGR565;
                        break;
                    case DXGI.Format.R32_Float:
                    case DXGI.Format.R16_Float:
                    case DXGI.Format.R16_UNorm:
                    case DXGI.Format.R8_UNorm:
                    case DXGI.Format.A8_UNorm:
                        targetFormat = PixelFormat.Format8bppGray;
                        break;
                    default:
                        targetFormat = PixelFormat.Format24bppBGR;
                        break;
                }
            }

            if (targetFormatGuid != Guid.Empty && targetFormatGuid != targetFormat) return result;

            try {
                // Create a new file
                if (stream.CanWrite) {
                    using (BitmapEncoder encoder = new BitmapEncoder(ImagingFactory, containerFormat)) {
                        encoder.Initialize(stream);

                        using (BitmapFrameEncode frameEncode = new BitmapFrameEncode(encoder)) {
                            frameEncode.Initialize();
                            frameEncode.SetSize(desc.Width, desc.Height);
                            frameEncode.SetResolution(72.0, 72.0);

                            
                            frameEncode.SetPixelFormat(ref targetFormat);

                            if (targetFormatGuid == Guid.Empty || targetFormat == targetFormatGuid) {
                                int subresource = 0;

                                // 讓CPU存取顯存貼圖
                                // MapSubresource 在 deferred context 下不支援 MapMode.Read
                                DataBox db = d3dDevice.ImmediateContext.MapSubresource(staging, subresource, MapMode.Read, MapFlags.None, out var dataStream);

                                if (sourceFormat != targetFormat) {
                                    // BGRA格式轉換
                                    using (FormatConverter formatCoverter = new FormatConverter(ImagingFactory)) {
                                        if (formatCoverter.CanConvert(sourceFormat, targetFormat)) {
                                            Bitmap src = new Bitmap(ImagingFactory, desc.Width, desc.Height, sourceFormat, new DataRectangle(db.DataPointer, db.RowPitch));
                                            formatCoverter.Initialize(src, targetFormat, BitmapDitherType.None, null, 0, BitmapPaletteType.Custom);
                                            frameEncode.WriteSource(formatCoverter, new Rectangle(0, 0, desc.Width, desc.Height));
                                        }
                                    }
                                } else {
                                    frameEncode.WritePixels(desc.Height, new DataRectangle(db.DataPointer, db.RowPitch));
                                }

                                // 控制權歸還
                                d3dDevice.ImmediateContext.UnmapSubresource(staging, subresource);

                                frameEncode.Commit();
                                encoder.Commit();
                                result = Result.Ok;
                            }
                        }
                        
                    }
                }
            } catch (Exception e) {
                System.Diagnostics.Debug.WriteLine(e.ToString());
                result = Result.Fail;
            }

            Utilities.Dispose(ref staging);

            return result;
        }

        /// <summary>
        /// 建立一個臨時的 Texture 以便擷取資源
        /// </summary>
        /// <param name="source">來源texture</param>
        /// <param name="staging">複本texture</param>
        /// <returns></returns>
        private static Result CreateStagingTexture(DeviceContext deviceContext, Resource source, out Texture2DDescription desc, out Texture2D staging) {
            desc = new Texture2DDescription();
            staging = null;
            if (deviceContext == null && source == null) return Result.InvalidArg;

            ResourceDimension resourceDimension = source.Dimension;
            if (resourceDimension != ResourceDimension.Texture2D) {
                return Result.InvalidArg;
            }

            if (!(source.QueryInterface<Texture2D>() is Texture2D src)) return Result.Fail;
            desc = src.Description;
            var d3dDevice = deviceContext.Device;

            if (desc.SampleDescription.Count > 1) {
                desc.SampleDescription.Count = 1;
                desc.SampleDescription.Quality = 0;

                Texture2D temp;

                try {
                    temp = new Texture2D(d3dDevice, desc);
                } catch (SharpDXException e) {
                    return e.ResultCode;
                }

                DXGI.Format fmt = desc.Format.EnsureNotTypeless();

                FormatSupport support = FormatSupport.None;
                try {
                    support = d3dDevice.CheckFormatSupport(fmt);
                } catch (SharpDXException e) {
                    return e.ResultCode;
                }

                if ((support & FormatSupport.MultisampleResolve) == 0)
                    return Result.Fail;

                for (int item = 0; item < desc.ArraySize; ++item) {
                    for (int level = 0; level < desc.MipLevels; ++level) {
                        int index = Resource.CalculateSubResourceIndex(level, item, desc.MipLevels);
                        deviceContext.ResolveSubresource(temp, index, source, index, fmt);
                    }
                }

                desc.BindFlags = BindFlags.None;
                desc.OptionFlags &= ResourceOptionFlags.TextureCube;
                desc.CpuAccessFlags = CpuAccessFlags.Read;
                desc.Usage = ResourceUsage.Staging;

                try {
                    staging = new Texture2D(d3dDevice, desc);
                    deviceContext.CopyResource(temp, staging);
                } catch (SharpDXException e) {
                    return e.ResultCode;
                }
            } else if (desc.Usage == ResourceUsage.Staging && desc.CpuAccessFlags == CpuAccessFlags.Read) {
                staging = source.QueryInterface<Texture2D>();
            } else {
                desc.BindFlags = BindFlags.None;
                desc.OptionFlags &= ResourceOptionFlags.TextureCube;
                desc.CpuAccessFlags = CpuAccessFlags.Read;
                desc.Usage = ResourceUsage.Staging;

                try {
                    staging = new Texture2D(d3dDevice, desc);
                    if (staging != null) {
                        deviceContext.CopyResource(source, staging);
                    } else {
                        return Result.Fail; 
                    }
                } catch (SharpDXException e) {
                    return e.ResultCode;
                } catch (Exception ex) {
                    System.Diagnostics.Debug.WriteLine(ex.ToString());
                    return Result.Fail;
                }
            }

            return Result.Ok;
        }
    }
}
