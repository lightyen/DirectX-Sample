
#include "stdafx.h"
#include "DirectXEnvironment.h"
#include "SimpleVertex.h"

#define CHECKRETURN(a,b) if (CheckFailed(a,b)) { \
	return; \
}

namespace DirectX {

	BOOL DirectXIsReady;
	D3D_FEATURE_LEVEL FeatureLevel;
	ComPtr<IDXGIFactory2> DXGIFactory;
	ComPtr<IDXGIAdapter2> DXGIAdapter;
	ComPtr<ID3D11Device> D3D11Device;
	ComPtr<IDXGISwapChain1> SwapChain;
	ComPtr<ID3D11DeviceContext> Context;

	ComPtr<ID3D11Texture2D> BackBuffer;
	ComPtr<ID3D11RenderTargetView> RenderTargetView;

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

		DXGI_SWAP_CHAIN_DESC1 desc;
		ZeroMemory(&desc, sizeof(DXGI_SWAP_CHAIN_DESC1));
		desc.BufferCount = 2;
		desc.Width = 0;		//auto sizing
		desc.Height = 0;	//auto sizing
		desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
		desc.SampleDesc.Count = 1;
		desc.SampleDesc.Quality = 0;
		desc.SwapEffect = DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;
		desc.BufferUsage = DXGI_USAGE_BACK_BUFFER | DXGI_USAGE_RENDER_TARGET_OUTPUT;

		result = DXGIFactory->CreateSwapChainForHwnd(D3D11Device.Get(), hWnd, &desc, nullptr, nullptr, &SwapChain);
		CHECKRETURN(result, TEXT("Create SwapChain"));

		DirectXIsReady = TRUE;
	}

	void CreateRenderTargetView() {
		HRESULT hr;
		if (!DirectXIsReady) return;

		hr = SwapChain->GetBuffer(0, __uuidof(ID3D11Texture2D), (void**)&BackBuffer);
		CHECKRETURN(hr, TEXT("Get BackBuffer"));

		hr = D3D11Device->CreateRenderTargetView(BackBuffer.Get(), nullptr, &RenderTargetView);
		CHECKRETURN(hr, TEXT("CreateRenderTargetView"));
	}

	void Render() {
		HRESULT hr;
		if (!DirectXIsReady) return;

		const float teelBlue[] = { 0x36 / 255.0f, 0x75 / 255.0f, 0x88 / 255.0f, 1.000f };
		// Clear Screen to Teel Blue.
		Context->ClearRenderTargetView(
			RenderTargetView.Get(), teelBlue);

		D3D11_VIEWPORT viewport = { 0 };
		viewport.TopLeftX = 0;
		viewport.TopLeftY = 0;
		RECT rect;
		GetWindowRect(hWnd, &rect);
		viewport.Width = (float)(rect.right - rect.left);
		viewport.Height = (float)(rect.bottom - rect.top);

		Context->RSSetViewports(1, &viewport);

		SimpleVertex vertices[] = {
			XMFLOAT3(0.0f, 0.5f, 0.0f),
			XMFLOAT3(0.45f, -0.5f, 0.0f),
			XMFLOAT3(-0.45f, -0.5f, 0.0f),
		};

		ComPtr<ID3D11Buffer> vertexbuffer;
		D3D11_BUFFER_DESC buffer_desc;
		ZeroMemory(&buffer_desc, sizeof(D3D11_BUFFER_DESC));
		buffer_desc.Usage = D3D11_USAGE_DEFAULT;
		buffer_desc.ByteWidth = sizeof(SimpleVertex) * ARRAYSIZE(vertices);
		buffer_desc.BindFlags = D3D11_BIND_VERTEX_BUFFER;
		buffer_desc.CPUAccessFlags = 0;
		buffer_desc.MiscFlags = 0;
		D3D11_SUBRESOURCE_DATA subresource_data = { vertices, 0, 0 };
		hr = D3D11Device->CreateBuffer(&buffer_desc, &subresource_data, &vertexbuffer);
		CHECKRETURN(hr, TEXT("Create VertexBuffer"));

		UINT stride = sizeof(vertices);
		UINT offset = 0;
		Context->IASetVertexBuffers(0, 1, vertexbuffer.GetAddressOf(), &stride, &offset);
		Context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
		Context->Draw(3, 0);

		

		SwapChain->Present(1, 0);
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
