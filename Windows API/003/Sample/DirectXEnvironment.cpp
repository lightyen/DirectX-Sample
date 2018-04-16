
#include "stdafx.h"
#include "DirectXEnvironment.h"
#include "SimpleVertex.h"
#include "Shader.h"


#define CHECKRETURN(a,b) if (CheckFailed(a,b)) { \
	return; \
}

namespace DirectX {

	BOOL DirectXIsReady;
	D3D_FEATURE_LEVEL FeatureLevel;
	ComPtr<IDXGIFactory2> DXGIFactory;
	ComPtr<IDXGIAdapter2> DXGIAdapter;
	ComPtr<ID3D11Device> D3D11Device;
#ifdef TEST
	ComPtr<IDXGISwapChain1> SwapChain;
#else
	ComPtr<IDXGISwapChain> SwapChain;
#endif
	ComPtr<ID3D11DeviceContext> Context;

	ComPtr<ID3D11Texture2D> BackBuffer;
	ComPtr<ID3D11RenderTargetView> RenderTargetView;
	ComPtr<ID3D11VertexShader> VertexShader;
	ComPtr<ID3D11PixelShader> PixelShader;
	ComPtr<ID3D11Buffer> VertexBuffer;
	ComPtr<ID3D11InputLayout> Inputlayout;
	HWND hWnd;

	void InitializeDirectX(HWND hwnd, D3D11_CREATE_DEVICE_FLAG flags) {
		HRESULT result = E_FAIL;
		if (DirectXIsReady) return;

		hWnd = hwnd;

		// 獲得DXGI介面
		ComPtr<IDXGIFactory> _dxgiFactory;
		result = CreateDXGIFactory(IID_PPV_ARGS(&_dxgiFactory));
		CHECKRETURN(result, TEXT("CreateDXGIFactory"));
		result = _dxgiFactory.As(&DXGIFactory);
		CHECKRETURN(result,TEXT("Get DXGIFactory2"));

		// 列舉所有繪圖介面卡
		vector<ComPtr<IDXGIAdapter1>> adapters = EnumerateAdapters(DXGIFactory);

		// 選擇一個高效能介面卡
		ComPtr<IDXGIAdapter1> adpater = FindHardwareAdapter(adapters);
		result = adpater.As(&DXGIAdapter);
		CHECKRETURN(result, TEXT("Get DXGIAdapter1"));
		ComPtr<IDXGIAdapter> adapter0;
		result = DXGIAdapter.As(&adapter0);
		CHECKRETURN(result, TEXT("Get DXGIAdapter"));

		// 建立Direct3D 11 Device
		D3D_FEATURE_LEVEL featureLevels[] =
		{
			D3D_FEATURE_LEVEL_11_1,
			D3D_FEATURE_LEVEL_11_0,
			D3D_FEATURE_LEVEL_10_1,
		};

		result = D3D11CreateDevice(adapter0.Get(), D3D_DRIVER_TYPE_UNKNOWN, nullptr,
			flags,
			featureLevels, ARRAYSIZE(featureLevels),
			D3D11_SDK_VERSION,
			&D3D11Device, &FeatureLevel, &Context);
		CHECKRETURN(result, TEXT("Create D3D11 Device"));
#ifdef TEST
		DXGI_SWAP_CHAIN_DESC1 desc;
		ZeroMemory(&desc, sizeof(DXGI_SWAP_CHAIN_DESC1));
		desc.BufferCount = 1;
		desc.Width = 0;		//auto sizing
		desc.Height = 0;	//auto sizing
		desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
		desc.SampleDesc.Count = 1;
		desc.SampleDesc.Quality = 0;
		//desc.SwapEffect = DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;
		desc.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
		result = DXGIFactory->CreateSwapChainForHwnd(D3D11Device.Get(), hWnd, &desc, nullptr, nullptr, &SwapChain);
#else
		RECT rect;
		GetClientRect(hWnd, &rect);
		DXGI_SWAP_CHAIN_DESC sd;
		ZeroMemory(&sd, sizeof(sd));
		sd.BufferCount = 1;
		sd.BufferDesc.Width = rect.right - rect.left;
		sd.BufferDesc.Height = rect.bottom - rect.top;
		sd.BufferDesc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
		sd.BufferDesc.RefreshRate.Numerator = 60;
		sd.BufferDesc.RefreshRate.Denominator = 1;
		sd.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
		sd.OutputWindow = hWnd;
		sd.SampleDesc.Count = 1;
		sd.SampleDesc.Quality = 0;
		sd.Windowed = TRUE;
		DXGIFactory->CreateSwapChain(D3D11Device.Get(), &sd, &SwapChain);
#endif
		CHECKRETURN(result, TEXT("Create SwapChain"));

		ShaderCode pixelShaderCode;
		ShaderCode vertexShaderCode;
		pixelShaderCode.LoadFromFile(TEXT("PixelShader.cso"));
		vertexShaderCode.LoadFromFile(TEXT("VertexShader.cso"));
		if (vertexShaderCode.IsOK && pixelShaderCode.IsOK) {
			result = D3D11Device->CreateVertexShader(vertexShaderCode.Code, vertexShaderCode.Length, nullptr, &VertexShader);
			CHECKRETURN(result, TEXT("Create VertexShader"));
			result = D3D11Device->CreatePixelShader(pixelShaderCode.Code, pixelShaderCode.Length, nullptr, &PixelShader);
			CHECKRETURN(result, TEXT("Create PixelShader"));

			// initialize input layout
			D3D11_INPUT_ELEMENT_DESC layout[] =
			{
				{ "POSITION", 0, DXGI_FORMAT_R32G32B32_FLOAT,		0, 0, D3D11_INPUT_PER_VERTEX_DATA, 0 },
			};

			// create and set the input layout
			result = D3D11Device->CreateInputLayout(layout, ARRAYSIZE(layout), vertexShaderCode.Code, vertexShaderCode.Length, &Inputlayout);
			CHECKRETURN(result, TEXT("SetInputLayout"));
			Context->IASetInputLayout(Inputlayout.Get());
		}
		
		DirectXIsReady = TRUE;
	}

