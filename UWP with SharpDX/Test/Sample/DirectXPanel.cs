using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.WIC;
using QRCoder;

namespace MyGame {
    public class DirectXPanel {

        private SwapChainPanel TargetSwapChainPanel;

        private SwapChain SwapChain;

        public Size2 SwapChainSize {
            get; private set;
        }

        private Size2F ActualSize;
        private Size2F Dpi;

        private Adapter CurrentAdapter;
        public DeviceInfo DeviceInfo {
            get; private set;
        }

        private SharpDX.Direct3D11.Device D3D11Device;
        private DeviceContext MainContext;
        private SharpDX.Direct2D1.DeviceContext D2DDeviceContext;
        private Texture2D offScreenSurface;
        private DepthStencilView depthView;
        private RenderTargetView renderTargetView;

        private VertexShader vertexShader;
        private PixelShader pixelShader;

        Stopwatch fpsTimer = new Stopwatch();
        int fpsCounter = 0;
        string fpsString = "0";

        private SharpDX.DirectWrite.TextFormat infoTextFormat;
        private SharpDX.DirectWrite.TextFormat textFormat;
        private SharpDX.Direct2D1.SolidColorBrush textBrush;

        enum RunMode {
            Immediate = 0,
            Deferred = 1,
        }

        RunMode Mode = RunMode.Deferred;

        bool TearingSupport = false; // 支援關閉垂直同步
        bool Running = true;

        InputLayout VertexLayout;

        public DirectXPanel(Size2 swapChainSize, SwapChainPanel panel) {

            SwapChainSize = swapChainSize;
            if (swapChainSize.Width * swapChainSize.Height <= 0) {
                throw new SharpDXException(Result.Fail, "DirectXPanel初始化Size有誤");
            }
            FindAdapter();
            CreateDevice();
            if (CurrentAdapter == null) {
                throw new SharpDXException(Result.Fail, "找不到適合的Adapter");
            }
            CreateSwapChain();
            SetSwapChain(panel);
            TargetSwapChainPanel = panel;
        }

        private void SetSwapChain(SwapChainPanel panel) {
            // 把Xaml的SwapChainPanel與DirectX的SwapChain連結起來
            using (ISwapChainPanelNative swapChainPanelNative = ComObject.As<ISwapChainPanelNative>(panel)) {
                swapChainPanelNative.SwapChain = SwapChain;
            }
        }

        private void FindAdapter() {
            using (var factory = new Factory1())
            using (var factory5 = factory.QueryInterface<Factory5>()) {

                // 測試看是否支援關閉垂直同步
                int allowTearing = 0;
                IntPtr data = System.Runtime.InteropServices.Marshal.AllocHGlobal(4);
                factory5.CheckFeatureSupport(SharpDX.DXGI.Feature.PresentAllowTearing, data, sizeof(int));
                allowTearing = System.Runtime.InteropServices.Marshal.PtrToStructure<int>(data);
                System.Runtime.InteropServices.Marshal.FreeHGlobal(data);
                if (allowTearing == 1) TearingSupport = true;

                // 找一個繪圖介面
                var list = factory.Adapters.ToList();
                var query = from adp in list
                            where adp.Description.VendorId == 0x10DE || adp.Description.VendorId == 0x1022 || adp.Description.VendorId == 0x8086
                            select adp;
                query.OrderBy((x) => {
                    if (x.Description.VendorId == 0x10DE) return 0;
                    if (x.Description.VendorId == 0x1022) return 1;
                    if (x.Description.VendorId == 0x8086) return 2;
                    return 3;
                });
                CurrentAdapter = query.ElementAtOrDefault(0);
            }
        }

        private void CreateDevice() {
            FeatureLevel[] featureLevels = new FeatureLevel[] {
                FeatureLevel.Level_11_1,
                FeatureLevel.Level_11_0,
            };
            DeviceCreationFlags flags = DeviceCreationFlags.BgraSupport;
#if DEBUG
            flags |= DeviceCreationFlags.Debug;
#endif
            D3D11Device = new SharpDX.Direct3D11.Device(CurrentAdapter, flags, featureLevels);

            var desc = CurrentAdapter.Description;
            DeviceInfo = new DeviceInfo {
                Description = desc.Description,
                VendorId = desc.VendorId,
                DeviceId = desc.DeviceId,
                DedicatedVideoMemory = desc.DedicatedVideoMemory,
                FeatureLevel = D3D11Device.FeatureLevel
            };
        }

