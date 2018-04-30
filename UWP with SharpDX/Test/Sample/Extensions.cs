using System;
using SharpDX.DXGI;

namespace SharpDX.WIC {

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


        public static Format ConvertToDXGIFormat(this Guid WICFormat) {
            for (int i = 0; i < WICFormats.Length; i++) {
                if (WICFormat == WICFormats[i].PixelFormat) return WICFormats[i].Format;
            }
            return Format.Unknown;
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
    }
}