	void CreateRenderTargetView() {
		HRESULT hr;
		if (!DirectXIsReady) return;

		hr = SwapChain->GetBuffer(0, __uuidof(ID3D11Texture2D), &BackBuffer);
		CHECKRETURN(hr, TEXT("Get BackBuffer"));

		hr = D3D11Device->CreateRenderTargetView(BackBuffer.Get(), nullptr, &RenderTargetView);
		CHECKRETURN(hr, TEXT("CreateRenderTargetView"));
		BackBuffer.Reset();

		Context->OMSetRenderTargets(1, RenderTargetView.GetAddressOf(), nullptr);
		
		D3D11_VIEWPORT viewport = { 0 };
		viewport.TopLeftX = 0;
		viewport.TopLeftY = 0;
		viewport.MinDepth = 0.0f;
		viewport.MaxDepth = 1.0f;
		RECT rect;
		GetClientRect(hWnd, &rect);
		viewport.Width = (float)(rect.right - rect.left);
		viewport.Height = (float)(rect.bottom - rect.top);
		Context->RSSetViewports(1, &viewport);


		SimpleVertex vertices[] = {
			XMFLOAT3(0.0f, 0.5f, 0.5f),
			XMFLOAT3(0.5f, -0.5f, 0.5f),
			XMFLOAT3(-0.5f, -0.5f, 0.5f),
		};

		D3D11_BUFFER_DESC buffer_desc;
		ZeroMemory(&buffer_desc, sizeof(D3D11_BUFFER_DESC));
		buffer_desc.Usage = D3D11_USAGE_DEFAULT;
		buffer_desc.ByteWidth = sizeof(SimpleVertex) * 3;
		buffer_desc.BindFlags = D3D11_BIND_VERTEX_BUFFER;
		buffer_desc.CPUAccessFlags = 0;
		buffer_desc.MiscFlags = 0;
		D3D11_SUBRESOURCE_DATA subresource_data;
		ZeroMemory(&subresource_data, sizeof(D3D11_SUBRESOURCE_DATA));
		subresource_data.pSysMem = vertices;
		subresource_data.SysMemPitch = 0;
		subresource_data.SysMemSlicePitch = 0;

		hr = D3D11Device->CreateBuffer(&buffer_desc, &subresource_data, &VertexBuffer);
		CHECKRETURN(hr, TEXT("Create VertexBuffer"));

		UINT stride = sizeof(SimpleVertex);
		UINT offset = 0;
		Context->IASetVertexBuffers(0, 1, &VertexBuffer, &stride, &offset);
		Context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
	}