        private void CreateSwapChain() {
            // https://docs.microsoft.com/en-us/windows/uwp/gaming/multisampling--multi-sample-anti-aliasing--in-windows-store-apps
            SwapChainDescription1 swapChainDescription = new SwapChainDescription1() {
                Usage = Usage.RenderTargetOutput | Usage.BackBuffer,
                BufferCount = 2,
                SwapEffect = SwapEffect.FlipSequential, // UWP 只能用 flip model
                SampleDescription = new SampleDescription(1, 0), // 在flip model下SwapChain不能開multi-sampling，只能另闢蹊徑
                Scaling = Scaling.Stretch,
                Flags = SwapChainFlags.AllowModeSwitch | SwapChainFlags.AllowTearing,
                Format = Format.R8G8B8A8_UNorm,
                Width = SwapChainSize.Width,
                Height = SwapChainSize.Height,
            };

            Dictionary<int, int> count_quality_levels = new Dictionary<int, int>();

            for (int i = 1; i <= SharpDX.Direct3D11.Device.MultisampleCountMaximum; i++) {
                var quality = D3D11Device.CheckMultisampleQualityLevels(Format.R8G8B8A8_UNorm, i);
                count_quality_levels.Add(i, quality);
            }

            D3D11Device.CheckThreadingSupport(out bool supportConcurentResources, out bool supportCommandList);

            // 建立SwapChain
            using (SharpDX.DXGI.Device3 dxgiDevice3 = D3D11Device.QueryInterface<SharpDX.DXGI.Device3>()) {
                using (Factory2 dxgiFactory2 = dxgiDevice3.Adapter.GetParent<Factory2>()) {
                    SwapChain = new SwapChain1(dxgiFactory2, D3D11Device, ref swapChainDescription);
                }
            }
        }

        private void CreateRenderTargetView(DeviceContext context) {

            offScreenSurface = new Texture2D(D3D11Device, new Texture2DDescription {
                Format = Format.R8G8B8A8_UNorm,
                Width = 1920,
                Height = 1080,
                BindFlags = BindFlags.RenderTarget,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = new SampleDescription(4, 0),
            });

            var depthBuffer = new Texture2D(D3D11Device, new Texture2DDescription {
                Format = Format.D32_Float_S8X24_UInt,
                ArraySize = 1,
                MipLevels = 1,
                Width = 1920,
                Height = 1080,
                SampleDescription = new SampleDescription(4, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.DepthStencil,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None,
            });

            depthView = new DepthStencilView(D3D11Device, depthBuffer);

            renderTargetView = new RenderTargetView(D3D11Device, offScreenSurface, new RenderTargetViewDescription {
                Format = Format.R8G8B8A8_UNorm,
                Dimension = RenderTargetViewDimension.Texture2DMultisampled,
            });

            using (var backbuffer = SwapChain.GetBackBuffer<Texture2D>(0))
            using (var Surface = backbuffer.QueryInterface<Surface2>())
            using (var dxgiDevice = D3D11Device.QueryInterface<SharpDX.DXGI.Device3>()) {
                SharpDX.Direct2D1.DebugLevel debugLevel = SharpDX.Direct2D1.DebugLevel.None;
#if DEBUG
                debugLevel |= SharpDX.Direct2D1.DebugLevel.Information;
#endif
                var factory = new SharpDX.Direct2D1.Factory2(SharpDX.Direct2D1.FactoryType.SingleThreaded, debugLevel);

                var d2ddevice = new SharpDX.Direct2D1.Device(factory, dxgiDevice);
                D2DDeviceContext = new SharpDX.Direct2D1.DeviceContext(d2ddevice, SharpDX.Direct2D1.DeviceContextOptions.None);

                Dpi = factory.DesktopDpi;

                using (var bitmap = new SharpDX.Direct2D1.Bitmap(D2DDeviceContext, Surface, new SharpDX.Direct2D1.BitmapProperties {
                    DpiX = 96.0f,
                    DpiY = 96.0f,
                    PixelFormat = new SharpDX.Direct2D1.PixelFormat(Format.Unknown, SharpDX.Direct2D1.AlphaMode.Premultiplied),
                })) {
                    // 將 D2DDeviceContext 的 Target 指向SwapChain 的 Surface
                    D2DDeviceContext.Target = bitmap;
                    D2DDeviceContext.UnitMode = SharpDX.Direct2D1.UnitMode.Pixels;
                }
            }

            PrepareD2D();

            using (var BackBuffer = SwapChain.GetBackBuffer<Texture2D>(0)) {
                renderTargetView = new RenderTargetView(D3D11Device, BackBuffer);
            }

            context.OutputMerger.SetTargets(depthView, renderTargetView);
        }

        private async Task LoadShader() {

            var VertexShaderByteCode = await LoadShaderCodeFromFile(new Uri("ms-appx:///Shader/VertexShader.cso"));
            var PixelShaderByteCode = await LoadShaderCodeFromFile(new Uri("ms-appx:///Shader/PixelShader.cso"));

            vertexShader = new VertexShader(D3D11Device, VertexShaderByteCode);
            pixelShader = new PixelShader(D3D11Device, PixelShaderByteCode);

            InputElement[] layout = new InputElement[] {
                new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 0),
                new InputElement("TEXCOORD", 0, Format.R32G32_Float, 16, 0),
            };
            VertexLayout = new InputLayout(D3D11Device, VertexShaderByteCode, layout);
        }

