using System;
using SharpDX.DXGI;
using SharpDX.WIC;

namespace DirectXToolkit {

    [Flags]
    public enum LoadFlags {
        Default,
        ForceSrgb,
        ignoreSrgb,
    }

    public static class Extensions {

        //-------------------------------------------------------------------------------------
        // WIC Pixel Format Translation Data
        //-------------------------------------------------------------------------------------
        static WICTranslate[] WICFormats = {
            new WICTranslate(PixelFormat.Format128bppRGBAFloat, Format.R32G32B32A32_Float),
            new WICTranslate(PixelFormat.Format64bppRGBAHalf, Format.R16G16B16A16_Float),
            new WICTranslate(PixelFormat.Format64bppRGBA, Format.R16G16B16A16_UNorm),

            new WICTranslate(PixelFormat.Format32bppRGBA, Format.R8G8B8A8_UNorm),
            new WICTranslate(PixelFormat.Format32bppBGRA, Format.B8G8R8A8_UNorm),
            new WICTranslate(PixelFormat.Format32bppBGR, Format.B8G8R8X8_UNorm),

            new WICTranslate(PixelFormat.Format32bppRGBA1010102XR, Format.R10G10B10_Xr_Bias_A2_UNorm),
            new WICTranslate(PixelFormat.Format32bppRGBA1010102, Format.R10G10B10A2_UNorm),
            new WICTranslate(PixelFormat.Format32bppRGBE, Format.R9G9B9E5_Sharedexp),

            new WICTranslate(PixelFormat.Format16bppBGRA5551, Format.B5G5R5A1_UNorm),
            new WICTranslate(PixelFormat.Format16bppBGR565, Format.B5G6R5_UNorm),

            new WICTranslate(PixelFormat.Format32bppGrayFloat, Format.R32_Float),
            new WICTranslate(PixelFormat.Format16bppGrayHalf, Format.R16_Float),
            new WICTranslate(PixelFormat.Format16bppGray, Format.R16_UNorm),
            new WICTranslate(PixelFormat.Format8bppGray, Format.R8_UNorm),
            new WICTranslate(PixelFormat.Format8bppAlpha, Format.A8_UNorm),

            new WICTranslate(PixelFormat.Format96bppRGBFloat, Format.R32G32B32_Float),
        };

