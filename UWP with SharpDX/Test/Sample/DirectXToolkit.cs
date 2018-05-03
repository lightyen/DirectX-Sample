using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpDX.Direct3D11;
using SharpDX.Direct3D;
using SharpDX.DXGI;
using SharpDX.WIC;
using System.Runtime.InteropServices;

namespace SharpDX.DirectXTookit {

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

        public static void CreateDDSTextureFromMemoryEx(Direct3D11.Device d3dDevice,
            DDS dds,
            int maxsize,
            ResourceUsage usage,
            BindFlags bindFlags,
            CpuAccessFlags cpuAccessFlags,
            ResourceOptionFlags miscFlags,
            bool forceSRGB,
            out Direct3D11.Resource texture,
            out Direct3D11.ShaderResourceView textureView,
            out DDS_AlphaMode alphaMode) {
            texture = null;
            textureView = null;
            alphaMode = DDS_AlphaMode.Unknown;

            if (d3dDevice == null) throw new ArgumentNullException("d3dDevice");

            IntPtr ddsData = IntPtr.Zero;
            GCHandle handle = GCHandle.Alloc(dds.Data, GCHandleType.Pinned);

            try {
                ddsData = handle.AddrOfPinnedObject();
                var result = CreateTextureFromDDS(d3dDevice, null, dds.Header, dds.HeaderDXT10, ddsData, dds.Data.Length, maxsize, usage, bindFlags, cpuAccessFlags, miscFlags, forceSRGB, out texture, out textureView);
                if (result.Success) {
                    texture.DebugName = "DDSTextureLoader";
                    textureView.DebugName = "DDSTextureLoader";
                    alphaMode = dds.AlphaMode;
                }

            } finally {
                handle.Free();
            }
        }

        public static void CreateDDSTextureFromMemoryEx(Direct3D11.Device d3dDevice,
            DDS dds,
            IntPtr ddsData,
            int dataSize,
            int maxsize,
            ResourceUsage usage,
            BindFlags bindFlags,
            CpuAccessFlags cpuAccessFlags,
            ResourceOptionFlags miscFlags,
            bool forceSRGB,
            out Direct3D11.Resource texture,
            out Direct3D11.ShaderResourceView textureView,
            out DDS_AlphaMode alphaMode) {
            texture = null;
            textureView = null;
            alphaMode = DDS_AlphaMode.Unknown;

            if (d3dDevice == null) throw new ArgumentNullException("d3dDevice");


        }