        public void SetView(float width, float height) {
            ActualSize = new Size2F((float)(Math.Floor(width)), (float)(Math.Floor(height)));
            SetViewport(MainContext);
        }

        public void Test() {
            using (var qrGenerator = new QRCodeGenerator()) {
                var data = qrGenerator.CreateQrCode("hello world", QRCodeGenerator.ECCLevel.Q);
                BitmapByteQRCode qrCode = new BitmapByteQRCode(data);
                byte[] qrCodeAsBitmapByteArr = qrCode.GetGraphic(30);
                Test(qrCodeAsBitmapByteArr);
            }
        }

        public void Test(byte[] data) {

            using (var memoryStream = new MemoryStream(data, false))
            using (var factory = new ImagingFactory2())
            using (var decoder = new BitmapDecoder(factory, ContainerFormatGuids.Bmp))
            using (var wicstream = new WICStream(factory, memoryStream)) {

                decoder.Initialize(wicstream, DecodeOptions.CacheOnDemand);

                using (var frame = decoder.GetFrame(0)) {
                    try {
                        CreateTextureFromWIC(D3D11Device, MainContext, frame,
                            0, ResourceUsage.Default, BindFlags.ShaderResource, CpuAccessFlags.None, ResourceOptionFlags.None, LoadFlags.Default,
                            out SharpDX.Direct3D11.Resource texture, out ShaderResourceView textureView);
                        MainContext.PixelShader.SetShaderResource(0, textureView);
                    } catch (SharpDXException e) {
                        Debug.WriteLine(e.ToString());
                    }
                }
                //Guid srcFormat = frameDecode.PixelFormat;
                //Guid desFormat = PixelFormat.Format128bppRGBAFloat;

                //if (formatCoverter.CanConvert(srcFormat, desFormat)) {
                //    formatCoverter.Initialize(frameDecode, desFormat, BitmapDitherType.None, null, 0, BitmapPaletteType.Custom);

                //    ////////........................................
                //    qrCode = SharpDX.Direct2D1.Bitmap1.FromWicBitmap(D2DDeviceContext, formatCoverter, new SharpDX.Direct2D1.BitmapProperties1() {
                //        DpiX = 96.0f,
                //        DpiY = 96.0f,
                //        PixelFormat = new SharpDX.Direct2D1.PixelFormat(Format.R32G32B32A32_Float, SharpDX.Direct2D1.AlphaMode.Premultiplied),
                //        BitmapOptions = SharpDX.Direct2D1.BitmapOptions.None
                //    });
                //}
            }
        }

        private void SetViewport(DeviceContext context) {
            if (context != null) {
                context.Rasterizer.SetViewport(0, 0, ActualSize.Width, ActualSize.Height);
            }
        }