        //-------------------------------------------------------------------------------------
        // WIC Pixel Format nearest conversion table
        //-------------------------------------------------------------------------------------
        static WICConvert[] WICConverts = {
            new WICConvert(PixelFormat.FormatBlackWhite, PixelFormat.Format8bppGray),

            new WICConvert(PixelFormat.Format1bppIndexed, PixelFormat.Format32bppRGBA),
            new WICConvert(PixelFormat.Format2bppIndexed, PixelFormat.Format32bppRGBA),
            new WICConvert(PixelFormat.Format4bppIndexed, PixelFormat.Format32bppRGBA),
            new WICConvert(PixelFormat.Format8bppIndexed, PixelFormat.Format32bppRGBA),

            new WICConvert(PixelFormat.Format2bppGray, PixelFormat.Format8bppGray),
            new WICConvert(PixelFormat.Format4bppGray, PixelFormat.Format8bppGray),

            new WICConvert(PixelFormat.Format16bppGrayFixedPoint, PixelFormat.Format16bppGrayHalf),
            new WICConvert(PixelFormat.Format32bppGrayFixedPoint, PixelFormat.Format32bppGrayFloat),

            new WICConvert(PixelFormat.Format16bppBGR555, PixelFormat.Format16bppBGRA5551),

            new WICConvert(PixelFormat.Format32bppBGR101010, PixelFormat.Format32bppRGBA1010102),

            new WICConvert(PixelFormat.Format24bppBGR, PixelFormat.Format32bppRGBA),
            new WICConvert(PixelFormat.Format24bppRGB, PixelFormat.Format32bppRGBA),
            new WICConvert(PixelFormat.Format32bppPBGRA, PixelFormat.Format32bppRGBA),
            new WICConvert(PixelFormat.Format32bppPRGBA, PixelFormat.Format32bppRGBA),

            new WICConvert(PixelFormat.Format48bppRGB, PixelFormat.Format64bppRGBA),
            new WICConvert(PixelFormat.Format48bppBGR, PixelFormat.Format64bppRGBA),
            new WICConvert(PixelFormat.Format64bppBGRA, PixelFormat.Format64bppRGBA),
            new WICConvert(PixelFormat.Format64bppPRGBA, PixelFormat.Format64bppRGBA),
            new WICConvert(PixelFormat.Format64bppPBGRA, PixelFormat.Format64bppRGBA),

            new WICConvert(PixelFormat.Format48bppRGBFixedPoint, PixelFormat.Format64bppRGBAHalf),
            new WICConvert(PixelFormat.Format48bppBGRFixedPoint, PixelFormat.Format64bppRGBAHalf),
            new WICConvert(PixelFormat.Format64bppRGBAFixedPoint, PixelFormat.Format64bppRGBAHalf),
            new WICConvert(PixelFormat.Format64bppBGRAFixedPoint, PixelFormat.Format64bppRGBAHalf),
            new WICConvert(PixelFormat.Format64bppRGBFixedPoint, PixelFormat.Format64bppRGBAHalf),
            new WICConvert(PixelFormat.Format64bppRGBHalf, PixelFormat.Format64bppRGBAHalf),
            new WICConvert(PixelFormat.Format48bppRGBHalf, PixelFormat.Format64bppRGBAHalf),

            new WICConvert(PixelFormat.Format128bppPRGBAFloat, PixelFormat.Format128bppRGBAFloat),
            new WICConvert(PixelFormat.Format128bppRGBFloat, PixelFormat.Format128bppRGBAFloat),
            new WICConvert(PixelFormat.Format128bppRGBAFixedPoint, PixelFormat.Format128bppRGBAFloat),
            new WICConvert(PixelFormat.Format128bppRGBFixedPoint, PixelFormat.Format128bppRGBAFloat),
            new WICConvert(PixelFormat.Format32bppRGBE, PixelFormat.Format128bppRGBAFloat),

            new WICConvert(PixelFormat.Format32bppCMYK, PixelFormat.Format32bppRGBA),
            new WICConvert(PixelFormat.Format64bppCMYK, PixelFormat.Format64bppRGBA),
            new WICConvert(PixelFormat.Format40bppCMYKAlpha, PixelFormat.Format32bppRGBA),
            new WICConvert(PixelFormat.Format80bppCMYKAlpha, PixelFormat.Format64bppRGBA),

            new WICConvert(PixelFormat.Format32bppRGB, PixelFormat.Format32bppRGBA),
            new WICConvert(PixelFormat.Format64bppRGB, PixelFormat.Format64bppRGBA),
            new WICConvert(PixelFormat.Format64bppPRGBAHalf, PixelFormat.Format64bppRGBAHalf),
        };


        public static Format ConvertWICToDXGIFormat(this Guid WICFormat) {
            for (int i = 0; i < WICFormats.Length; i++) {
                if (WICFormat == WICFormats[i].PixelFormat) return WICFormats[i].Format;
            }
            return Format.Unknown;
        }

        public static Guid ConvertDXGIToWICFormat(this Format DXGIFormat) {
            Guid target = Guid.Empty;
            switch (DXGIFormat) {
                case Format.R32G32B32A32_Float: target = PixelFormat.Format128bppRGBAFloat; break;
                case Format.R16G16B16A16_Float: target = PixelFormat.Format64bppRGBAHalf; break;
                case Format.R16G16B16A16_UNorm: target = PixelFormat.Format64bppRGBA; break;
                case Format.R10G10B10_Xr_Bias_A2_UNorm: target = PixelFormat.Format32bppRGBA1010102XR; break;
                case Format.R10G10B10A2_UNorm: target = PixelFormat.Format32bppRGBA1010102; break;
                case Format.B5G5R5A1_UNorm: target = PixelFormat.Format16bppBGRA5551; break;
                case Format.B5G6R5_UNorm: target = PixelFormat.Format16bppBGR565; break;
                case Format.R32_Float: target = PixelFormat.Format32bppGrayFloat; break;
                case Format.R16_Float: target = PixelFormat.Format16bppGrayHalf; break;
                case Format.R16_UNorm: target = PixelFormat.Format16bppGray; break;
                case Format.R8_UNorm: target = PixelFormat.Format8bppGray; break;
                case Format.A8_UNorm: target = PixelFormat.Format8bppAlpha; break;
                case Format.R8G8B8A8_UNorm: target = PixelFormat.Format32bppRGBA; break;
                case Format.R8G8B8A8_UNorm_SRgb: target = PixelFormat.Format32bppRGBA; break;
                case Format.B8G8R8A8_UNorm: target = PixelFormat.Format32bppBGRA; break;
                case Format.B8G8R8A8_UNorm_SRgb: target = PixelFormat.Format32bppBGRA; break;
                case Format.B8G8R8X8_UNorm: target = PixelFormat.Format32bppBGR; break;
                case Format.B8G8R8X8_UNorm_SRgb: target = PixelFormat.Format32bppBGR; break;
            }
            return target;
        }

