using System;
using System.IO;
using SharpDX.DXGI;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using System.Runtime.InteropServices;

namespace SharpDX.DirectXToolkit {
    public class DDS {
        // https://msdn.microsoft.com/zh-tw/library/windows/desktop/dn424129(v=vs.85).aspx
        // https://msdn.microsoft.com/en-us/library/windows/desktop/bb943991(v=vs.85).aspx
        // https://blog.csdn.net/puppet_master/article/details/50186613
        public bool IsDDS = false;
        public DDS_HEADER Header;
        public DDS_HEADER_DXT10? HeaderDXT10;
        public byte[] Data;

        public const uint CubeMap = 0x00000200;

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
            if (Header.IsDX10) {
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
            } else {
                HeaderDXT10 = null;
            }
        }

        public DDS_AlphaMode AlphaMode {
            get {
                if (Header.IsDX10) {
                    var d3d10ext = HeaderDXT10.Value;
                    var mode = d3d10ext.miscFlags2 & (DDS_AlphaMode)DDS_MISC_FLAGS2.DDS_MISC_FLAGS2_ALPHA_MODE_MASK;
                    switch (mode) {
                        case DDS_AlphaMode.Straight:
                        case DDS_AlphaMode.Premultiplied:
                        case DDS_AlphaMode.Opaque:
                        case DDS_AlphaMode.Custom:
                            return mode;
                    }
                } else if ((MakeFourCC('D', 'X', 'T', '2') == Header.ddsPixelFormat.fourCC)
                      || (MakeFourCC('D', 'X', 'T', '4') == Header.ddsPixelFormat.fourCC)) {
                    return DDS_AlphaMode.Premultiplied;
                }
                return DDS_AlphaMode.Unknown;
            }
        }

        public static uint MakeFourCC(char a, char b, char c, char d) {
            return (byte)a | (uint)(byte)b << 8 | (uint)(byte)c << 16 | (uint)(byte)d << 24;
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct DDS_HEADER {
        [FieldOffset(0)] public uint size;
        [FieldOffset(4)] public DDS_Header flags;
        [FieldOffset(8)] public uint height;
        [FieldOffset(12)] public uint width;
        [FieldOffset(16)] public uint pitchOrLinearSize;
        [FieldOffset(20)] public uint depth;
        [FieldOffset(24)] public uint mipMapCount;
        //uint[] dwReserved1;
        [FieldOffset(72)] public DDS_PIXELFORMAT ddsPixelFormat;
        [FieldOffset(104)] public DDS_Caps Caps;
        [FieldOffset(108)] public DDS_CubeMap Caps2;
        /// <summary>
        /// Unused.
        /// </summary>
        [FieldOffset(112)] private uint Caps3;
        /// <summary>
        /// Unused.
        /// </summary>
        [FieldOffset(116)] private uint Caps4;
        /// <summary>
        /// Unused.
        /// </summary>
        [FieldOffset(120)] private uint Reserved2;

        public bool IsDX10 {
            get {
                // dwFourCC == "DX10"
                return ddsPixelFormat.flags.HasFlag(DDS_PixelFormat.FourCC) && ddsPixelFormat.fourCC == 0x30315844;
            }
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct DDS_HEADER_DXT10 {
        [FieldOffset(0)] public SharpDX.DXGI.Format dxgiFormat;
        [FieldOffset(4)] public SharpDX.Direct3D11.ResourceDimension resourceDimension;
        [FieldOffset(8)] public SharpDX.Direct3D11.ResourceOptionFlags miscFlag;
        [FieldOffset(12)] public uint arraySize;
        [FieldOffset(16)] public DDS_AlphaMode miscFlags2;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct DDS_PIXELFORMAT {
        [FieldOffset(0)] public uint size;
        [FieldOffset(4)] public DDS_PixelFormat flags;
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

    public static partial class DirectXToolkit {

        public static void CreateDDSTextureFromStream(Direct3D11.Device d3dDevice, Stream stream, out Direct3D11.Resource texture, out Direct3D11.ShaderResourceView textureView) {
            if (stream.CanRead) {
                var dds = new DDS(stream);
                CreateDDSTextureFromMemory(d3dDevice, dds, out texture, out textureView);
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
        public static void CreateDDSTextureFromMemory(Direct3D11.Device d3dDevice, DDS dds, out Direct3D11.Resource texture, out Direct3D11.ShaderResourceView textureView) {
            CreateDDSTextureFromMemoryEx(d3dDevice, dds, 0, ResourceUsage.Default, BindFlags.ShaderResource, CpuAccessFlags.None, ResourceOptionFlags.None, false, out texture, out textureView, out var alphaMode);
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
            if (dds == null) throw new ArgumentNullException("dds");

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

                switch (d3d10ext.dxgiFormat) {
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
    }
}