        private void Update() {
            // 更新資料
            fpsCounter++;
            if (fpsTimer.ElapsedMilliseconds > 1000) {
                var fps = 1000.0 * fpsCounter / fpsTimer.ElapsedMilliseconds;
                fpsString = $"{fps:F1}";
                fpsCounter = 0;
                fpsTimer.Reset();
                fpsTimer.Start();
            }
        }

        private void Render() {
            if (MainContext != null) {

                // 把renderTargetView綁定到Output-Merger Stage
                MainContext.OutputMerger.SetRenderTargets(renderTargetView);

                // 初始化模板暫存區
                MainContext.ClearDepthStencilView(depthView, DepthStencilClearFlags.Depth, 1.0f, 0);
                // https://msdn.microsoft.com/en-us/library/windows/desktop/bb205120(v=vs.85).aspx

                // 填滿背景色
                MainContext.ClearRenderTargetView(renderTargetView, Color.Black);

                // 畫
                MainContext.DrawIndexed(3 * 2, 0, 0);

                if (Mode == RunMode.Deferred) {
                    // TRUE means restore the state of the device context to its pre-command list condition when recording is completed.
                    var commandList = MainContext.FinishCommandList(true);
                    D3D11Device.ImmediateContext.ExecuteCommandList(commandList, false);
                    commandList.Dispose();
                    commandList = null;
                }

                // https://docs.microsoft.com/en-us/windows/uwp/gaming/multisampling--multi-sample-anti-aliasing--in-windows-store-apps
                using (var BackBuffer = SwapChain.GetBackBuffer<Texture2D>(0)) {
                    var index = SharpDX.Direct3D11.Resource.CalculateSubResourceIndex(0, 0, 1);
                    MainContext.ResolveSubresource(offScreenSurface, index, BackBuffer, index, Format.R8G8B8A8_UNorm);
                }

                D2DDraw();

                // 把畫好的結果輸出到螢幕上！
                // syncInterval = 0 表示不同步
                SwapChain.Present(0, TearingSupport ? PresentFlags.AllowTearing : PresentFlags.None);
            }
        }

        private void PrepareD2D() {
            var writeFactory = new SharpDX.DirectWrite.Factory();
            textFormat = new SharpDX.DirectWrite.TextFormat(writeFactory, "微軟正黑體", 30.0f);
            infoTextFormat = new SharpDX.DirectWrite.TextFormat(writeFactory, "微軟正黑體", 14.0f);
            textBrush = new SharpDX.Direct2D1.SolidColorBrush(D2DDeviceContext, Color.LightGreen);
        }

        private void D2DDraw() {
            D2DDeviceContext.BeginDraw();
            D2DDeviceContext.DrawText(fpsString, textFormat, new RectangleF(0, ActualSize.Height - 32, 80, 32), textBrush);
            D2DDeviceContext.DrawText(DeviceInfo.ToString(), infoTextFormat, new RectangleF(0, 0, 250, 100), textBrush);
            D2DDeviceContext.EndDraw();
        }

        private async Task LoadResource() {
            await LoadShader();
        }

        void PreparePipeline(DeviceContext context) {

            context.InputAssembler.InputLayout = VertexLayout;
            context.VertexShader.Set(vertexShader);
            context.PixelShader.Set(pixelShader);

            float width = 1920.0f;
            float height = 1080.0f;

            var vertices = new SimpleVertex[] {
                new SimpleVertex { Position = new Vector4(0.0f, height * 0.6f, 1.0f, 1.0f), TexCoord = new Vector2(0.0f, 0.0f)},

                new SimpleVertex { Position = new Vector4(0.0f, 0.0f, 1.0f, 1.0f), TexCoord = new Vector2(1.0f, 0.0f)},

                new SimpleVertex { Position = new Vector4(width * 0.6f, 0.0f, 1.0f, 1.0f), TexCoord = new Vector2(1.0f, 1.0f)},

                new SimpleVertex { Position = new Vector4(width * 0.6f, height * 0.6f, 1.0f, 1.0f), TexCoord = new Vector2(0.0f, 1.0f)},
            };

            // CreateBuffer 在記憶體
            var vertexBuffer = SharpDX.Direct3D11.Buffer.Create(D3D11Device, BindFlags.VertexBuffer, vertices);

            // SetVertexBuffers
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(vertexBuffer, Utilities.SizeOf<SimpleVertex>(), 0));


