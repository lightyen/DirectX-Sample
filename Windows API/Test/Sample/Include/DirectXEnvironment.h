#pragma once
#include <chrono>
using namespace std::chrono;
#include <windows.h>
#include <vector>
#include <wrl\client.h>
using namespace Microsoft::WRL;
#include "DirectX.h"
using namespace DirectX;
#include <wincodec.h>
#include "DeviceInfo.h"
#include "SimpleVertex.h"
#include "Shader.h"
#include "registry.h"
#include "WICTextureLoader.h"

#define CHECKRETURN(a,b) if (CheckFailed(a,b)) { \
	return; \
}

namespace MyGame {

	
	class DirectXPanel {

		public:
		DirectXPanel() {
		HRESULT hr;
		// 在此線程初始化 COM 組件調用模式，並且設定同步/非同步類型
		hr = CoInitializeEx(nullptr, COINITBASE_MULTITHREADED);
		CHECKRETURN(hr, TEXT("CoInitialize"));

		hr = CoCreateInstance(
			CLSID_WICImagingFactory,
			NULL,
			CLSCTX_INPROC_SERVER,
			IID_PPV_ARGS(&WICImagingFactory)
		);
		CHECKRETURN(hr, TEXT("Create WICImagingFactory"));

		// 獲得DXGI介面
		hr = CreateDXGIFactory(IID_PPV_ARGS(&DXGIFactory));
		CHECKRETURN(hr, TEXT("CreateDXGIFactory"));
		ComPtr<IDXGIFactory5> dxgi5;
		DXGIFactory->QueryInterface(IID_PPV_ARGS(&dxgi5));
		CHECKRETURN(hr, TEXT("CreateDXGIFactory"));
		DXGIFactory = dxgi5;

		// 測試看是否支援關閉垂直同步
		int allowTearing = 0;
		dxgi5->CheckFeatureSupport(DXGI_FEATURE_PRESENT_ALLOW_TEARING, &allowTearing, sizeof(allowTearing));
		if (allowTearing == 1) TearingSupport = true;

		// 選擇繪圖介面卡
		ComPtr<IDXGIAdapter> adapter = FindAdapter();
		if (adapter == nullptr) {
			CHECKRETURN(E_FAIL, TEXT("FindAdapter"));
		}

		CreateDevice(adapter);
	}

		public:
		~DirectXPanel() {
			Clear();
			// Closes the COM library on the current thread
			CoUninitialize();
		}

		private:
		void CreateDevice(ComPtr<IDXGIAdapter> DXGIAdapter) {
			if (DXGIAdapter.Get()) {

				HRESULT hr = E_FAIL;

				D3D_FEATURE_LEVEL featureLevels[] =
				{
					D3D_FEATURE_LEVEL_11_1,
					D3D_FEATURE_LEVEL_11_0,
				};

				D3D11_CREATE_DEVICE_FLAG flags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;

				hr = D3D11CreateDevice(DXGIAdapter.Get(), D3D_DRIVER_TYPE_UNKNOWN, nullptr,
					flags,
					featureLevels, ARRAYSIZE(featureLevels),
					D3D11_SDK_VERSION,
					D3D11Device.ReleaseAndGetAddressOf(), &FeatureLevel, ImmediateContext.ReleaseAndGetAddressOf());

				if (hr == E_INVALIDARG) {
					// DirectX 11.0 認不得 D3D_FEATURE_LEVEL_11_1 所以需要排除他,然後再試一次
					hr = D3D11CreateDevice(DXGIAdapter.Get(), D3D_DRIVER_TYPE_UNKNOWN, nullptr, flags, &featureLevels[1], ARRAYSIZE(featureLevels) - 1,
						D3D11_SDK_VERSION, D3D11Device.ReleaseAndGetAddressOf(), &FeatureLevel, ImmediateContext.ReleaseAndGetAddressOf());
					CHECKRETURN(hr, TEXT("D3D11CreateDevice"));
					CurrentContext = ImmediateContext;
				} else if (hr == E_FAIL) {
					CHECKRETURN(E_FAIL, TEXT("D3D11CreateDevice"));
				}

				DXGI_ADAPTER_DESC desc;
				ZeroMemory(&desc, sizeof(desc));
				hr = DXGIAdapter->GetDesc(&desc);
				CHECKRETURN(hr, TEXT("Adapter GetDesc"));
				Info = make_unique<DeviceInfo>();
				Info->FeatureLevel = FeatureLevel;
				Info->VendorId = desc.VendorId;
				Info->DeviceId = desc.DeviceId;
				Info->DedicatedVideoMemory = desc.DedicatedVideoMemory;
				Info->Description = desc.Description;
			}
		}

