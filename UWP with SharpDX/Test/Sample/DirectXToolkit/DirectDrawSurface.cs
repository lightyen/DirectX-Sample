using System;
using System.IO;
using System.Runtime.InteropServices;
using SharpDX.Multimedia;

// https://msdn.microsoft.com/zh-tw/library/windows/desktop/dn424129(v=vs.85).aspx
// https://msdn.microsoft.com/en-us/library/windows/desktop/bb943991(v=vs.85).aspx
// https://blog.csdn.net/puppet_master/article/details/50186613

namespace SharpDX.DirectXToolkit {
    
    [StructLayout(LayoutKind.Sequential)]
    public struct DDS_HEADER {
        public uint magic;
        public uint size;
        public DDS_Header flags;
        public uint height;
        public uint width;
        public uint pitchOrLinearSize;
        public uint depth;
        public uint mipMapCount;
        /// <summary>
        /// Unused.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 11)]
        private uint[] dwReserved1;
        public DDS_PIXELFORMAT ddsPixelFormat;
        public DDS_Caps Caps;
        public DDS_CubeMap Caps2;
        /// <summary>
        /// Unused.
        /// </summary>
        private uint Caps3;
        /// <summary>
        /// Unused.
        /// </summary>
        private uint Caps4;
        /// <summary>
        /// Unused.
        /// </summary>
        private uint Reserved2;