            uint[] indices = new uint[] { 0, 2, 1, 0, 3, 2 };
            var indexBuffer = SharpDX.Direct3D11.Buffer.Create(D3D11Device, BindFlags.IndexBuffer, indices);
            context.InputAssembler.SetIndexBuffer(indexBuffer, Format.R32_UInt, 0);

            var staticContantBuffer = new SharpDX.Direct3D11.Buffer(D3D11Device, Utilities.SizeOf<Matrix>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            context.VertexShader.SetConstantBuffer(0, staticContantBuffer);

            var mat = Matrix.Translation(-width * 0.6f / 2.0f, -height * 0.6f / 2.0f, 0.0f) * Matrix.OrthoLH(width, height, -1.0f, 1.0f);
            context.UpdateSubresource(ref mat, staticContantBuffer);

            var sampDesc = new SamplerStateDescription() {
                Filter = Filter.MinMagMipLinear,
                AddressU = TextureAddressMode.Wrap,
                AddressV = TextureAddressMode.Wrap,
                AddressW = TextureAddressMode.Wrap,
                ComparisonFunction = Comparison.Never,
                MinimumLod = 0.0f,
                MaximumLod = 0.0f,
            };

            var samplerLinear = new SamplerState(D3D11Device, sampDesc);
            context.PixelShader.SetSampler(0, samplerLinear);

            Test();

            // Set primitive topology
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;

            fpsTimer.Start();
        }

        public async Task Start() {

            await LoadResource();

            DeviceContext deferredContext;
            if (Mode == RunMode.Deferred) {
                deferredContext = new DeviceContext(D3D11Device);
                CreateRenderTargetView(deferredContext);
                SetViewport(deferredContext);
                PreparePipeline(deferredContext);
                MainContext = deferredContext;
            } else {
                CreateRenderTargetView(D3D11Device.ImmediateContext);
                SetViewport(D3D11Device.ImmediateContext);
                PreparePipeline(D3D11Device.ImmediateContext);
                MainContext = D3D11Device.ImmediateContext;
            }


            while (Running) {
                Update();
                Render();
            }
        }

        public void Stop() {
            Running = false;
        }

        private async Task<byte[]> LoadShaderCodeFromFile(Uri uri) {

            byte[] code = null;

            var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
            if (file != null) {
                using (var stream = await file.OpenStreamForReadAsync()) {
                    code = new byte[(int)stream.Length];
                    stream.Read(code, 0, (int)stream.Length);
                }
            }
            return code;
        }

