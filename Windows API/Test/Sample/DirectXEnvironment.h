#pragma once

#include "stdafx.h"
#include "SimpleVertex.h"
#include "Shader.h"
#include <vector>
#include <d3d11.h>
#include <d3d11_1.h>
#include <dxgi.h>
#include <dxgi1_2.h>
#include <DirectXColors.h>
#include <wrl\client.h>
using namespace Microsoft::WRL;
#include "DirectXTK\Inc\WICTextureLoader.h"
#include "DDSTextureLoader.h"

#define CHECKRETURN(a,b) if (CheckFailed(a,b)) { \
	return; \
}

namespace DirectX {

	class MyDirectX {

	public:
		/// 關於ComPtr, 建立新的東東就用ReleaseAndGetAddressOf() => '&', 如果只是想要參考那就用GetAddressOf()
		ComPtr<ID3D11Device> D3D11Device;
		ComPtr<ID3D11Device1> D3D11Device1;
		ComPtr<IDXGIDevice> DXGIDevice;
		ComPtr<ID3D11DeviceContext> ImmediateContext;
		ComPtr<ID3D11DeviceContext1> ImmediateContext1;
		ComPtr<IDXGIFactory> DXGIFactory;
		ComPtr<IDXGIFactory2> DXGIFactory2;
		D3D_FEATURE_LEVEL FeatureLevel;

		HWND hWnd;
		ComPtr<IDXGISwapChain> SwapChain;
		ComPtr<IDXGISwapChain1> SwapChain1;

		ComPtr<ID3D11RenderTargetView> RenderTargetView;

		ComPtr<ID3D11InputLayout> VertexLayout;
		ComPtr<ID3D11VertexShader> VertexShader;
		ComPtr<ID3D11PixelShader> PixelShader;

		ComPtr<ID3D11Buffer> VertexBuffer;
		ComPtr<ID3D11Buffer> IndexBuffer;
		ComPtr<ID3D11ShaderResourceView> ShaderRV;
		ComPtr<ID3D11SamplerState> SamplerState;

		void Initialize(D3D11_CREATE_DEVICE_FLAG flags = D3D11_CREATE_DEVICE_DEBUG) {
			HRESULT hr = E_FAIL;

			hr = CoInitializeEx(nullptr, COINITBASE_MULTITHREADED);
			CHECKRETURN(hr, TEXT("CoInitialize"));

			// 獲得DXGI介面
			hr = CreateDXGIFactory(IID_PPV_ARGS(&DXGIFactory));
			CHECKRETURN(hr, TEXT("CreateDXGIFactory"));

			ComPtr<IDXGIAdapter> adapter = GetPreferenceAdapter(DXGIFactory.Get());
			if (adapter == nullptr) {
				CHECKRETURN(E_FAIL, TEXT("GetPreferenceAdapter"));
			}

			D3D_FEATURE_LEVEL featureLevels[] =
			{
				D3D_FEATURE_LEVEL_11_1,
				D3D_FEATURE_LEVEL_11_0,
			};

			hr = D3D11CreateDevice(adapter.Get(), D3D_DRIVER_TYPE_UNKNOWN, nullptr,
				flags,
				featureLevels, ARRAYSIZE(featureLevels),
				D3D11_SDK_VERSION,
				&D3D11Device, &FeatureLevel, &ImmediateContext);

			if (hr == E_INVALIDARG) {
				// DirectX 11.0 認不得 D3D_FEATURE_LEVEL_11_1 所以需要排除他再試一次
				hr = D3D11CreateDevice(adapter.Get(), D3D_DRIVER_TYPE_UNKNOWN, nullptr, flags, &featureLevels[1], ARRAYSIZE(featureLevels) - 1,
					D3D11_SDK_VERSION, &D3D11Device, &FeatureLevel, &ImmediateContext);
				CHECKRETURN(hr, TEXT("D3D11CreateDevice"));
			} else if (hr == E_FAIL) {
				CHECKRETURN(E_FAIL, TEXT("D3D11CreateDevice"));
			}
		}

		void CreateSwapChain(HWND hwnd) {
			if (DXGIFactory.Get()) {
				HRESULT hr;
				hr = DXGIFactory.As(&DXGIFactory2);
				if (DXGIFactory2.Get()) {
					// DirectX 11.1 以上版本
					hr = D3D11Device->QueryInterface(IID_PPV_ARGS(&D3D11Device1));
					if (SUCCEEDED(hr)) {
						ImmediateContext->QueryInterface(IID_PPV_ARGS(&ImmediateContext1));
					}
					// 不一定會用到 先放著。
				}

				RECT rect;
				GetClientRect(hWnd, &rect);
				DXGI_SWAP_CHAIN_DESC1 sd;
				ZeroMemory(&sd, sizeof(DXGI_SWAP_CHAIN_DESC1));
				sd.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
				sd.BufferCount = 2;
				sd.SwapEffect = DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;
				sd.SampleDesc.Count = 1;
				sd.SampleDesc.Quality = 0;
				sd.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
				sd.Width = rect.right - rect.left;
				sd.Height = rect.bottom - rect.top;
				sd.Scaling = DXGI_SCALING_STRETCH;
				sd.Stereo = false;

				// 記一下
				hWnd = hwnd;

				hr = DXGIFactory2->CreateSwapChainForHwnd(D3D11Device.Get(), hWnd, &sd, nullptr, nullptr, &SwapChain1);
				CHECKRETURN(hr, TEXT("Create SwapChain1"));

				hr = SwapChain1.As<IDXGISwapChain>(&SwapChain);
				CHECKRETURN(hr, TEXT("Create SwapChain"));
			}
		}