		public:
		void CreateSwapChain(HWND hwnd) {
			if (DXGIFactory.Get()) {
				HRESULT hr;
				ComPtr<IDXGIFactory2> fac2;
				hr = DXGIFactory.As(&fac2);
				if (fac2.Get()) {
					// DirectX 11.1 以上版本
					ComPtr<ID3D11Device1> dev1;
					hr = D3D11Device.As(&dev1);
					if (SUCCEEDED(hr)) {
						ComPtr<ID3D11DeviceContext1> dc1;
						hr = ImmediateContext->QueryInterface(IID_PPV_ARGS(&dc1));
						if (SUCCEEDED(hr)) {
							ImmediateContext = dc1;
							CurrentContext = ImmediateContext;
						}
					}
				}

				RECT rect;
				GetClientRect(hWnd, &rect);
				ZeroMemory(&SwapChainDesc, sizeof(DXGI_SWAP_CHAIN_DESC1));
				SwapChainDesc.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT | DXGI_USAGE_BACK_BUFFER;
				SwapChainDesc.BufferCount = 2;
				SwapChainDesc.SwapEffect = DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;
				SwapChainDesc.SampleDesc.Count = 1;
				SwapChainDesc.SampleDesc.Quality = 0;
				SwapChainDesc.Scaling = DXGI_SCALING_STRETCH;
				SwapChainDesc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
				SwapChainDesc.Width = rect.right - rect.left;
				SwapChainDesc.Height = rect.bottom - rect.top;
				SwapChainDesc.Stereo = false;
				SwapChainDesc.Flags = TearingSupport ? DXGI_SWAP_CHAIN_FLAG_ALLOW_TEARING : 0;
				hWnd = hwnd;

				hr = fac2->CreateSwapChainForHwnd(D3D11Device.Get(), hWnd, &SwapChainDesc, nullptr, nullptr, SwapChain.ReleaseAndGetAddressOf());
				CHECKRETURN(hr, TEXT("Create SwapChain1"));

				CreateD2DDeviceContextFromSwapChain(SwapChain);
			}
		}

		public:
		void CreateResource() {
			CreateRenderTargetView();
			SetViewport();
			LoadShader();
			PreparePipeline();
			PrepareDirect2D();
		}

		private:
		void CreateD2DDeviceContextFromSwapChain(ComPtr<IDXGISwapChain> swapChain) {
			//https://msdn.microsoft.com/zh-tw/library/windows/desktop/hh780339(v=vs.85).aspx
			if (swapChain.Get()) {
				HRESULT hr;
				
				ComPtr<IDXGIDevice3> dxgiDevice;
				hr = D3D11Device.As(&dxgiDevice);
				CHECKRETURN(hr, TEXT("Get DXGIDevice from D3D11Device"));

				ComPtr<ID2D1Factory1> factory;
				D2D1_FACTORY_OPTIONS fac_options;
				fac_options.debugLevel = D2D1_DEBUG_LEVEL_INFORMATION;
				hr = D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, factory.ReleaseAndGetAddressOf());
				CHECKRETURN(hr, TEXT("Create D2D1Factory"));

				ComPtr<ID2D1Device> d2ddevice;
				hr = factory->CreateDevice(dxgiDevice.Get(), d2ddevice.ReleaseAndGetAddressOf());
				CHECKRETURN(hr, TEXT("Create D2D1Device"));

				hr = d2ddevice->CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS_NONE, D2DDeviceContext.ReleaseAndGetAddressOf());
				CHECKRETURN(hr, TEXT("Create D2D1 DeviceContext"));

