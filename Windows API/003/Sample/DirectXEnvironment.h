#pragma once

#include <vector>

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
	extern ComPtr<IDXGISwapChain1> SwapChain;
	extern ComPtr<ID3D11DeviceContext> Context;
	extern ComPtr<ID3D11Texture2D> BackBuffer;
	extern ComPtr<ID3D11RenderTargetView> RenderTargetView;
}