        private static Result CreateTextureFromDDS(
            Direct3D11.Device d3dDevice,
            Direct3D11.DeviceContext deviceContext,
            DDS_HEADER header,
            DDS_HEADER_DXT10? header10,
            IntPtr bitData,
            int bitsize,
            int maxsize,
            ResourceUsage usage,
            BindFlags bindFlags,
            CpuAccessFlags cpuAccessFlags,
            ResourceOptionFlags resourceOptionFlags,
            bool forceSRGB,
            out Direct3D11.Resource texture, out ShaderResourceView textureView) {


            texture = null;
            textureView = null;

            int width = Convert.ToInt32(header.width);
            int height = Convert.ToInt32(header.height);
            int depth = Convert.ToInt32(header.depth);

            ResourceDimension resourceDimension = ResourceDimension.Unknown;
            int arraySize = 1;
            Format format = Format.Unknown;
            bool isCubeMap = false;

            int mipCount = Convert.ToInt32(header.mipMapCount);
            if (0 == mipCount) {
                mipCount = 1;
            }

            if (header.IsDX10) {
                var d3d10ext = header10.Value;
                arraySize = Convert.ToInt32(d3d10ext.arraySize);
                if (arraySize == 0) {
                    return Result.InvalidArg;
                }

                switch(d3d10ext.dxgiFormat) {
                    case Format.AI44:
                    case Format.IA44:
                    case Format.P8:
                    case Format.A8P8:
                        return Result.InvalidArg;
                    default:
                        if (d3d10ext.dxgiFormat.BitPerPixel() == 0) {
                            return Result.InvalidArg;
                        }
                        break;
                }

                format = d3d10ext.dxgiFormat;

                switch (d3d10ext.resourceDimension) {
                    case ResourceDimension.Texture1D:
                        // D3DX writes 1D textures with a fixed Height of 1
                        if ((header.flags.HasFlag(DDS_Header.Height)) && height != 1) {
                            return Result.InvalidArg;
                        }
                        height = depth = 1;
                        break;

                    case ResourceDimension.Texture2D:
                        if (d3d10ext.miscFlag.HasFlag(ResourceOptionFlags.TextureCube)) {
                            arraySize *= 6;
                            isCubeMap = true;
                        }
                        depth = 1;
                        break;

                    case ResourceDimension.Texture3D:
                        if (!(header.flags.HasFlag(DDS_Header.Volume))) {
                            return Result.InvalidArg;
                        }

                        if (arraySize > 1) {
                            return Result.InvalidArg;
                        }
                        break;

                    default:
                        return Result.InvalidArg;
                }
                resourceDimension = d3d10ext.resourceDimension;
            } else {
                format = header.ddsPixelFormat.Format;

                if (format == Format.Unknown) {
                    return Result.InvalidArg;
                }

                if (header.flags.HasFlag(DDS_Header.Volume)) {
                    resourceDimension = ResourceDimension.Texture3D;
                } else {

                    if (header.Caps2.HasFlag(DDS_CubeMap.CubeMap)) {
                        // We require all six faces to be defined
                        if (header.Caps2.HasFlag(DDS_CubeMap.AllFaces)) {
                            return Result.InvalidArg;
                        }

                        arraySize = 6;
                        isCubeMap = true;
                    }

                    depth = 1;
                    resourceDimension = ResourceDimension.Texture2D;

                    // Note there's no way for a legacy Direct3D 9 DDS to express a '1D' texture
                }

                if (format.BitPerPixel() == 0) {
                    return Result.InvalidArg;
                }
            }

            // Bound sizes (for security purposes we don't trust DDS file metadata larger than the D3D 11.x hardware requirements)
            if (mipCount > SharpDX.Direct3D11.Resource.MaximumMipLevels) {
                return Result.InvalidArg;
            }

            switch (resourceDimension) {
                case ResourceDimension.Texture1D:
                    if (arraySize > Direct3D11.Resource.MaximumTexture1DArraySize || width > Direct3D11.Resource.MaximumTexture1DSize) return Result.InvalidArg;
                    break;
                case ResourceDimension.Texture2D:
                    if (isCubeMap) {
                        if (arraySize > Direct3D11.Resource.MaximumTexture2DArraySize || width > Direct3D11.Resource.MaximumTexture2DSize || height > Direct3D11.Resource.MaximumTexture2DSize) return Result.InvalidArg;
                    }
                    break;
                case ResourceDimension.Texture3D:
                    if (arraySize > 1 || 
                        width > Direct3D11.Resource.MaximumTexture3DSize ||
                        height > Direct3D11.Resource.MaximumTexture3DSize ||
                        depth > Direct3D11.Resource.MaximumTexture3DSize) return Result.InvalidArg;
                    break;
            }

            // auto generate mipmaps
            bool autoGen = false;
            ////
            ////

            if (autoGen) {

            } else {
                DataBox[] initData = new DataBox[mipCount * arraySize];

                if (FillInitData(width, height, depth, mipCount, arraySize, format, bitData, bitsize, maxsize, out int twidth, out int theight, out int tdepth, out int skipMip, initData) == Result.Ok) {
                    var result = CreateD3DResources(d3dDevice, resourceDimension, twidth, theight, tdepth, mipCount - skipMip, arraySize,
                        format, usage, bindFlags, cpuAccessFlags, resourceOptionFlags, forceSRGB, isCubeMap, initData, out texture, out textureView);

                    if (!result.Success && maxsize == 0 && (mipCount > 1)) {
                        // Retry with a maxsize determined by feature level

                        switch (d3dDevice.FeatureLevel) {
                            case FeatureLevel.Level_9_1:
                            case FeatureLevel.Level_9_2:
                                if (isCubeMap) {
                                    maxsize = 512 /*D3D_FL9_1_REQ_TEXTURECUBE_DIMENSION*/;
                                } else {
                                    maxsize = (resourceDimension == ResourceDimension.Texture3D)
                                        ? 256 /*D3D_FL9_1_REQ_TEXTURE3D_U_V_OR_W_DIMENSION*/
                                        : 2048 /*D3D_FL9_1_REQ_TEXTURE2D_U_OR_V_DIMENSION*/;
                                }
                                break;

                            case FeatureLevel.Level_9_3:
                                maxsize = (resourceDimension == ResourceDimension.Texture3D)
                                    ? 256 /*D3D_FL9_1_REQ_TEXTURE3D_U_V_OR_W_DIMENSION*/
                                    : 4096 /*D3D_FL9_3_REQ_TEXTURE2D_U_OR_V_DIMENSION*/;
                                break;

                            default: // D3D_FEATURE_LEVEL_10_0 & D3D_FEATURE_LEVEL_10_1
                                maxsize = (resourceDimension == ResourceDimension.Texture3D)
                                    ? 2048 /*D3D10_REQ_TEXTURE3D_U_V_OR_W_DIMENSION*/
                                    : 8192 /*D3D10_REQ_TEXTURE2D_U_OR_V_DIMENSION*/;
                                break;
                        }

                        if (FillInitData(width, height, depth, mipCount, arraySize, format, bitData, bitsize, maxsize,
                            out twidth, out theight, out tdepth, out skipMip, initData).Success) {
                            var hr = CreateD3DResources(d3dDevice, resourceDimension, twidth, theight, tdepth, mipCount - skipMip, arraySize,
                                format, usage, bindFlags, cpuAccessFlags, resourceOptionFlags, forceSRGB,
                                isCubeMap, initData, out texture, out textureView);
                        }
                    }
                }
            }

            return Result.False;
        }

