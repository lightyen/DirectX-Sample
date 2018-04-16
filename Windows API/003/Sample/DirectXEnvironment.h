#pragma once

#include <vector>
#include <d3d11.h>
#include <dxgi1_2.h>
#include <wrl\client.h>
using namespace Microsoft::WRL;

namespace DirectX {

	void InitializeDirectX(HWND hWnd, D3D11_CREATE_DEVICE_FLAG flags = D3D11_CREATE_DEVICE_VIDEO_SUPPORT);
	vector<ComPtr<IDXGIAdapter1>> EnumerateAdapters(const ComPtr<IDXGIFactory2>& DXGIFactory);
	ComPtr<IDXGIAdapter1> FindHardwareAdapter(const vector<ComPtr<IDXGIAdapter1>>& vAdapters);
	void CreateRenderTargetView();
	void Render();
	String GetFeatureLevelString();

	extern HWND hWnd;
	extern BOOL DirectXIsReady;
	extern D3D_FEATURE_LEVEL FeatureLevel;
	extern ComPtr<IDXGIFactory2> DXGIFactory;
	extern ComPtr<IDXGIAdapter2> DXGIAdapter;
	extern ComPtr<ID3D11Device> D3D11Device;
	extern ComPtr<IDXGIDevice2> DXGIDevice;
#ifdef TEST
	extern ComPtr<IDXGISwapChain1> SwapChain;
#else
	extern ComPtr<IDXGISwapChain> SwapChain;
#endif
	extern ComPtr<ID3D11DeviceContext> Context;
	extern ComPtr<ID3D11Texture2D> BackBuffer;
	extern ComPtr<ID3D11RenderTargetView> RenderTargetView;
	extern ComPtr<ID3D11VertexShader>VertexShader;
	extern ComPtr<ID3D11PixelShader> PixelShader;
	extern ComPtr<ID3D11Buffer> VertexBuffer;
	extern ComPtr<ID3D11InputLayout> Inputlayout;
}