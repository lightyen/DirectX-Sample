using System;
using System.IO;
using SharpDX;
using DXGI = SharpDX.DXGI;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using System.Runtime.InteropServices;
using SharpDX.Multimedia;

namespace DirectXToolkit {
    public static partial class DirectXTK {

        public static void CreateDDSTextureFromStream(Device d3dDevice, Stream stream, out Resource texture, out ShaderResourceView textureView, DeviceContext deviceContext = null) {
            if (stream.CanRead) {
                using (var dds = new DirectDrawSurface(stream)) {
                    CreateDDSTextureFromMemory(d3dDevice, dds, out texture, out textureView);
                }
            } else {
                texture = null;
                textureView = null;
            }
        }

        /// <summary>
        /// 從記憶體建立DDS Texture
        /// </summary>
        /// <param name="d3dDevice"></param>
        /// <param name="dds"></param>
        /// <param name="texture"></param>
        /// <param name="textureView"></param>
        public static void CreateDDSTextureFromMemory(Device d3dDevice, DirectDrawSurface dds, out Resource texture, out ShaderResourceView textureView) {
            CreateDDSTextureFromMemoryEx(d3dDevice, dds, 0, ResourceUsage.Default, BindFlags.ShaderResource, CpuAccessFlags.None, ResourceOptionFlags.None, false, out texture, out textureView, out var alphaMode);
        }

        public static void CreateDDSTextureFromMemoryEx(Device d3dDevice,
            DirectDrawSurface dds,
            int maxsize,
            ResourceUsage usage,
            BindFlags bindFlags,
            CpuAccessFlags cpuAccessFlags,
            ResourceOptionFlags miscFlags,
            bool forceSRGB,
            out Resource texture,
            out ShaderResourceView textureView,
            out DDS_AlphaMode alphaMode) {
            texture = null;
            textureView = null;
            alphaMode = DDS_AlphaMode.Unknown;

            if (d3dDevice == null) throw new ArgumentNullException("d3dDevice");
            if (dds == null && !dds.Header.HasValue) throw new ArgumentNullException("dds");

            try {
                
                var result = CreateTextureFromDDS(d3dDevice, null, dds.Header.Value, dds.HeaderDXT10, dds.Data, dds.DataLength, maxsize, usage, bindFlags, cpuAccessFlags, miscFlags, forceSRGB, out texture, out textureView);
                if (result.Success) {
                    texture.DebugName = "DDSTextureLoader";
                    textureView.DebugName = "DDSTextureLoader";
                    alphaMode = dds.AlphaMode;
                }

            } finally {
                
            }
        }

        private static Result CreateTextureFromDDS(
            Device d3dDevice,
            DeviceContext deviceContext,
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
            out Resource texture, out ShaderResourceView textureView) {


            texture = null;
            textureView = null;

            int width = Convert.ToInt32(header.width);
            int height = Convert.ToInt32(header.height);
            int depth = Convert.ToInt32(header.depth);

            ResourceDimension resourceDimension = ResourceDimension.Unknown;
            int arraySize = 1;
            DXGI.Format format = DXGI.Format.Unknown;
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

                switch (d3d10ext.dxgiFormat) {
                    case DXGI.Format.AI44:
                    case DXGI.Format.IA44:
                    case DXGI.Format.P8:
                    case DXGI.Format.A8P8:
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

                if (format == DXGI.Format.Unknown) {
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
                    if (arraySize > Resource.MaximumTexture1DArraySize || width > Resource.MaximumTexture1DSize) return Result.InvalidArg;
                    break;
                case ResourceDimension.Texture2D:
                    if (isCubeMap) {
                        if (arraySize > Resource.MaximumTexture2DArraySize || width > Resource.MaximumTexture2DSize || height > Resource.MaximumTexture2DSize) return Result.InvalidArg;
                    }
                    break;
                case ResourceDimension.Texture3D:
                    if (arraySize > 1 ||
                        width > Resource.MaximumTexture3DSize ||
                        height > Resource.MaximumTexture3DSize ||
                        depth > Resource.MaximumTexture3DSize) return Result.InvalidArg;
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
            DXGI.Format format,
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

        private static void GetSurfaceInfo(int width, int height, DXGI.Format fmt, out int numBytes, out int rowBytes, out int numRows) {

            rowBytes = 0;
            numRows = 0;
            numBytes = 0;

            bool bc = false;
            bool packed = false;
            bool planar = false;
            int bpe = 0;

            switch (fmt) {
                case DXGI.Format.BC1_Typeless:
                case DXGI.Format.BC1_UNorm:
                case DXGI.Format.BC1_UNorm_SRgb:
                case DXGI.Format.BC4_Typeless:
                case DXGI.Format.BC4_UNorm:
                case DXGI.Format.BC4_SNorm:
                    bc = true;
                    bpe = 8;
                    break;

                case DXGI.Format.BC2_Typeless:
                case DXGI.Format.BC2_UNorm:
                case DXGI.Format.BC2_UNorm_SRgb:
                case DXGI.Format.BC3_Typeless:
                case DXGI.Format.BC3_UNorm:
                case DXGI.Format.BC3_UNorm_SRgb:
                case DXGI.Format.BC5_Typeless:
                case DXGI.Format.BC5_UNorm:
                case DXGI.Format.BC5_SNorm:
                case DXGI.Format.BC6H_Typeless:
                case DXGI.Format.BC6H_Uf16:
                case DXGI.Format.BC6H_Sf16:
                case DXGI.Format.BC7_Typeless:
                case DXGI.Format.BC7_UNorm:
                case DXGI.Format.BC7_UNorm_SRgb:
                    bc = true;
                    bpe = 16;
                    break;

                case DXGI.Format.R8G8_B8G8_UNorm:
                case DXGI.Format.G8R8_G8B8_UNorm:
                case DXGI.Format.YUY2:
                    packed = true;
                    bpe = 4;
                    break;

                case DXGI.Format.Y210:
                case DXGI.Format.Y216:
                    packed = true;
                    bpe = 8;
                    break;

                case DXGI.Format.NV12:
                case DXGI.Format.Opaque420:
                    planar = true;
                    bpe = 2;
                    break;

                case DXGI.Format.P010:
                case DXGI.Format.P016:
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
            } else if (fmt == DXGI.Format.NV11) {
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

        private static Result CreateD3DResources(Device d3dDevice,
            ResourceDimension resourceDimension,
            int width,
            int height,
            int depth,
            int mipCount,
            int arraySize,
            DXGI.Format format,
            ResourceUsage usage,
            BindFlags bindFlags,
            CpuAccessFlags cpuAccessFlags,
            ResourceOptionFlags miscFlags,
            bool forceSRGB,
            bool isCubeMap,
            DataBox[] initData,
            out Resource texture,
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
                            SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
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
    }
}