        private static Result FillInitData(
            int width,
            int height,
            int depth,
            int mipCount,
            int arraySize,
            Format format,
            IntPtr bitData,
            int bitSize,
            int maxsize,
            out int twidth,
            out int theight,
            out int tdepth,
            out int skipMip,
            DataBox[] initData) {

            skipMip = 0;
            twidth = 0;
            theight = 0;
            tdepth = 0;

            if (bitData == null || initData == null) return Result.InvalidPointer;

            int index = 0;
            int NumBytes = 0;
            int RowBytes = 0;
            IntPtr pSrcBits = bitData;
            IntPtr pEndBits = bitData + bitSize;

            for (int j = 0; j < arraySize; j++) {

                int w = width;
                int h = height;
                int d = depth;

                for (int i = 0; i < mipCount; i++) {
                    GetSurfaceInfo(w, h, format, out NumBytes, out RowBytes, out var NumRows);

                    if ((mipCount <= 1) || maxsize == 0 || (w <= maxsize && h <= maxsize && d <= maxsize)) {
                        if (twidth == 0) {
                            twidth = w;
                            theight = h;
                            tdepth = d;
                        }

                        System.Diagnostics.Debug.Assert(index < mipCount * arraySize);
                        initData[index].DataPointer = pSrcBits;
                        initData[index].RowPitch = RowBytes;
                        initData[index].SlicePitch = NumBytes;
                        ++index;
                    } else if (j == 0) {
                        // Count number of skipped mipmaps (first item only)
                        ++skipMip;
                    }

                    if ((pSrcBits + (NumBytes * d)).ToInt64() > pEndBits.ToInt64()) {
                        return Result.Fail;
                    }

                    pSrcBits += NumBytes * d;

                    w = w >> 1;
                    h = h >> 1;
                    d = d >> 1;
                    if (w == 0) {
                        w = 1;
                    }
                    if (h == 0) {
                        h = 1;
                    }
                    if (d == 0) {
                        d = 1;
                    }
                } 
            }

            return (index > 0) ? Result.Ok : Result.Fail;
        }