        public static Guid ConvertToNearest(this Guid source) {
            for (int i = 0; i < WICConverts.Length; i++) {
                if (source == WICConverts[i].SourceFormat) return WICConverts[i].TargetFormat;
            }
            return source;
        }

        public static Format MakeSRgb(this Format format) {
            switch (format) {
                case Format.R8G8B8A8_UNorm:
                    return Format.R8G8B8A8_UNorm_SRgb;
                case Format.BC1_UNorm:
                    return Format.BC1_UNorm_SRgb;
                case Format.BC2_UNorm:
                    return Format.BC2_UNorm_SRgb;
                case Format.BC3_UNorm:
                    return Format.BC3_UNorm_SRgb;
                case Format.B8G8R8A8_UNorm:
                    return Format.B8G8R8A8_UNorm_SRgb;
                case Format.B8G8R8X8_UNorm:
                    return Format.B8G8R8X8_UNorm_SRgb;
                case Format.BC7_UNorm:
                    return Format.BC7_UNorm_SRgb;
                default:
                    return format;
            }
        }

        public static int BitPerPixel(this Format format) {
            switch (format) {
                case Format.R32G32B32A32_Typeless:
                case Format.R32G32B32A32_Float:
                case Format.R32G32B32A32_UInt:
                case Format.R32G32B32A32_SInt:
                    return 128;
                case Format.R32G32B32_Typeless:
                case Format.R32G32B32_Float:
                case Format.R32G32B32_UInt:
                case Format.R32G32B32_SInt:
                    return 96;
                case Format.R16G16B16A16_Typeless:
                case Format.R16G16B16A16_Float:
                case Format.R16G16B16A16_UNorm:
                case Format.R16G16B16A16_UInt:
                case Format.R16G16B16A16_SNorm:
                case Format.R16G16B16A16_SInt:
                case Format.R32G32_Typeless:
                case Format.R32G32_Float:
                case Format.R32G32_UInt:
                case Format.R32G32_SInt:
                case Format.R32G8X24_Typeless:
                case Format.D32_Float_S8X24_UInt:
                case Format.R32_Float_X8X24_Typeless:
                case Format.X32_Typeless_G8X24_UInt:
                case Format.Y416:
                case Format.Y210:
                case Format.Y216:
                    return 64;
                case Format.R10G10B10A2_Typeless:
                case Format.R10G10B10A2_UNorm:
                case Format.R10G10B10A2_UInt:
                case Format.R11G11B10_Float:
                case Format.R8G8B8A8_Typeless:
                case Format.R8G8B8A8_UNorm:
                case Format.R8G8B8A8_UNorm_SRgb:
                case Format.R8G8B8A8_UInt:
                case Format.R8G8B8A8_SNorm:
                case Format.R8G8B8A8_SInt:
                case Format.R16G16_Typeless:
                case Format.R16G16_Float:
                case Format.R16G16_UNorm:
                case Format.R16G16_UInt:
                case Format.R16G16_SNorm:
                case Format.R16G16_SInt:
                case Format.R32_Typeless:
                case Format.D32_Float:
                case Format.R32_Float:
                case Format.R32_UInt:
                case Format.R32_SInt:
                case Format.R24G8_Typeless:
                case Format.D24_UNorm_S8_UInt:
                case Format.R24_UNorm_X8_Typeless:
                case Format.X24_Typeless_G8_UInt:
                case Format.R9G9B9E5_Sharedexp:
                case Format.R8G8_B8G8_UNorm:
                case Format.G8R8_G8B8_UNorm:
                case Format.B8G8R8A8_UNorm:
                case Format.B8G8R8X8_UNorm:
                case Format.R10G10B10_Xr_Bias_A2_UNorm:
                case Format.B8G8R8A8_Typeless:
                case Format.B8G8R8A8_UNorm_SRgb:
                case Format.B8G8R8X8_Typeless:
                case Format.B8G8R8X8_UNorm_SRgb:
                case Format.AYUV:
                case Format.Y410:
                case Format.YUY2:
                    return 32;
                case Format.P010:
                case Format.P016:
                    return 24;
                case Format.R8G8_Typeless:
                case Format.R8G8_UNorm:
                case Format.R8G8_UInt:
                case Format.R8G8_SNorm:
                case Format.R8G8_SInt:
                case Format.R16_Typeless:
                case Format.R16_Float:
                case Format.D16_UNorm:
                case Format.R16_UNorm:
                case Format.R16_UInt:
                case Format.R16_SNorm:
                case Format.R16_SInt:
                case Format.B5G6R5_UNorm:
                case Format.B5G5R5A1_UNorm:
                case Format.A8P8:
                case Format.B4G4R4A4_UNorm:
                    return 16;
                case Format.NV12:
                case Format.Opaque420:
                case Format.NV11:
                    return 12;
                case Format.R8_Typeless:
                case Format.R8_UNorm:
                case Format.R8_UInt:
                case Format.R8_SNorm:
                case Format.R8_SInt:
                case Format.A8_UNorm:
                case Format.AI44:
                case Format.IA44:
                case Format.P8:
                    return 8;
                case Format.R1_UNorm:
                    return 1;
                case Format.BC1_Typeless:
                case Format.BC1_UNorm:
                case Format.BC1_UNorm_SRgb:
                case Format.BC4_Typeless:
                case Format.BC4_UNorm:
                case Format.BC4_SNorm:
                    return 4;
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
                    return 8;
                default:
                    return 0;
            }
        }