		void CreateRenderTargetView() {
			if (SwapChain.Get()) {
				HRESULT hr;
				ComPtr<ID3D11Texture2D> BackBuffer;
				hr = SwapChain->GetBuffer(0, IID_PPV_ARGS(&BackBuffer));
				CHECKRETURN(hr, TEXT("GetBuffer from SwapChain"));
				hr = D3D11Device->CreateRenderTargetView(BackBuffer.Get(), nullptr, &RenderTargetView);
				CHECKRETURN(hr, TEXT("CreateRenderTargetView"));
				// 放手
				BackBuffer.Reset();
				
				ImmediateContext->OMSetRenderTargets(1, RenderTargetView.GetAddressOf(), nullptr);
			}
		}

		void SetViewport() {
			if (ImmediateContext.Get()) {
				RECT rect;
				GetClientRect(hWnd, &rect);
				D3D11_VIEWPORT vp;
				vp.Width = (FLOAT)(rect.right - rect.left);
				vp.Height = (FLOAT)(rect.bottom - rect.top);
				vp.MinDepth = 0.0f;
				vp.MaxDepth = 1.0f;
				vp.TopLeftX = 0;
				vp.TopLeftY = 0;
				ImmediateContext->RSSetViewports(1, &vp);
			}
		}

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
					vertexShaderCode.Length, &VertexLayout);
				CHECKRETURN(hr, TEXT("Create VertexLayout"));

				// Set the input layout
				ImmediateContext->IASetInputLayout(VertexLayout.Get());

				ShaderCode pixelShaderCode;
				pixelShaderCode.LoadFromFile(TEXT("PixelShader.cso"));
				hr = D3D11Device->CreatePixelShader(pixelShaderCode.Code, pixelShaderCode.Length, nullptr, &PixelShader);
				CHECKRETURN(hr, TEXT("CreatePixelShader"));

				ImmediateContext->VSSetShader(VertexShader.Get(), nullptr, 0);
				ImmediateContext->PSSetShader(PixelShader.Get(), nullptr, 0);
			}
		}

		void PreparePipeline() {
			// Create vertex buffer
			if (D3D11Device.Get()) {
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
				// 注意這裡是GetAddressOf, 而不是ReleaseAndGetAddressOf
				ImmediateContext->IASetVertexBuffers(0, 1, VertexBuffer.GetAddressOf(), &stride, &offset);

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
				hr = D3D11Device->CreateBuffer(&indexDesc, &indexsrd, &IndexBuffer);
				CHECKRETURN(hr, TEXT("Create IndexBuffer"));
				ImmediateContext->IASetIndexBuffer(IndexBuffer.Get(), DXGI_FORMAT_R32_UINT, 0);

				// Set primitive topology
				ImmediateContext->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);

				Test();
			}
		}

		void Test() {
			HRESULT hr;
			//hr = CreateDDSTextureFromFile(D3D11Device.Get(), L"seafloor.dds", nullptr, &ShaderRV);
			//CHECKRETURN(hr, TEXT("CreateDDSTextureFromFile"));

			hr = CreateWICTextureFromFile(D3D11Device.Get(), L"helloworld.png", nullptr, ShaderRV.ReleaseAndGetAddressOf());
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
			hr = D3D11Device->CreateSamplerState(&sampDesc, &SamplerState);
			CHECKRETURN(hr, TEXT("CreateSamplerState"));
			ImmediateContext->PSSetSamplers(0, 1, SamplerState.GetAddressOf());


			ImmediateContext->PSSetShaderResources(0, 1, ShaderRV.GetAddressOf());
		}

		void Render() {
			if (ImmediateContext != nullptr) {
				// 把RenderTargetView綁定到Output-Merger Stage
				// 注意這裡是GetAddressOf,而不是ReleaseAndGetAddressOf
				ImmediateContext->OMSetRenderTargets(1, RenderTargetView.GetAddressOf(), nullptr);
				// 填滿背景色
				ImmediateContext->ClearRenderTargetView(RenderTargetView.Get(), Colors::Black);
				// 畫一個三角形
				ImmediateContext->DrawIndexed(6, 0, 0);
				// 把畫好的結果輸出到螢幕上！
				SwapChain1->Present(1, 0);
			}
		}

		ComPtr<IDXGIAdapter> GetPreferenceAdapter(IDXGIFactory* DXGIFactory) {
			vector<ComPtr<IDXGIAdapter>> adapters;
			ComPtr<IDXGIAdapter> pAdapter;
			for (UINT i = 0; DXGIFactory->EnumAdapters(i, &pAdapter) != DXGI_ERROR_NOT_FOUND; ++i) {
				adapters.push_back(pAdapter);
			}

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
			return nullptr;
		}
	};
}