        private static Result CreateD3DResources(Direct3D11.Device d3dDevice,
            ResourceDimension resourceDimension,
            int width,
            int height,
            int depth,
            int mipCount,
            int arraySize,
            Format format,
            ResourceUsage usage,
            BindFlags bindFlags,
            CpuAccessFlags cpuAccessFlags,
            ResourceOptionFlags miscFlags,
            bool forceSRGB,
            bool isCubeMap,
            DataBox[] initData,
            out Direct3D11.Resource texture,
            out ShaderResourceView textureView) {
            texture = null;
            textureView = null;
            if (d3dDevice == null) return Result.InvalidArg;

            if (forceSRGB) {
                format = format.MakeSRgb();
            }

            ShaderResourceViewDescription SRVDesc = new ShaderResourceViewDescription {
                Format = format
            };

            switch (resourceDimension) {
                case ResourceDimension.Texture1D: {
                        Texture1DDescription desc = new Texture1DDescription {
                            Width = width,
                            MipLevels = mipCount,
                            ArraySize = arraySize,
                            Format = format,
                            Usage = usage,
                            BindFlags = bindFlags,
                            CpuAccessFlags = cpuAccessFlags,
                            OptionFlags = miscFlags & ~ResourceOptionFlags.TextureCube
                        };

                        Texture1D tex = new Texture1D(d3dDevice, desc, initData);

                        if (tex != null) {
                            if (arraySize > 1) {
                                SRVDesc.Dimension = ShaderResourceViewDimension.Texture1DArray;
                                SRVDesc.Texture1DArray.MipLevels = (mipCount == 0) ? -1 : desc.MipLevels;
                                SRVDesc.Texture1DArray.ArraySize = arraySize;
                            } else {
                                SRVDesc.Dimension = ShaderResourceViewDimension.Texture1D;
                                SRVDesc.Texture1D.MipLevels = (mipCount == 0) ? -1 : desc.MipLevels;
                            }

                            textureView = new ShaderResourceView(d3dDevice, tex, SRVDesc);

                            if (textureView == null) {
                                tex.Dispose();
                                texture = null;
                                return Result.Fail;
                            }
                            texture = tex;
                        } else {
                            return Result.Fail;
                        }
                    }
                    break;
                case ResourceDimension.Texture2D: {
                        Texture2DDescription desc = new Texture2DDescription {
                            Width = width,
                            Height = height,
                            MipLevels = mipCount,
                            ArraySize = arraySize,
                            Format = format,
                            SampleDescription = new SampleDescription(1, 0),
                            Usage = usage,
                            BindFlags = bindFlags,
                            CpuAccessFlags = cpuAccessFlags,
                        };
                        if (isCubeMap) {
                            desc.OptionFlags = miscFlags | ResourceOptionFlags.TextureCube;
                        } else {
                            desc.OptionFlags = miscFlags & ~ResourceOptionFlags.TextureCube;
                        }

                        Texture2D tex = new Texture2D(d3dDevice, desc, initData);

                        if (tex != null) {
                            if (isCubeMap) {
                                if (arraySize > 6) {
                                    SRVDesc.Dimension = ShaderResourceViewDimension.TextureCubeArray;
                                    SRVDesc.TextureCubeArray.MipLevels = (mipCount == 0) ? -1 : desc.MipLevels;
                                    // Earlier we set arraySize to (NumCubes * 6)
                                    SRVDesc.TextureCubeArray.CubeCount = arraySize / 6;
                                } else {
                                    SRVDesc.Dimension = ShaderResourceViewDimension.TextureCube;
                                    SRVDesc.TextureCube.MipLevels = (mipCount == 0) ? -1 : desc.MipLevels;
                                }
                            } else if (arraySize > 1) {
                                SRVDesc.Dimension = ShaderResourceViewDimension.Texture2DArray;
                                SRVDesc.Texture2DArray.MipLevels = (mipCount == 0) ? -1 : desc.MipLevels;
                                SRVDesc.Texture2DArray.ArraySize = arraySize;
                            } else {
                                SRVDesc.Dimension = ShaderResourceViewDimension.Texture2D;
                                SRVDesc.Texture2D.MipLevels = (mipCount == 0) ? -1 : desc.MipLevels;
                            }

                            textureView = new ShaderResourceView(d3dDevice, tex, SRVDesc);

                            if (textureView == null) {
                                tex.Dispose();
                                texture = null;
                                return Result.Fail;
                            }
                            texture = tex;
                        } else {
                            return Result.Fail;
                        }
                    }
                    break;
                case ResourceDimension.Texture3D: {
                        Texture3DDescription desc = new Texture3DDescription {
                            Width = width,
                            Height = height,
                            Depth = depth,
                            MipLevels = mipCount,
                            Format = format,
                            Usage = usage,
                            BindFlags = bindFlags,
                            CpuAccessFlags = cpuAccessFlags,
                            OptionFlags = miscFlags & ~ResourceOptionFlags.TextureCube
                        };

                        Texture3D tex = new Texture3D(d3dDevice, desc, initData);

                        if (tex != null) {
                            SRVDesc.Dimension = ShaderResourceViewDimension.Texture3D;
                            SRVDesc.Texture3D.MipLevels = (mipCount == 0) ? -1 : desc.MipLevels;

                            textureView = new ShaderResourceView(d3dDevice, tex, SRVDesc);

                            if (textureView == null) {
                                tex.Dispose();
                                texture = null;
                                return Result.Fail;
                            }
                            texture = tex;
                        } else {
                            return Result.Fail;
                        }
                    }
                    break;
            }

            return Result.Ok;
        }