        public Result CreateTextureFromWIC(SharpDX.Direct3D11.Device device, DeviceContext deviceContext,
        BitmapFrameDecode frame, int maxsize,
        ResourceUsage usage, BindFlags bind, CpuAccessFlags cpuAccess, ResourceOptionFlags option, LoadFlags load,
        out SharpDX.Direct3D11.Resource texture, out ShaderResourceView textureView) {
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
                        maxsize = SharpDX.Direct3D11.Resource.MaximumTexture2DSize; /*D3D11_REQ_TEXTURE2D_U_OR_V_DIMENSION*/
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

            if (format == Format.R32G32B32_Float && deviceContext != null && textureView != null) {
                // Special case test for optional device support for autogen mipchains for R32G32B32_FLOAT 

                var formatSupport = device.CheckFormatSupport(Format.R32G32B32_Float);

                if (!formatSupport.HasFlag(FormatSupport.MipAutogen)) {
                    targetFormat = PixelFormat.Format128bppRGBAFloat;
                    format = Format.R32G32B32A32_Float;
                    bpp = 128;
                }
            }

            if (bpp == 0) return Result.Fail;

            if (load.HasFlag(LoadFlags.ForceSrgb)) {
                format = format.MakeSRgb();
            } else if (!load.HasFlag(LoadFlags.ignoreSrgb)) {

                //try {
                //    var metareader = frame.MetadataQueryReader;
                //    var containerFormat = metareader.ContainerFormat;

                //    // Check for sRGB colorspace metadata
                //    bool sRGB = false;
                //    if (metareader.GetMetadataByName("/sRGB/RenderingIntent") != null) {
                //        sRGB = true;
                //    } else if (metareader.GetMetadataByName("System.Image.ColorSpace") != null) {
                //        sRGB = true;
                //    }

                //    if (sRGB)
                //        format = format.MakeSRgb();
                //} catch (SharpDXException e) {
                //    Debug.WriteLine(e.ToString());
                //}

                

                
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

            //byte[] test = new byte[imageSize];
            //System.Runtime.InteropServices.Marshal.Copy(temp, test, 0, imageSize);

            bool autogen = false;
            if (deviceContext != null && textureView != null) // Must have context and shader-view to auto generate mipmaps
            {
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

            var SRVDesc = new ShaderResourceViewDescription() {
                Format = format,
                Dimension = ShaderResourceViewDimension.Texture2D,
                Texture2D = new ShaderResourceViewDescription.Texture2DResource() { MipLevels = autogen ? -1 : 1 },
            };

            textureView = new ShaderResourceView(device, texture, SRVDesc);

            System.Runtime.InteropServices.Marshal.FreeCoTaskMem(temp);

            return Result.Ok;
        }
    }
    public class DeviceInfo {
        public FeatureLevel FeatureLevel;
        public PointerSize DedicatedVideoMemory;
        public int VendorId;
        public int DeviceId;
        public string Description;

        public string Vender {
            get {
                switch (VendorId) {
                    case 0x8086:
                        return "Intel";
                    case 0x10DE:
                        return "NVIDIA";
                    case 0x1022:
                        return "AMD";
                    default:
                        return $"{VendorId:X4}";
                }
            }
        }

        public string D3DVersion {
            get {
                switch (FeatureLevel) {
                    case SharpDX.Direct3D.FeatureLevel.Level_12_1:
                        return "DirectX 12.1";
                    case SharpDX.Direct3D.FeatureLevel.Level_12_0:
                        return "DirectX 12.0";
                    case SharpDX.Direct3D.FeatureLevel.Level_11_1:
                        return "DirectX 11.1";
                    case SharpDX.Direct3D.FeatureLevel.Level_11_0:
                        return "DirectX 11.0";
                    case SharpDX.Direct3D.FeatureLevel.Level_10_1:
                        return "DirectX 11.1";
                    case SharpDX.Direct3D.FeatureLevel.Level_10_0:
                        return "DirectX 10.0";
                    case SharpDX.Direct3D.FeatureLevel.Level_9_3:
                        return "DirectX 9.3";
                    case SharpDX.Direct3D.FeatureLevel.Level_9_2:
                        return "DirectX 9.2";
                    case SharpDX.Direct3D.FeatureLevel.Level_9_1:
                        return "DirectX 9.1";
                    default:
                        return "DirectX Not Support";
                }
            }
        }

        public override string ToString() {

            StringBuilder sb = new StringBuilder();
            sb.Append($"{D3DVersion}\n");
            sb.Append($"{Vender}\n");
            sb.Append($"{Description}\n");
            sb.Append(string.Format(new PointerSizeFormat(), "{0}", DedicatedVideoMemory));
            return sb.ToString();
        }
    }

    public class PointerSizeFormat : IFormatProvider, ICustomFormatter {
        public object GetFormat(Type formatType) {
            if (formatType == typeof(ICustomFormatter))
                return this;
            else
                return null;
        }

        public string Format(string format, object arg, IFormatProvider formatProvider) {
            if (arg is PointerSize size) {
                StringBuilder sb = new StringBuilder();
                if (size < (1 << 10)) {
                    sb.Append($"{size} bytes");
                } else if (size < (1 << 20)) {
                    sb.Append($"{size / (1 << 10)} KB");
                } else if (size < (1 << 30)) {
                    sb.Append($"{size / (1 << 20)} MB");
                } else {
                    sb.Append($"{size / (1 << 30)} GB");
                }
                return sb.ToString();
            } else {
                throw new FormatException(String.Format("The format of '{0}' is invalid.", format));
            }
        }
    }
}