        public bool IsDX10 {
            get {
                // dwFourCC == "DX10"
                return ddsPixelFormat.flags.HasFlag(DDS_PixelFormat.FourCC) && ddsPixelFormat.fourCC == 0x30315844;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DDS_HEADER_DXT10 {
        public SharpDX.DXGI.Format dxgiFormat;
        public SharpDX.Direct3D11.ResourceDimension resourceDimension;
        public SharpDX.Direct3D11.ResourceOptionFlags miscFlag;
        public uint arraySize;
        public DDS_AlphaMode miscFlags2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DDS_PIXELFORMAT {
        public uint size;
        public DDS_PixelFormat flags;
        public FourCC fourCC;
        public uint RGBBitCount;
        public uint RBitMask;
        public uint GBitMask;
        public uint BBitMask;
        public uint ABitMask;

        private bool IsBitMask(uint r, uint g, uint b, uint a) {
            return RBitMask == r && GBitMask == g && BBitMask == b && ABitMask == a;
        }

        public DXGI.Format Format {
            get {
                if (flags.HasFlag(DDS_PixelFormat.RGB)) {

                    // Note that sRGB formats are written using the "DX10" extended header

                    switch (RGBBitCount) {
                        case 32:
                            if (IsBitMask(0x000000ff, 0x0000ff00, 0x00ff0000, 0xff000000)) {
                                return DXGI.Format.R8G8B8A8_UNorm;
                            }

                            if (IsBitMask(0x00ff0000, 0x0000ff00, 0x000000ff, 0xff000000)) {
                                return DXGI.Format.B8G8R8A8_UNorm;
                            }

                            if (IsBitMask(0x00ff0000, 0x0000ff00, 0x000000ff, 0x00000000)) {
                                return DXGI.Format.B8G8R8X8_UNorm;
                            }

                            // No DXGI format maps to ISBITMASK(0x000000ff,0x0000ff00,0x00ff0000,0x00000000) aka D3DFMT_X8B8G8R8

                            // Note that many common DDS reader/writers (including D3DX) swap the
                            // the RED/BLUE masks for 10:10:10:2 formats. We assumme
                            // below that the 'backwards' header mask is being used since it is most
                            // likely written by D3DX. The more robust solution is to use the 'DX10'
                            // header extension and specify the DXGI_FORMAT_R10G10B10A2_UNORM format directly

                            // For 'correct' writers, this should be 0x000003ff,0x000ffc00,0x3ff00000 for RGB data
                            if (IsBitMask(0x3ff00000, 0x000ffc00, 0x000003ff, 0xc0000000)) {
                                return DXGI.Format.R10G10B10A2_UNorm;
                            }

                            // No DXGI format maps to ISBITMASK(0x000003ff,0x000ffc00,0x3ff00000,0xc0000000) aka D3DFMT_A2R10G10B10

                            if (IsBitMask(0x0000ffff, 0xffff0000, 0x00000000, 0x00000000)) {
                                return DXGI.Format.R16G16_UNorm;
                            }

                            if (IsBitMask(0xffffffff, 0x00000000, 0x00000000, 0x00000000)) {
                                // Only 32-bit color channel format in D3D9 was R32F
                                return DXGI.Format.R32_Float; // D3DX writes this out as a FourCC of 114
                            }
                            break;
                        case 24:
                            // No 24bpp DXGI formats aka D3DFMT_R8G8B8
                            break;
                        case 16:
                            if (IsBitMask(0x7c00, 0x03e0, 0x001f, 0x8000)) {
                                return DXGI.Format.B5G5R5A1_UNorm;
                            }
                            if (IsBitMask(0xf800, 0x07e0, 0x001f, 0x0000)) {
                                return DXGI.Format.B5G6R5_UNorm;
                            }

                            // No DXGI format maps to ISBITMASK(0x7c00,0x03e0,0x001f,0x0000) aka D3DFMT_X1R5G5B5

                            if (IsBitMask(0x0f00, 0x00f0, 0x000f, 0xf000)) {
                                return DXGI.Format.B4G4R4A4_UNorm;
                            }

                            // No DXGI format maps to ISBITMASK(0x0f00,0x00f0,0x000f,0x0000) aka D3DFMT_X4R4G4B4

                            // No 3:3:2, 3:3:2:8, or paletted DXGI formats aka D3DFMT_A8R3G3B2, D3DFMT_R3G3B2, D3DFMT_P8, D3DFMT_A8P8, etc.
                            break;

                    }

                } else if (flags.HasFlag(DDS_PixelFormat.Luminance)) {
                    if (8 == RGBBitCount) {
                        if (IsBitMask(0x000000ff, 0x00000000, 0x00000000, 0x00000000)) {
                            return DXGI.Format.R8_UNorm; // D3DX10/11 writes this out as DX10 extension
                        }

                        // No DXGI format maps to ISBITMASK(0x0f,0x00,0x00,0xf0) aka D3DFMT_A4L4
                    }

                    if (16 == RGBBitCount) {
                        if (IsBitMask(0x0000ffff, 0x00000000, 0x00000000, 0x00000000)) {
                            return DXGI.Format.R16_UNorm; // D3DX10/11 writes this out as DX10 extension
                        }
                        if (IsBitMask(0x000000ff, 0x00000000, 0x00000000, 0x0000ff00)) {
                            return DXGI.Format.R8G8_UNorm; // D3DX10/11 writes this out as DX10 extension
                        }
                    }
                } else if (flags.HasFlag(DDS_PixelFormat.Alpha)) {
                    if (8 == RGBBitCount) {
                        return DXGI.Format.A8_UNorm;
                    }
                } else if (flags.HasFlag(DDS_PixelFormat.FourCC)) {

                    if (new FourCC("DXT1") == fourCC) {
                        return DXGI.Format.BC1_UNorm;
                    }
                    if (new FourCC("DXT3") == fourCC) {
                        return DXGI.Format.BC2_UNorm;
                    }
                    if (new FourCC("DXT5") == fourCC) {
                        return DXGI.Format.BC3_UNorm;
                    }

                    // While pre-mulitplied alpha isn't directly supported by the DXGI formats,
                    // they are basically the same as these BC formats so they can be mapped
                    if (new FourCC("DXT2") == fourCC) {
                        return DXGI.Format.BC2_UNorm;
                    }
                    if (new FourCC("DXT4") == fourCC) {
                        return DXGI.Format.BC3_UNorm;
                    }

                    if (new FourCC("ATI1") == fourCC) {
                        return DXGI.Format.BC4_UNorm;
                    }
                    if (new FourCC("BC4U") == fourCC) {
                        return DXGI.Format.BC4_UNorm;
                    }
                    if (new FourCC("BC4S") == fourCC) {
                        return DXGI.Format.BC4_SNorm;
                    }

                    if (new FourCC("ATI2") == fourCC) {
                        return DXGI.Format.BC5_UNorm;
                    }
                    if (new FourCC("BC5U") == fourCC) {
                        return DXGI.Format.BC5_UNorm;
                    }
                    if (new FourCC("BC5S") == fourCC) {
                        return DXGI.Format.BC5_SNorm;
                    }

                    // BC6H and BC7 are written using the "DX10" extended header

                    if (new FourCC("RGBG") == fourCC) {
                        return DXGI.Format.R8G8_B8G8_UNorm;
                    }
                    if (new FourCC("GRGB") == fourCC) {
                        return DXGI.Format.G8R8_G8B8_UNorm;
                    }

                    if (new FourCC("YUY2") == fourCC) {
                        return DXGI.Format.YUY2;
                    }

                    // Check for D3DFORMAT enums being set here
                    switch ((uint)fourCC) {
                        case 36: // D3DFMT_A16B16G16R16
                            return DXGI.Format.R16G16B16A16_UNorm;

                        case 110: // D3DFMT_Q16W16V16U16
                            return DXGI.Format.R16G16B16A16_SNorm;

                        case 111: // D3DFMT_R16F
                            return DXGI.Format.R16_Float;

                        case 112: // D3DFMT_G16R16F
                            return DXGI.Format.R16G16_Float;

                        case 113: // D3DFMT_A16B16G16R16F
                            return DXGI.Format.R16G16B16A16_Float;

                        case 114: // D3DFMT_R32F
                            return DXGI.Format.R32_Float;

                        case 115: // D3DFMT_G32R32F
                            return DXGI.Format.R32G32_Float;

                        case 116: // D3DFMT_A32B32G32R32F
                            return DXGI.Format.R32G32B32A32_Float;
                    }
                }

                return DXGI.Format.Unknown;
            }
        }
    }

    public class DirectDrawSurface : IDisposable {

        public DDS_HEADER? Header {
            get; private set;
        }

        public DDS_HEADER_DXT10? HeaderDXT10 {
            get; private set;
        }

        private GCHandle dataHandle;

        public IntPtr Data { get; private set; }
        public int DataLength { get; private set; }

        public const uint CubeMap = 0x00000200;

        public DirectDrawSurface(Stream stream) {

            if (stream.CanRead) {
                var br = new BinaryReader(stream);
                if (stream.Length > Marshal.SizeOf<DDS_HEADER>()) {
                    GetDdsHeader(stream);
                    if (Header != null) {
                        GetDdsHeaderDXT10();
                        Data += Marshal.SizeOf<DDS_HEADER>();
                        DataLength -= Marshal.SizeOf<DDS_HEADER>();
                    }
                    if (HeaderDXT10.HasValue) {
                        Data += Marshal.SizeOf<DDS_HEADER_DXT10>();
                        DataLength -= Marshal.SizeOf<DDS_HEADER_DXT10>();
                    }
                }
            }
        }

        private bool disposed = false;

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    dataHandle.Free();
                }
                Data = IntPtr.Zero;
                disposed = true;
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~DirectDrawSurface() {
            if (dataHandle.IsAllocated) {
                dataHandle.Free();
            }
        }

        private void GetDdsHeader(Stream stream) {
            // 超過1G 不搞惹 !!
            if (stream.Length < 1073741824) {
                var br = new BinaryReader(stream);
                var data = br.ReadBytes((int)stream.Length);
                DataLength = data.Length;
                dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
                Data = dataHandle.AddrOfPinnedObject();
                Header = Marshal.PtrToStructure<DDS_HEADER>(Data);
                // 檢驗一下 magic number 還有 header size
                if (Header?.magic != 0x20534444 || Header?.size != Marshal.SizeOf<DDS_HEADER>() - 4) {
                    Header = null;
                    Data = IntPtr.Zero;
                    DataLength = 0;
                    disposed = true;
                    dataHandle.Free();
                }
            }

            
            
        }

        private void GetDdsHeaderDXT10() {
            if (Header?.IsDX10 == true) {
                HeaderDXT10 = Marshal.PtrToStructure<DDS_HEADER_DXT10>(Data + Marshal.SizeOf<DDS_HEADER>());
            } else {
                HeaderDXT10 = null;
            }
        }

        public DDS_AlphaMode AlphaMode {
            get {
                if (Header?.IsDX10 == true) {
                    var d3d10ext = HeaderDXT10.Value;
                    var mode = d3d10ext.miscFlags2 & (DDS_AlphaMode)DDS_MISC_FLAGS2.DDS_MISC_FLAGS2_ALPHA_MODE_MASK;
                    switch (mode) {
                        case DDS_AlphaMode.Straight:
                        case DDS_AlphaMode.Premultiplied:
                        case DDS_AlphaMode.Opaque:
                        case DDS_AlphaMode.Custom:
                            return mode;
                    }
                } else if ((new FourCC("DXT2") == Header?.ddsPixelFormat.fourCC) || (new FourCC("DXT4") == Header?.ddsPixelFormat.fourCC)) {
                    return DDS_AlphaMode.Premultiplied;
                }
                return DDS_AlphaMode.Unknown;
            }
        }
    }


    [Flags]
    public enum DDS_Header {
        /// <summary>
        /// Required in every .dds file.
        /// </summary>
        Caps = 0x1,
        /// <summary>
        /// Required in every .dds file.
        /// </summary>
        Height = 0x2,
        /// <summary>
        /// Required in every .dds file.
        /// </summary>
        Width = 0x4,
        /// <summary>
        /// Required when pitch is provided for an uncompressed texture.
        /// </summary>
        Pitch = 0x8,
        /// <summary>
        /// Required in every .dds file.
        /// </summary>
        PixelFormat = 0x1000,
        /// <summary>
        /// Required in a mipmapped texture.
        /// </summary>
        MipMapCount = 0x20000,
        /// <summary>
        /// Required when pitch is provided for a compressed texture.
        /// </summary>
        LinearSize = 0x80000,
        /// <summary>
        /// Required in a depth texture.
        /// </summary>
        Depth = 0x800000,

        Texture = Caps | Height | Width | PixelFormat,
        MipMap = MipMapCount,
        Volume = Depth,
    }

    [Flags]
    public enum DDS_PixelFormat {
        /// <summary>
        /// Texture contains alpha data; dwRGBAlphaBitMask contains valid data.
        /// </summary>
        AlphaPixels = 0x1,
        /// <summary>
        /// Used in some older DDS files for alpha channel only uncompressed data (dwRGBBitCount contains the alpha channel bitcount; dwABitMask contains valid data)
        /// </summary>
        Alpha = 0x2,
        /// <summary>
        /// Texture contains compressed RGB data; dwFourCC contains valid data.
        /// </summary>
        FourCC = 0x4,
        /// <summary>
        /// Texture contains uncompressed RGB data; dwRGBBitCount and the RGB masks (dwRBitMask, dwGBitMask, dwBBitMask) contain valid data.
        /// </summary>
        RGB = 0x40,
        RGBA = RGB | AlphaPixels,
        /// <summary>
        /// Used in some older DDS files for YUV uncompressed data (dwRGBBitCount contains the YUV bit count; dwRBitMask contains the Y mask, dwGBitMask contains the U mask, dwBBitMask contains the V mask)
        /// </summary>
        YUV = 0x200,
        /// <summary>
        /// Used in some older DDS files for single channel color uncompressed data (dwRGBBitCount contains the luminance channel bit count; dwRBitMask contains the channel mask). Can be combined with DDPF_ALPHAPIXELS for a two channel DDS file.
        /// </summary>
        Luminance = 0x20000,
        LuminanceA = Luminance | AlphaPixels,
    }

    [Flags]
    public enum DDS_AlphaMode {
        Unknown = 0,
        Straight = 1,
        Premultiplied = 2,
        Opaque = 3,
        Custom = 4,
    };

    [Flags]
    public enum DDS_Caps {
        Complex = 0x8,
        MipMap = 0x400000,
        Texture = 0x1000,
    }

    [Flags]
    public enum DDS_Caps2 {
        /// <summary>
        /// Required for a cube map.
        /// </summary>
        CubeMap = 0x200,
        /// <summary>
        /// Required when these surfaces are stored in a cube map.	
        /// </summary>
        PositiveX = 0x400,
        /// <summary>
        /// Required when these surfaces are stored in a cube map.	
        /// </summary>
        NegativeX = 0x800,
        /// <summary>
        /// Required when these surfaces are stored in a cube map.	
        /// </summary>
        PositiveY = 0x1000,
        /// <summary>
        /// Required when these surfaces are stored in a cube map.	
        /// </summary>
        NegativeY = 0x2000,
        /// <summary>
        /// Required when these surfaces are stored in a cube map.	
        /// </summary>
        PositiveZ = 0x4000,
        /// <summary>
        /// Required when these surfaces are stored in a cube map.
        /// </summary>
        NegativeZ = 0x8000,
        /// <summary>
        /// Required for a volume texture.
        /// </summary>
        Volume = 0x200000,
    }

    [Flags]
    public enum DDS_CubeMap {
        CubeMap = DDS_Caps2.CubeMap,
        PositiveX = DDS_Caps2.CubeMap | DDS_Caps2.PositiveX,
        NegativeX = DDS_Caps2.CubeMap | DDS_Caps2.NegativeX,
        PositiveY = DDS_Caps2.CubeMap | DDS_Caps2.PositiveY,
        NegativeY = DDS_Caps2.CubeMap | DDS_Caps2.NegativeY,
        PositiveZ = DDS_Caps2.CubeMap | DDS_Caps2.PositiveZ,
        NegativeZ = DDS_Caps2.CubeMap | DDS_Caps2.NegativeZ,
        AllFaces = PositiveX | NegativeX | PositiveY | NegativeY | PositiveZ | NegativeZ,
    }

    enum DDS_MISC_FLAGS2 {
        DDS_MISC_FLAGS2_ALPHA_MODE_MASK = 0x7,
    }
}