        private static void GetSurfaceInfo(int width, int height, Format fmt, out int numBytes, out int rowBytes, out int numRows) {
            
            rowBytes = 0;
            numRows = 0;
            numBytes = 0;

            bool bc = false;
            bool packed = false;
            bool planar = false;
            int bpe = 0;

            switch (fmt) {
                case Format.BC1_Typeless:
                case Format.BC1_UNorm:
                case Format.BC1_UNorm_SRgb:
                case Format.BC4_Typeless:
                case Format.BC4_UNorm:
                case Format.BC4_SNorm:
                    bc = true;
                    bpe = 8;
                    break;

                case Format.BC2_Typeless:
                case Format.BC2_UNorm:
                case Format.BC2_UNorm_SRgb:
                case Format.BC3_Typeless:
                case Format.BC3_UNorm:
                case Format.BC3_UNorm_SRgb:
                case Format.BC5_Typeless:
                case Format.BC5_UNorm:
                case Format.BC5_SNorm:
                case Format.BC6H_Typeless:
                case Format.BC6H_Uf16:
                case Format.BC6H_Sf16:
                case Format.BC7_Typeless:
                case Format.BC7_UNorm:
                case Format.BC7_UNorm_SRgb:
                    bc = true;
                    bpe = 16;
                    break;

                case Format.R8G8_B8G8_UNorm:
                case Format.G8R8_G8B8_UNorm:
                case Format.YUY2:
                    packed = true;
                    bpe = 4;
                    break;

                case Format.Y210:
                case Format.Y216:
                    packed = true;
                    bpe = 8;
                    break;

                case Format.NV12:
                case Format.Opaque420:
                    planar = true;
                    bpe = 2;
                    break;

                case Format.P010:
                case Format.P016:
                    planar = true;
                    bpe = 4;
                    break;
            }

            if (bc) {
                int numBlocksWide = 0;
                if (width > 0) {
                    numBlocksWide = Math.Max(1, (width + 3) / 4);
                }
                int numBlocksHigh = 0;
                if (height > 0) {
                    numBlocksHigh = Math.Max(1, (height + 3) / 4);
                }
                rowBytes = numBlocksWide * bpe;
                numRows = numBlocksHigh;
                numBytes = rowBytes * numBlocksHigh;
            } else if (packed) {
                rowBytes = ((width + 1) >> 1) * bpe;
                numRows = height;
                numBytes = rowBytes * height;
            } else if (fmt == Format.NV11) {
                rowBytes = ((width + 3) >> 2) * 4;
                numRows = height * 2; // Direct3D makes this simplifying assumption, although it is larger than the 4:1:1 data
                numBytes = rowBytes * numRows;
            } else if (planar) {
                rowBytes = ((width + 1) >> 1) * bpe;
                numBytes = (rowBytes * height) + ((rowBytes * height + 1) >> 1);
                numRows = height + ((height + 1) >> 1);
            } else {
                int bpp = fmt.BitPerPixel();
                rowBytes = (width * bpp + 7) / 8; // round up to nearest byte
                numRows = height;
                numBytes = rowBytes * height;
            }
        }
    }
}