	void Render() {

		if (!DirectXIsReady) return;

		const float teelBlue[] = { 0x36 / 255.0f, 0x75 / 255.0f, 0x88 / 255.0f, 1.000f };
		const float black[] = {0.0f, 0.0f, 0.0f, 1.0f};
		// Clear Screen to Teel Blue.
		Context->ClearRenderTargetView(
			RenderTargetView.Get(), teelBlue);

		//Context->VSSetShader(VertexShader.Get(), nullptr, 0);
		//Context->PSSetShader(PixelShader.Get(), nullptr, 0);

		//Context->Draw(3, 0);

		SwapChain->Present(0, 0);
	}

	vector<ComPtr<IDXGIAdapter1>> EnumerateAdapters(const ComPtr<IDXGIFactory2>& DXGIFactory) {
		vector<ComPtr<IDXGIAdapter1>> adapters;
		ComPtr<IDXGIAdapter1> pAdapter;
		for (UINT i = 0;
			DXGIFactory->EnumAdapters1(i, &pAdapter) != DXGI_ERROR_NOT_FOUND;
			++i) {
			adapters.push_back(pAdapter);
		}

		return adapters;
	}

	ComPtr<IDXGIAdapter1> FindHardwareAdapter(const vector<ComPtr<IDXGIAdapter1>>& vAdapters) {
		DXGI_ADAPTER_DESC1 desc;

		for (const auto& p : vAdapters) {
			p->GetDesc1(&desc);
			// NVIDIA
			if (desc.VendorId == 0x10DE) {
				return p;
			}
		}

		for (const auto& p : vAdapters) {
			p->GetDesc1(&desc);
			// AMD
			if (desc.VendorId == 0x1022) {
				return p;
			}
		}

		for (const auto& p : vAdapters) {
			p->GetDesc1(&desc);
			// AMD
			if (desc.VendorId == 0x8086) {
				return p;
			}
		}

		if (vAdapters.size()) return *vAdapters.begin();
		return ComPtr<IDXGIAdapter1>();
	}

	String GetFeatureLevelString() {
		switch (FeatureLevel) {
		case D3D_FEATURE_LEVEL_12_1:
			return TEXT("Direct3D 12.1");
		case D3D_FEATURE_LEVEL_12_0:
			return TEXT("Direct3D 12.0");
		case D3D_FEATURE_LEVEL_11_1:
			return TEXT("Direct3D 11.1");
		case D3D_FEATURE_LEVEL_11_0:
			return TEXT("Direct3D 11.0");
		case D3D_FEATURE_LEVEL_10_1:
			return TEXT("Direct3D 10.1");
		case D3D_FEATURE_LEVEL_10_0:
			return TEXT("Direct3D 10.0");
		case D3D_FEATURE_LEVEL_9_3:
			return TEXT("Direct3D 9.3");
		case D3D_FEATURE_LEVEL_9_2:
			return TEXT("Direct3D 9.2");
		case D3D_FEATURE_LEVEL_9_1:
			return TEXT("Direct3D 9.1");
		default:
			return TEXT("Direct3D Unknown");
		}
	}
}