				ComPtr<IDXGISurface> backBuffer;
				hr = SwapChain->GetBuffer(0, IID_PPV_ARGS(&backBuffer));
				CHECKRETURN(hr, TEXT("Create D2D1Device"));

				D2D1_BITMAP_PROPERTIES1 bitmapProperties = D2D1::BitmapProperties1();
				// 設定可以被 DeviceContext 使用, 但不能拿來當成 input
				bitmapProperties.bitmapOptions = D2D1_BITMAP_OPTIONS_TARGET | D2D1_BITMAP_OPTIONS_CANNOT_DRAW;
				// Unknown 表示自動選擇與 backbuffer 一樣的格式
				bitmapProperties.pixelFormat = D2D1::PixelFormat(DXGI_FORMAT_UNKNOWN, D2D1_ALPHA_MODE_PREMULTIPLIED);
				D2DDeviceContext->GetDpi(&bitmapProperties.dpiX, &bitmapProperties.dpiY);

				ComPtr<ID2D1Bitmap1> bitmap;
				hr = D2DDeviceContext->CreateBitmapFromDxgiSurface(
					backBuffer.Get(),
					&bitmapProperties,
					bitmap.GetAddressOf());
				backBuffer.Reset();
				CHECKRETURN(hr, TEXT("Get Bitmap From D3D Surface"));

