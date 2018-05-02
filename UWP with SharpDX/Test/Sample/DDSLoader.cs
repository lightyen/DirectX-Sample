using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SharpDX.Direct3D11 {
    public class DDS {
        // https://msdn.microsoft.com/zh-tw/library/windows/desktop/dn424129(v=vs.85).aspx
        // https://msdn.microsoft.com/en-us/library/windows/desktop/bb943991(v=vs.85).aspx
        // https://blog.csdn.net/puppet_master/article/details/50186613
        public bool IsDDS = false;
        public DDS_HEADER Header;
        public bool IsDX10 = false;
        public DDS_HEADER_DXT10 HeaderDXT10;
        public byte[] Data;

        public DDS(Stream stream) {
            if (stream.CanRead) {
                var br = new BinaryReader(stream);
                if (stream.Length >= 4) {
                    // magic
                    if (br.ReadInt32() == 0x20534444) {
                        GetDdsHeader(stream);
                        if (IsDDS) {
                            GetDdsHeaderDXT10(stream);
                            Data = br.ReadBytes(Convert.ToInt32(stream.Length - stream.Position));
                        }
                    }
                }
            }
        }

        void GetDdsHeader(Stream stream) {
            int header_size = Marshal.SizeOf<DDS_HEADER>();
            if (stream.Length - stream.Position >= header_size) {
                var br = new BinaryReader(stream);
                var data = br.ReadBytes(header_size);
                unsafe {
                    fixed (byte* ptr = &data[0]) {
                        Header = *(DDS_HEADER*)ptr;
                    }
                }
                if (Header.size == Marshal.SizeOf<DDS_HEADER>()) IsDDS = true;
            }
        }

        void GetDdsHeaderDXT10(Stream stream) {
            // dwFourCC == "DX10"
            if (Header.ddsPixelFormat.flags.HasFlag(DDS_PIXELFORMAT_FLAGS.FourCC) && Header.ddsPixelFormat.fourCC == 0x30315844) {
                IsDX10 = true;
                int header_size = Marshal.SizeOf<DDS_HEADER_DXT10>();
                if (stream.Length - stream.Position >= header_size) {
                    var br = new BinaryReader(stream);
                    var data = br.ReadBytes(header_size);
                    unsafe {
                        fixed (byte* ptr = &data[0]) {
                            HeaderDXT10 = *(DDS_HEADER_DXT10*)ptr;
                        }
                    }
                }
            }
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct DDS_HEADER {
        [FieldOffset(0)] public uint size;
        [FieldOffset(4)] public DDS_HEADER_FLAGS flags;
        [FieldOffset(8)] public uint height;
        [FieldOffset(12)] public uint width;
        [FieldOffset(16)] public uint pitchOrLinearSize;
        [FieldOffset(20)] public uint depth;
        [FieldOffset(24)] public uint mipMapCount;
        //uint[] dwReserved1;
        [FieldOffset(72)] public DDS_PIXELFORMAT ddsPixelFormat;
        [FieldOffset(104)] public uint Caps;
        [FieldOffset(108)] public uint Caps2;
        [FieldOffset(112)] public uint Caps3;
        [FieldOffset(116)] public uint Caps4;
        [FieldOffset(120)] private uint Reserved2;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct DDS_HEADER_DXT10 {
        [FieldOffset(0)] public SharpDX.DXGI.Format dxgiFormat;
        [FieldOffset(4)] public SharpDX.Direct3D11.ResourceDimension resourceDimension;
        [FieldOffset(8)] public SharpDX.Direct3D11.ResourceOptionFlags miscFlag;
        [FieldOffset(12)] public uint arraySize;
        [FieldOffset(16)] public DDS_ALPHA_MODE miscFlags2;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct DDS_PIXELFORMAT {
        [FieldOffset(0)] public uint size;
        [FieldOffset(4)] public DDS_PIXELFORMAT_FLAGS flags;
        [FieldOffset(8)] public uint fourCC;
        [FieldOffset(12)] public uint RGBBitCount;
        [FieldOffset(16)] public uint RBitMask;
        [FieldOffset(20)] public uint GBitMask;
        [FieldOffset(24)] public uint BBitMask;
        [FieldOffset(28)] public uint ABitMask;

        private bool IsBitMask(uint r, uint g, uint b, uint a) {
            return RBitMask == r && GBitMask == g && BBitMask == b && ABitMask == a;
        }

        public DXGI.Format Format {
            get {
                if (flags.HasFlag(DDS_PIXELFORMAT_FLAGS.RGB)) {

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

                } else if (flags.HasFlag(DDS_PIXELFORMAT_FLAGS.Luminance)) {
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
                } else if (flags.HasFlag(DDS_PIXELFORMAT_FLAGS.Alpha)) {
                    if (8 == RGBBitCount) {
                        return DXGI.Format.A8_UNorm;
                    }
                } else if (flags.HasFlag(DDS_PIXELFORMAT_FLAGS.FourCC)) {

                    uint MakeFourCC(char a, char b, char c, char d) {
                        return (byte)a | ((uint)(byte)b << 8) | ((uint)(byte)c << 16) | ((uint)(byte)d << 24);
                    }

                    if (MakeFourCC('D', 'X', 'T', '1') == fourCC) {
                        return DXGI.Format.BC1_UNorm;
                    }
                    if (MakeFourCC('D', 'X', 'T', '3') == fourCC) {
                        return DXGI.Format.BC2_UNorm;
                    }
                    if (MakeFourCC('D', 'X', 'T', '5') == fourCC) {
                        return DXGI.Format.BC3_UNorm;
                    }

                    // While pre-mulitplied alpha isn't directly supported by the DXGI formats,
                    // they are basically the same as these BC formats so they can be mapped
                    if (MakeFourCC('D', 'X', 'T', '2') == fourCC) {
                        return DXGI.Format.BC2_UNorm;
                    }
                    if (MakeFourCC('D', 'X', 'T', '4') == fourCC) {
                        return DXGI.Format.BC3_UNorm;
                    }

                    if (MakeFourCC('A', 'T', 'I', '1') == fourCC) {
                        return DXGI.Format.BC4_UNorm;
                    }
                    if (MakeFourCC('B', 'C', '4', 'U') == fourCC) {
                        return DXGI.Format.BC4_UNorm;
                    }
                    if (MakeFourCC('B', 'C', '4', 'S') == fourCC) {
                        return DXGI.Format.BC4_SNorm;
                    }

                    if (MakeFourCC('A', 'T', 'I', '2') == fourCC) {
                        return DXGI.Format.BC5_UNorm;
                    }
                    if (MakeFourCC('B', 'C', '5', 'U') == fourCC) {
                        return DXGI.Format.BC5_UNorm;
                    }
                    if (MakeFourCC('B', 'C', '5', 'S') == fourCC) {
                        return DXGI.Format.BC5_SNorm;
                    }

                    // BC6H and BC7 are written using the "DX10" extended header

                    if (MakeFourCC('R', 'G', 'B', 'G') == fourCC) {
                        return DXGI.Format.R8G8_B8G8_UNorm;
                    }
                    if (MakeFourCC('G', 'R', 'G', 'B') == fourCC) {
                        return DXGI.Format.G8R8_G8B8_UNorm;
                    }

                    if (MakeFourCC('Y', 'U', 'Y', '2') == fourCC) {
                        return DXGI.Format.YUY2;
                    }

                    // Check for D3DFORMAT enums being set here
                    switch (fourCC) {
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

    [Flags]
    public enum DDS_HEADER_FLAGS {
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
    public enum DDS_PIXELFORMAT_FLAGS {
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
    public enum DDS_ALPHA_MODE {
        Unknown = 0,
        Straight = 1,
        Premultiplied = 2,
        Opaque = 3,
        Custom = 4,
    };
}
