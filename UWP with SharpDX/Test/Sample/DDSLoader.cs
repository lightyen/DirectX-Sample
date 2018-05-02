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
        public bool IsFourCC = false;
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
                if (Header.dwSize == Marshal.SizeOf<DDS_HEADER>()) IsDDS = true;
            }
        }

        void GetDdsHeaderDXT10(Stream stream) {
            // dwFourCC == "DX10"
            if (Header.ddsPixelFormat.flags.HasFlag(DDS_PIXELFORMAT_FLAGS.FourCC) && Header.ddsPixelFormat.dwFourCC == 0x30315844) {
                IsFourCC = true;
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

        [StructLayout(LayoutKind.Explicit)]
        public struct DDS_HEADER {
            [FieldOffset(0)] public uint dwSize;
            [FieldOffset(4)] public DDS_HEADER_FLAGS flags;
            [FieldOffset(8)] public uint dwHeight;
            [FieldOffset(12)] public uint dwWidth;
            [FieldOffset(16)] public uint dwPitchOrLinearSize;
            [FieldOffset(20)] public uint dwDepth;
            [FieldOffset(24)] public uint dwMipMapCount;
            //uint[] dwReserved1;
            [FieldOffset(72)] public DDS_PIXELFORMAT ddsPixelFormat;
            [FieldOffset(104)] public uint dwCaps;
            [FieldOffset(108)] public uint dwCaps2;
            [FieldOffset(112)] public uint dwCaps3;
            [FieldOffset(116)] public uint dwCaps4;
            [FieldOffset(120)] private uint dwReserved2;
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
            [FieldOffset(0)] public uint dwSize;
            [FieldOffset(4)] public DDS_PIXELFORMAT_FLAGS flags;
            [FieldOffset(8)] public uint dwFourCC;
            [FieldOffset(12)] public uint dwRGBBitCount;
            [FieldOffset(16)] public uint dwRBitMask;
            [FieldOffset(20)] public uint dwGBitMask;
            [FieldOffset(24)] public uint dwBBitMask;
            [FieldOffset(28)] public uint dwABitMask;
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
            /// <summary>
            /// Used in some older DDS files for YUV uncompressed data (dwRGBBitCount contains the YUV bit count; dwRBitMask contains the Y mask, dwGBitMask contains the U mask, dwBBitMask contains the V mask)
            /// </summary>
            YUV = 0x200,
            /// <summary>
            /// Used in some older DDS files for single channel color uncompressed data (dwRGBBitCount contains the luminance channel bit count; dwRBitMask contains the channel mask). Can be combined with DDPF_ALPHAPIXELS for a two channel DDS file.
            /// </summary>
            Luminance = 0x20000,
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
}