        public static string GetFormatName(this Guid Format) {
            if (Format == PixelFormat.FormatDontCare) return "FormatDontCare";
            if (Format == PixelFormat.Format96bpp6Channels) return "Format96bpp6Channels";
            if (Format == PixelFormat.Format80bpp5Channels) return "Format80bpp5Channels";
            if (Format == PixelFormat.Format64bpp4Channels) return "Format64bpp4Channels";
            if (Format == PixelFormat.Format48bpp3Channels) return "Format48bpp3Channels";
            if (Format == PixelFormat.Format64bpp8Channels) return "Format64bpp8Channels";
            if (Format == PixelFormat.Format56bpp7Channels) return "Format56bpp7Channels";
            if (Format == PixelFormat.Format48bpp6Channels) return "Format48bpp6Channels";
            if (Format == PixelFormat.Format40bpp5Channels) return "Format40bpp5Channels";
            if (Format == PixelFormat.Format112bpp7Channels) return "Format112bpp7Channels";
            if (Format == PixelFormat.Format32bpp4Channels) return "Format32bpp4Channels";
            if (Format == PixelFormat.Format64bppCMYK) return "Format64bppCMYK";
            if (Format == PixelFormat.Format32bppRGBA1010102XR) return "Format32bppRGBA1010102XR";
            if (Format == PixelFormat.Format32bppRGBA1010102) return "Format32bppRGBA1010102";
            if (Format == PixelFormat.Format32bppGrayFixedPoint) return "Format32bppGrayFixedPoint";
            if (Format == PixelFormat.Format16bppGrayHalf) return "Format16bppGrayHalf";
            if (Format == PixelFormat.Format32bppRGBE) return "Format32bppRGBE";
            if (Format == PixelFormat.Format48bppRGBHalf) return "Format48bppRGBHalf";
            if (Format == PixelFormat.Format64bppRGBHalf) return "Format64bppRGBHalf";
            if (Format == PixelFormat.Format24bpp3Channels) return "Format24bpp3Channels";
            if (Format == PixelFormat.Format128bpp8Channels) return "Format128bpp8Channels";
            if (Format == PixelFormat.Format40bppCMYKAlpha) return "Format40bppCMYKAlpha";
            if (Format == PixelFormat.Format80bppCMYKAlpha) return "Format80bppCMYKAlpha";
            if (Format == PixelFormat.Format16bppCrQuantizedDctCoefficients) return "Format16bppCrQuantizedDctCoefficients";
            if (Format == PixelFormat.Format16bppCbQuantizedDctCoefficients) return "Format16bppCbQuantizedDctCoefficients";
            if (Format == PixelFormat.Format16bppYQuantizedDctCoefficients) return "Format16bppYQuantizedDctCoefficients";
            if (Format == PixelFormat.Format16bppCbCr) return "Format16bppCbCr";
            if (Format == PixelFormat.Format8bppCr) return "Format8bppCr";
            if (Format == PixelFormat.Format8bppCb) return "Format8bppCb";
            if (Format == PixelFormat.Format8bppY) return "Format8bppY";
            if (Format == PixelFormat.Format144bpp8ChannelsAlpha) return "Format144bpp8ChannelsAlpha";
            if (Format == PixelFormat.Format128bpp7ChannelsAlpha) return "Format128bpp7ChannelsAlpha";
            if (Format == PixelFormat.Format112bpp6ChannelsAlpha) return "Format112bpp6ChannelsAlpha";
            if (Format == PixelFormat.Format96bpp5ChannelsAlpha) return "Format96bpp5ChannelsAlpha";
            if (Format == PixelFormat.Format80bpp4ChannelsAlpha) return "Format80bpp4ChannelsAlpha";
            if (Format == PixelFormat.Format64bpp3ChannelsAlpha) return "Format64bpp3ChannelsAlpha";
            if (Format == PixelFormat.Format72bpp8ChannelsAlpha) return "Format72bpp8ChannelsAlpha";
            if (Format == PixelFormat.Format64bpp7ChannelsAlpha) return "Format64bpp7ChannelsAlpha";
            if (Format == PixelFormat.Format56bpp6ChannelsAlpha) return "Format56bpp6ChannelsAlpha";
            if (Format == PixelFormat.Format48bpp5ChannelsAlpha) return "Format48bpp5ChannelsAlpha";
            if (Format == PixelFormat.Format40bpp4ChannelsAlpha) return "Format40bpp4ChannelsAlpha";
            if (Format == PixelFormat.Format32bpp3ChannelsAlpha) return "Format32bpp3ChannelsAlpha";
            if (Format == PixelFormat.Format64bppPRGBAHalf) return "Format64bppPRGBAHalf";
            if (Format == PixelFormat.Format128bppRGBFixedPoint) return "Format128bppRGBFixedPoint";
            if (Format == PixelFormat.Format64bppRGBAHalf) return "Format64bppRGBAHalf";
            if (Format == PixelFormat.Format32bppRGBA) return "Format32bppRGBA";
            if (Format == PixelFormat.Format32bppRGB) return "Format32bppRGB";
            if (Format == PixelFormat.Format32bppGrayFloat) return "Format32bppGrayFloat";
            if (Format == PixelFormat.Format32bppPBGRA) return "Format32bppPBGRA";
            if (Format == PixelFormat.Format32bppBGRA) return "Format32bppBGRA";
            if (Format == PixelFormat.Format32bppBGR) return "Format32bppBGR";
            if (Format == PixelFormat.Format24bppRGB) return "Format24bppRGB";
            if (Format == PixelFormat.Format24bppBGR) return "Format24bppBGR";
            if (Format == PixelFormat.Format16bppGray) return "Format16bppGray";
            if (Format == PixelFormat.Format128bppRGBAFixedPoint) return "Format128bppRGBAFixedPoint";
            if (Format == PixelFormat.Format16bppBGRA5551) return "Format16bppBGRA5551";
            if (Format == PixelFormat.Format16bppBGR555) return "Format16bppBGR555";
            if (Format == PixelFormat.Format8bppAlpha) return "Format8bppAlpha";
            if (Format == PixelFormat.Format8bppGray) return "Format8bppGray";
            if (Format == PixelFormat.Format4bppGray) return "Format4bppGray";
            if (Format == PixelFormat.Format2bppGray) return "Format2bppGray";
            if (Format == PixelFormat.FormatBlackWhite) return "FormatBlackWhite";
            if (Format == PixelFormat.Format8bppIndexed) return "Format8bppIndexed";
            if (Format == PixelFormat.Format4bppIndexed) return "Format4bppIndexed";
            if (Format == PixelFormat.Format16bppBGR565) return "Format16bppBGR565";
            if (Format == PixelFormat.Format2bppIndexed) return "Format2bppIndexed";
            if (Format == PixelFormat.Format32bppPRGBA) return "Format32bppPRGBA";
            if (Format == PixelFormat.Format48bppBGR) return "Format48bppBGR";
            if (Format == PixelFormat.Format64bppRGBFixedPoint) return "Format64bppRGBFixedPoint";
            if (Format == PixelFormat.Format64bppBGRAFixedPoint) return "Format64bppBGRAFixedPoint";
            if (Format == PixelFormat.Format64bppRGBAFixedPoint) return "Format64bppRGBAFixedPoint";
            if (Format == PixelFormat.Format32bppCMYK) return "Format32bppCMYK";
            if (Format == PixelFormat.Format128bppRGBFloat) return "Format128bppRGBFloat";
            if (Format == PixelFormat.Format128bppPRGBAFloat) return "Format128bppPRGBAFloat";
            if (Format == PixelFormat.Format128bppRGBAFloat) return "Format128bppRGBAFloat";
            if (Format == PixelFormat.Format96bppRGBFloat) return "Format96bppRGBFloat";
            if (Format == PixelFormat.Format48bppRGB) return "Format48bppRGB";
            if (Format == PixelFormat.Format96bppRGBFixedPoint) return "Format96bppRGBFixedPoint";
            if (Format == PixelFormat.Format48bppRGBFixedPoint) return "Format48bppRGBFixedPoint";
            if (Format == PixelFormat.Format32bppBGR101010) return "Format32bppBGR101010";
            if (Format == PixelFormat.Format16bppGrayFixedPoint) return "Format16bppGrayFixedPoint";
            if (Format == PixelFormat.Format64bppPBGRA) return "Format64bppPBGRA";
            if (Format == PixelFormat.Format64bppPRGBA) return "Format64bppPRGBA";
            if (Format == PixelFormat.Format64bppBGRA) return "Format64bppBGRA";
            if (Format == PixelFormat.Format64bppRGBA) return "Format64bppRGBA";
            if (Format == PixelFormat.Format64bppRGB) return "Format64bppRGB";
            if (Format == PixelFormat.Format48bppBGRFixedPoint) return "Format48bppBGRFixedPoint";
            if (Format == PixelFormat.Format1bppIndexed) return "Format1bppIndexed";

            if (Format == ContainerFormatGuids.Jpeg) return "JPEG";
            if (Format == ContainerFormatGuids.Png) return "PNG";
            if (Format == ContainerFormatGuids.Dds) return "DDS";
            if (Format == ContainerFormatGuids.Bmp) return "BMP";
            if (Format == ContainerFormatGuids.Gif) return "GIF";
            if (Format == ContainerFormatGuids.Tiff) return "TIFF";
            if (Format == ContainerFormatGuids.Ico) return "ICO";
            if (Format == ContainerFormatGuids.Wmp) return "WMP";
            if (Format == ContainerFormatGuids.Adng) return "ADNG";
            return "unknown format";
        }