				D2DDeviceContext->SetTarget(bitmap.Get());
				D2DDeviceContext->SetUnitMode(D2D1_UNIT_MODE_PIXELS);
			}
		}

		private:
		void CreateRenderTargetView() {
			if (SwapChain.Get()) {
				HRESULT hr;
				ComPtr<ID3D11Texture2D> backBuffer;
				hr = SwapChain->GetBuffer(0, IID_PPV_ARGS(&backBuffer));
				CHECKRETURN(hr, TEXT("GetBuffer from SwapChain"));
				hr = D3D11Device->CreateRenderTargetView(backBuffer.Get(), nullptr, &RenderTargetView);
				CHECKRETURN(hr, TEXT("CreateRenderTargetView"));
				backBuffer.Reset();
				CurrentContext->OMSetRenderTargets(1, RenderTargetView.GetAddressOf(), nullptr);
			}
		}

		private:
		void SetViewport() {
			if (CurrentContext.Get()) {
				RECT rect;
				GetClientRect(hWnd, &rect);
				D3D11_VIEWPORT vp;
				vp.Width = (FLOAT)(rect.right - rect.left);
				vp.Height = (FLOAT)(rect.bottom - rect.top);
				vp.MinDepth = 0.0f;
				vp.MaxDepth = 1.0f;
				vp.TopLeftX = 0;
				vp.TopLeftY = 0;
				CurrentContext->RSSetViewports(1, &vp);
			}
		}

		private:
		void LoadShader() {		
			if (D3D11Device.Get()) {
				HRESULT hr;
				ShaderCode vertexShaderCode;
				vertexShaderCode.LoadFromFile(TEXT("VertexShader.cso"));
				hr = D3D11Device->CreateVertexShader(vertexShaderCode.Code, vertexShaderCode.Length, nullptr, &VertexShader);
				CHECKRETURN(hr, TEXT("CreateVertexShader"));

				D3D11_INPUT_ELEMENT_DESC layout[] =
				{
					{ "POSITION", 0, DXGI_FORMAT_R32G32B32A32_FLOAT, 0, 0, D3D11_INPUT_PER_VERTEX_DATA, 0 },
					{ "COLOR", 0, DXGI_FORMAT_R32G32B32A32_FLOAT, 0, 16, D3D11_INPUT_PER_VERTEX_DATA, 0 },
					{ "TEXCOORD", 0, DXGI_FORMAT_R32G32_FLOAT, 0, 32, D3D11_INPUT_PER_VERTEX_DATA, 0 }
				};

				// Create the input layout
				hr = D3D11Device->CreateInputLayout(layout, ARRAYSIZE(layout), vertexShaderCode.Code,
					vertexShaderCode.Length, VertexLayout.ReleaseAndGetAddressOf());
				CHECKRETURN(hr, TEXT("Create VertexLayout"));

				// Set the input layout
				CurrentContext->IASetInputLayout(VertexLayout.Get());

				ShaderCode pixelShaderCode;
				pixelShaderCode.LoadFromFile(TEXT("PixelShader.cso"));
				hr = D3D11Device->CreatePixelShader(pixelShaderCode.Code, pixelShaderCode.Length, nullptr, PixelShader.ReleaseAndGetAddressOf());
				CHECKRETURN(hr, TEXT("CreatePixelShader"));

				CurrentContext->VSSetShader(VertexShader.Get(), nullptr, 0);
				CurrentContext->PSSetShader(PixelShader.Get(), nullptr, 0);
			}
		}

		private:
		void Test() {
			HRESULT hr;
			//hr = CreateDDSTextureFromFile(D3D11Device.Get(), L"seafloor.dds", nullptr, &ShaderRV);
			//CHECKRETURN(hr, TEXT("CreateDDSTextureFromFile"));

			hr = CreateWICTextureFromFile(D3D11Device.Get(), L"helloworld.png", nullptr, &resourceView);
			CHECKRETURN(hr, TEXT("CreateWICTextureFromFile"));
			D3D11_SAMPLER_DESC sampDesc;
			ZeroMemory(&sampDesc, sizeof(sampDesc));
			sampDesc.Filter = D3D11_FILTER_MIN_MAG_MIP_LINEAR;
			sampDesc.AddressU = D3D11_TEXTURE_ADDRESS_WRAP;
			sampDesc.AddressV = D3D11_TEXTURE_ADDRESS_WRAP;
			sampDesc.AddressW = D3D11_TEXTURE_ADDRESS_WRAP;
			sampDesc.ComparisonFunc = D3D11_COMPARISON_NEVER;
			sampDesc.MinLOD = 0;
			sampDesc.MaxLOD = D3D11_FLOAT32_MAX;
			hr = D3D11Device->CreateSamplerState(&sampDesc, SamplerState.ReleaseAndGetAddressOf());
			CHECKRETURN(hr, TEXT("CreateSamplerState"));
			CurrentContext->PSSetSamplers(0, 1, SamplerState.GetAddressOf());
			CurrentContext->PSSetShaderResources(0, 1, resourceView.GetAddressOf());
		}

		private:
		void PreparePipeline() {
			// Create vertex buffer
			if (CurrentContext.Get()) {
				HRESULT hr;
				SimpleVertex vertices[] =
				{
					XMFLOAT4(-0.2f, 0.4f, 0.5f, 1.0f), XMFLOAT4(1.0f, 0.0f, 0.0f, 1.0f), XMFLOAT2(0.0f, 0.0f),
					XMFLOAT4(0.2f, 0.4f, 0.5f, 1.0f), XMFLOAT4(0.0f, 1.0f, 0.0f, 1.0f), XMFLOAT2(1.0f, 0.0f),
					XMFLOAT4(-0.2f, -0.4f, 0.5f, 1.0f), XMFLOAT4(0.0f, 0.0f, 1.0f, 1.0f), XMFLOAT2(0.0f, 1.0f),
					XMFLOAT4(0.2f, -0.4f, 0.5f, 1.0f), XMFLOAT4(1.0f, 1.0f, 1.0f, 1.0f), XMFLOAT2(1.0f, 1.0f),
				};

				D3D11_BUFFER_DESC bd;
				ZeroMemory(&bd, sizeof(bd));
				//bd.Usage = D3D11_USAGE_DEFAULT;
				bd.ByteWidth = sizeof(SimpleVertex) * ARRAYSIZE(vertices);
				bd.BindFlags = D3D11_BIND_VERTEX_BUFFER;
				//bd.CPUAccessFlags = 0;
				D3D11_SUBRESOURCE_DATA srd;
				ZeroMemory(&srd, sizeof(srd));
				srd.pSysMem = vertices;
				hr = D3D11Device->CreateBuffer(&bd, &srd, &VertexBuffer);
				CHECKRETURN(hr, TEXT("Create VertexBuffer"));

				// Set vertex buffer
				UINT stride = sizeof(SimpleVertex);
				UINT offset = 0;
				CurrentContext->IASetVertexBuffers(0, 1, VertexBuffer.GetAddressOf(), &stride, &offset);

				// IndexBuffer
				UINT indices[] = { 0, 1, 2, 1, 3, 2 };
				D3D11_BUFFER_DESC indexDesc;
				ZeroMemory(&indexDesc, sizeof(indexDesc));
				indexDesc.ByteWidth = sizeof(UINT) * 6;
				indexDesc.BindFlags = D3D11_BIND_INDEX_BUFFER;
				indexDesc.Usage = D3D11_USAGE_DEFAULT;
				indexDesc.CPUAccessFlags = 0;

				D3D11_SUBRESOURCE_DATA indexsrd;
				ZeroMemory(&indexsrd, sizeof(indexsrd));
				indexsrd.pSysMem = indices;
				hr = D3D11Device->CreateBuffer(&indexDesc, &indexsrd, IndexBuffer.ReleaseAndGetAddressOf());
				CHECKRETURN(hr, TEXT("Create IndexBuffer"));
				CurrentContext->IASetIndexBuffer(IndexBuffer.Get(), DXGI_FORMAT_R32_UINT, 0);

				// Set primitive topology
				CurrentContext->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);

				Test();

				time = std::chrono::system_clock::now();
			}
		}

		private:
		void PrepareDirect2D() {

			if (D2DDeviceContext) {
				HRESULT hr;
				hr = DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory), &DWriteFactory);
				CHECKRETURN(hr, TEXT("DWriteCreateFactory"));
				hr = SwapChain->GetDesc1(&SwapChainDesc);
				CHECKRETURN(hr, TEXT("SwapChain GetDesc1"));

				hr = DWriteFactory->CreateTextFormat(TEXT("微軟正黑體"), nullptr, DWRITE_FONT_WEIGHT_REGULAR, DWRITE_FONT_STYLE_NORMAL, DWRITE_FONT_STRETCH_NORMAL, 22.0f, TEXT("zh-TW"), InfoTextFormat.ReleaseAndGetAddressOf());
				CheckFailed(hr, TEXT("Create Info TextFormat"));
				hr = DWriteFactory->CreateTextFormat(TEXT("微軟正黑體"), nullptr, DWRITE_FONT_WEIGHT_REGULAR, DWRITE_FONT_STYLE_NORMAL, DWRITE_FONT_STRETCH_NORMAL, 24.0f, TEXT("zh-TW"), FPSFormat.ReleaseAndGetAddressOf());
				CheckFailed(hr, TEXT("Create FPS TextFormat"));
				hr = D2DDeviceContext->CreateSolidColorBrush(D2D1::ColorF(D2D1::ColorF::AliceBlue), TextBrush.GetAddressOf());
				CheckFailed(hr, TEXT("Create Text Brush"));
			}
		}

		private:
		void Direct2DRneder() {
			D2DDeviceContext->BeginDraw();
				const String info = Info->ToString();
				if (!info.IsNullOrEmpty()) {
					LPCTSTR str = info.c_str();
					D2D1_RECT_F layoutRect = { 0, 0, 300, 300 };
					D2DDeviceContext->DrawText(str, (UINT32)_tcslen(str), InfoTextFormat.Get(), layoutRect, TextBrush.Get(), D2D1_DRAW_TEXT_OPTIONS_CLIP, DWRITE_MEASURING_MODE_NATURAL);
				}

				if (!(fpsString.IsNullOrEmpty())) {
					LPCTSTR str = fpsString.c_str();
					D2D1_RECT_F layoutRect = { 0, (FLOAT)SwapChainDesc.Height - 30, 300, (FLOAT)SwapChainDesc.Height };
					D2DDeviceContext->DrawText(str, (UINT32)_tcslen(str), FPSFormat.Get(), layoutRect, TextBrush.Get(), D2D1_DRAW_TEXT_OPTIONS_CLIP, DWRITE_MEASURING_MODE_NATURAL);
				}
			D2DDeviceContext->EndDraw();
		}

		public:
		void Update() {
			auto now = std::chrono::system_clock::now();
			auto elapsed = now - time;
			auto count = std::chrono::duration_cast<std::chrono::milliseconds>(elapsed).count();
			if (count > 100) {
				double fps = 1000.0 * fpsCounter / count;
				fpsString.Format(TEXT("%.1lf"), fps);
				fpsCounter = 0;
				time = now;
			}
			fpsCounter++;
		}

		public:
		void Render() {
			if (CurrentContext.Get()) {
				// 把RenderTargetView綁定到Output-Merger Stage
				// 注意這裡是GetAddressOf,而不是ReleaseAndGetAddressOf
				CurrentContext->OMSetRenderTargets(1, RenderTargetView.GetAddressOf(), nullptr);
				// 填滿背景色
				CurrentContext->ClearRenderTargetView(RenderTargetView.Get(), Colors::Black);

				CurrentContext->DrawIndexed(6, 0, 0);

				Direct2DRneder();

				// 把畫好的結果輸出到螢幕上！
				SwapChain->Present(0, TearingSupport ? DXGI_PRESENT_ALLOW_TEARING : 0);
			}
		}

		private:
		void CreateTexture(uint8_t* data, long len) {
			if (len >= 4) {
				if (data[0] == 0x44 && data[1] == 0x44 && data[2] == 0x53 && data[3] == 0x20) {
					//CreateDDSTextureFromMemory(d3dDevice, stream, out texture, out textureView, d3dContext);
				} else {
					CreateWICTexture(data, len);
				}
			}
		}

		void CreateWICTexture(byte* data, size_t size) {
			ComPtr<ID3D11Resource> resource;
			ComPtr<ID3D11ShaderResourceView> resourceView;
			HRESULT hr;
			hr = CreateWICTextureFromMemory(D3D11Device.Get(), data, size, &resource, &resourceView);
			CHECKRETURN(hr, TEXT("CreateWICTextureFromMemory"));

			D3D11_SAMPLER_DESC sampDesc;
			ZeroMemory(&sampDesc, sizeof(sampDesc));
			sampDesc.Filter = D3D11_FILTER_MIN_MAG_MIP_LINEAR;
			sampDesc.AddressU = D3D11_TEXTURE_ADDRESS_WRAP;
			sampDesc.AddressV = D3D11_TEXTURE_ADDRESS_WRAP;
			sampDesc.AddressW = D3D11_TEXTURE_ADDRESS_WRAP;
			sampDesc.ComparisonFunc = D3D11_COMPARISON_NEVER;
			sampDesc.MinLOD = 0;
			sampDesc.MaxLOD = D3D11_FLOAT32_MAX;
			hr = D3D11Device->CreateSamplerState(&sampDesc, SamplerState.ReleaseAndGetAddressOf());
			CHECKRETURN(hr, TEXT("CreateSamplerState"));
			CurrentContext->PSSetSamplers(0, 1, SamplerState.GetAddressOf());
			CurrentContext->PSSetShaderResources(0, 1, resourceView.GetAddressOf());
		}

		public:
		void OpenImage() {
			OPENFILENAME ofn;
			ZeroMemory(&ofn, sizeof(ofn));
			ofn.lStructSize = sizeof(ofn);
			ofn.nMaxFile = 1 * MAX_PATH;
			ofn.lpstrFile = new TCHAR[ofn.nMaxFile];
			ofn.lpstrFile[0] = TEXT('\0');
			ofn.lpstrFilter = TEXT("Files(*.png;*.jpg)\0*.png;*.jpg\0\0");

			DWORD len;
			GetPathMyPictures(NULL, &len);
			TCHAR* pictureFolder = (TCHAR*)new BYTE[len];
			GetPathMyPictures(pictureFolder, &len);
			ofn.lpstrInitialDir = pictureFolder;

			ofn.Flags = OFN_EXPLORER | OFN_FILEMUSTEXIST;

			if (GetOpenFileName(&ofn)) {
				LPTSTR a = ofn.lpstrFile;
				FILE* file;
				
				errno_t err = _tfopen_s(&file, a, TEXT("r"));
				if (err == 0) {
					long len = 0;
					fseek(file, 0, SEEK_END);
					len = ftell(file);
					fseek(file, 0, SEEK_SET);
					uint8_t* buffer = new uint8_t[len];
					fread(buffer, sizeof(uint8_t), len, file);
					fclose(file);
					CreateTexture(buffer, len);
					delete[] buffer;
				}
			}
		}

		private:
		ComPtr<IDXGIAdapter> FindAdapter() {
			if (DXGIFactory.Get()) {
				vector<ComPtr<IDXGIAdapter>> adapters;
				ComPtr<IDXGIAdapter> pAdapter;
				// 列舉所有介面
				for (UINT i = 0; DXGIFactory->EnumAdapters(i, pAdapter.ReleaseAndGetAddressOf()) != DXGI_ERROR_NOT_FOUND; ++i) {
					adapters.push_back(pAdapter);
				}

				// 有 Nvidia 就選, 然後AMD, Intel排最後.

				DXGI_ADAPTER_DESC desc;

				for (const auto& p : adapters) {
					p->GetDesc(&desc);
					// NVIDIA
					if (desc.VendorId == 0x10DE) {
						return p;
					}
				}

				for (const auto& p : adapters) {
					p->GetDesc(&desc);
					// AMD
					if (desc.VendorId == 0x1022) {
						return p;
					}
				}

				for (const auto& p : adapters) {
					p->GetDesc(&desc);
					// Intel
					if (desc.VendorId == 0x8086) {
						return p;
					}
				}

				if (adapters.size() > 0) return adapters[0];
			}
			return nullptr;
		}

		private: 
		void Clear() {
			TextBrush.Reset();
			InfoTextFormat.Reset();
			FPSFormat.Reset();
			DWriteFactory.Reset();
			SamplerState.Reset();
			resourceView.Reset();
			IndexBuffer.Reset();
			VertexBuffer.Reset();
			PixelShader.Reset();
			VertexShader.Reset();
			VertexLayout.Reset();
			RenderTargetView.Reset();
			D2DDeviceContext.Reset();
			ImmediateContext.Reset();
			D3D11Device.Reset();
			DXGIFactory.Reset();
			WICImagingFactory.Reset();
		}

		private:
		HWND hWnd;
		/// 關於ComPtr, 建立新的東西就用ReleaseAndGetAddressOf() 也即 '&'; 如果只是想要參考那就用GetAddressOf()
		ComPtr<IDXGIFactory> DXGIFactory;
		D3D_FEATURE_LEVEL FeatureLevel;
		ComPtr<ID3D11Device> D3D11Device;
			
		ComPtr<ID3D11DeviceContext> ImmediateContext;
		ComPtr<ID3D11DeviceContext> CurrentContext;
		ComPtr<IDXGISwapChain1> SwapChain;
		DXGI_SWAP_CHAIN_DESC1 SwapChainDesc;
		ComPtr<ID2D1DeviceContext> D2DDeviceContext;

		ComPtr<ID3D11RenderTargetView> RenderTargetView;

		ComPtr<ID3D11InputLayout> VertexLayout;
		ComPtr<ID3D11VertexShader> VertexShader;
		ComPtr<ID3D11PixelShader> PixelShader;

		ComPtr<ID3D11Buffer> VertexBuffer;
		ComPtr<ID3D11Buffer> IndexBuffer;
		ComPtr<ID3D11Resource> resource;
		ComPtr<ID3D11ShaderResourceView> resourceView;
		ComPtr<ID3D11SamplerState> SamplerState;

		bool TearingSupport = false; // 支援關閉垂直同步

		ComPtr<IDWriteFactory> DWriteFactory;
		ComPtr<IDWriteTextFormat> FPSFormat;
		ComPtr<IDWriteTextFormat> InfoTextFormat;
		ComPtr<ID2D1SolidColorBrush> TextBrush;

		ComPtr<IWICImagingFactory> WICImagingFactory;

		unique_ptr<DeviceInfo> Info;
		int fpsCounter = 0;
		time_point<system_clock> time;
		String fpsString;
	};
}