        public class WICTranslate {
            public WICTranslate(Guid pixelFormat, Format format) {
                PixelFormat = pixelFormat;
                Format = format;
            }
            public Guid PixelFormat { get; private set; }
            public Format Format { get; private set; }
        }

        public class WICConvert {
            public WICConvert(Guid source, Guid target) {
                SourceFormat = source;
                TargetFormat = target;
            }
            public Guid SourceFormat { get; private set; }
            public Guid TargetFormat { get; private set; }
        }

        public static Format EnsureNotTypeless(this Format format) {
            switch (format) {
                case Format.R32G32B32A32_Typeless: return Format.R32G32B32A32_Float;
                case Format.R32G32B32_Typeless: return Format.R32G32B32_Float;
                case Format.R16G16B16A16_Typeless: return Format.R16G16B16A16_UNorm;
                case Format.R32G32_Typeless: return Format.R32G32_Float;
                case Format.R10G10B10A2_Typeless: return Format.R10G10B10A2_UNorm;
                case Format.R8G8B8A8_Typeless: return Format.R8G8B8A8_UNorm;
                case Format.R16G16_Typeless: return Format.R16G16_UNorm;
                case Format.R32_Typeless: return Format.R32_Float;
                case Format.R8G8_Typeless: return Format.R8G8_UNorm;
                case Format.R16_Typeless: return Format.R16_UNorm;
                case Format.R8_Typeless: return Format.R8_UNorm;
                case Format.BC1_Typeless: return Format.BC1_UNorm;
                case Format.BC2_Typeless: return Format.BC2_UNorm;
                case Format.BC3_Typeless: return Format.BC3_UNorm;
                case Format.BC4_Typeless: return Format.BC4_UNorm;
                case Format.BC5_Typeless: return Format.BC5_UNorm;
                case Format.B8G8R8A8_Typeless: return Format.B8G8R8A8_UNorm;
                case Format.B8G8R8X8_Typeless: return Format.B8G8R8X8_UNorm;
                case Format.BC7_Typeless: return Format.BC7_UNorm;
                default: return format;
            }
        }
    }
}


