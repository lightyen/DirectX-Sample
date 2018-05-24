#pragma once

#include "DeviceInfo.h"
#include "SimpleVertex.h"
#include "Shader.h"

#include <wbemidl.h>
#include <comutil.h>
#pragma comment(lib, "wbemuuid.lib")
#pragma comment(lib, "comsuppw.lib")

#define CHECKRETURN(a,b) if(CheckFailed(a,b)){return;}

namespace MyGame {

	class DirectXPanel {

		public:
		DirectXPanel() {

			fbxSdkManager = FbxManager::Create();
			FbxImporter* fbxImportor = FbxImporter::Create(fbxSdkManager, "");
			FbxIOSettings* pIOsettings = FbxIOSettings::Create(fbxSdkManager, IOSROOT);
			fbxSdkManager->SetIOSettings(pIOsettings);

			if (fbxImportor->Initialize("./Resource/studio_objs.fbx", -1, fbxSdkManager->GetIOSettings())) {
				fbxScene = FbxScene::Create(fbxSdkManager, "");
				if (fbxImportor->Import(fbxScene) == false) {
					fbxScene->Destroy();
					fbxScene = nullptr;
				} else {
					OutputDebug(TEXT("Load FBX success\n"));
					// Populate the FBX file format version numbers with the import file.
					int major, minor, revision;
					fbxImportor->GetFileVersion(major, minor, revision);
					OutputDebug(TEXT("FBX File Version: %d %d %d\n"), major, minor, revision);
					
					if (FbxNode* fbxRootNode = fbxScene->GetRootNode()) {
						PrintFBXHierarchy(fbxRootNode);
					}
				}
			}

			if (fbxImportor) fbxImportor->Destroy();

			HRESULT hr;
			// 在此線程初始化 COM 組件調用模式，並且設定同步/非同步類型
			hr = CoInitializeEx(nullptr, COINITBASE_MULTITHREADED);
			CHECKRETURN(hr, TEXT("CoInitialize"));

			Info = make_unique<DeviceInfo>();

			// 獲得系統資訊
			GetWMIData();

			// 獲得DXGI介面
			hr = CreateDXGIFactory(IID_PPV_ARGS(&DXGIFactory));
			CHECKRETURN(hr, TEXT("CreateDXGIFactory"));
			ComPtr<IDXGIFactory5> dxgi5;
			DXGIFactory->QueryInterface(IID_PPV_ARGS(&dxgi5));
			CHECKRETURN(hr, TEXT("CreateDXGIFactory"));
			DXGIFactory = dxgi5;

			// 測試看看是否支援關閉垂直同步
			dxgi5->CheckFeatureSupport(DXGI_FEATURE_PRESENT_ALLOW_TEARING, &TearingSupport, sizeof(TearingSupport));
			
			// 選擇繪圖介面卡
			ComPtr<IDXGIAdapter> adapter = FindAdapter();
			if (adapter == nullptr) {
				CHECKRETURN(E_FAIL, TEXT("FindAdapter"));
			}
			
			CreateDevice(adapter);

			hr = CoCreateInstance(
				CLSID_WICImagingFactory,
				NULL,
				CLSCTX_INPROC_SERVER,
				IID_PPV_ARGS(&WICImagingFactory)
			);
			CHECKRETURN(hr, TEXT("Create WICImagingFactory"));
		}

		public:
		~DirectXPanel() {
			Clear();
			// Closes the COM library before exit
			CoUninitialize();
		}

		private:
		void Initialize(HWND hWnd) {
			
			if (!CreateSwapChain(hWnd)) return;

			CreateRenderTargetView();
			CreateDepthStencilView();
			PrepareData();

			LoadShader(ImmediateContext.Get());
			SetupPipeline(ImmediateContext.Get());
			SetViewport(ImmediateContext.Get());

			//CommonStates states = CommonStates(D3D11Device.Get());
			//auto cull = states.CullCounterClockwise();
			//ImmediateContext->RSSetState(cull);

			PrepareDirect2D();
			QueryPerformanceCounter(&time);
			QueryPerformanceFrequency(&freq);
		}

		private:
		void CreateDevice(ComPtr<IDXGIAdapter> DXGIAdapter) {
			if (DXGIAdapter.Get()) {

				HRESULT hr = E_FAIL;

				D3D_FEATURE_LEVEL featureLevels[2] =
				{
					D3D_FEATURE_LEVEL_11_1,
					D3D_FEATURE_LEVEL_11_0,
				};

				D3D11_CREATE_DEVICE_FLAG flags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;

				hr = D3D11CreateDevice(DXGIAdapter.Get(), D3D_DRIVER_TYPE_UNKNOWN, nullptr, flags, featureLevels, sizeof(featureLevels) / sizeof(D3D_FEATURE_LEVEL),
					D3D11_SDK_VERSION, &D3D11Device, &FeatureLevel, &ImmediateContext);

				if (hr == E_INVALIDARG) {
					// DirectX 11.0 認不得 D3D_FEATURE_LEVEL_11_1 所以需要排除他,然後再試一次
					hr = D3D11CreateDevice(DXGIAdapter.Get(), D3D_DRIVER_TYPE_UNKNOWN, nullptr, flags, &featureLevels[1], sizeof(featureLevels) / sizeof(D3D_FEATURE_LEVEL) - 1,
						D3D11_SDK_VERSION, &D3D11Device, &FeatureLevel, &ImmediateContext);
				}

				CHECKRETURN(hr, TEXT("D3D11CreateDevice"));

				DXGI_ADAPTER_DESC desc;
				ZeroMemory(&desc, sizeof(DXGI_ADAPTER_DESC));
				hr = DXGIAdapter->GetDesc(&desc);
				CHECKRETURN(hr, TEXT("Adapter GetDesc"));
				
				Info->FeatureLevel = FeatureLevel;
				Info->VendorId = desc.VendorId;
				Info->DeviceId = desc.DeviceId;
				Info->DedicatedVideoMemory = desc.DedicatedVideoMemory;
				Info->Description = desc.Description;

				for (int i = 1; i <= 128; i = i << 1) {
					UINT MsaaQuality;
					hr = D3D11Device->CheckMultisampleQualityLevels(DXGI_FORMAT_B8G8R8A8_UNORM, i, &MsaaQuality);
					MsaaQualities.push_back(MsaaQuality);
				}

				// 檢查驅動程式有無支援 MultiThreading
				// https://msdn.microsoft.com/zh-tw/library/windows/desktop/ff476893(v=vs.85).aspx
				hr = D3D11Device->CheckFeatureSupport(D3D11_FEATURE_THREADING, &ThreadingSupport, sizeof(D3D11_FEATURE_DATA_THREADING));
				CHECKRETURN(hr, TEXT("CheckFeatureSupport Multithreading"));
			}
		}

		private:
		BOOL CreateSwapChain(HWND hwnd) {
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
						}
					}
				}
				
				RECT rect;
				GetClientRect(hwnd, &rect);
				ZeroMemory(&SwapChainDesc, sizeof(DXGI_SWAP_CHAIN_DESC1));
				SwapChainDesc.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT | DXGI_USAGE_BACK_BUFFER;
				SwapChainDesc.BufferCount = 2;
				SwapChainDesc.SwapEffect = DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;
				if (MsaaQualities[2]) {
					SwapChainDesc.SampleDesc.Count = 1 << 2;
					SwapChainDesc.SampleDesc.Quality = MsaaQualities[2] - 1;
				}
				else {
					
				}
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
				if (CheckFailed(hr, TEXT("Create SwapChain1"))) return FALSE;

				CreateD2DDeviceContextFromSwapChain(SwapChain);

				if (!Tearing) {
					CheckMenuItem(GetMenu(hWnd), IDM_TEARING, MF_CHECKED);
				}

				return TRUE;
			}

			return FALSE;
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
			}
		}

		private: 
		void CreateDepthStencilView() {
			if (hWnd && D3D11Device.Get()) {
				RECT rect;
				GetClientRect(hWnd, &rect);

				////////////////////////////////////////
				// Create a Depth-Stencil Resource
				////////////////////////////////////////

				D3D11_TEXTURE2D_DESC desc;
				ZeroMemory(&desc, sizeof(D3D11_TEXTURE2D_DESC));
				desc.Width = SwapChainDesc.Width;
				desc.Height = SwapChainDesc.Height;
				desc.MipLevels = 1;
				desc.ArraySize = 1;
				desc.Format = DXGI_FORMAT_D32_FLOAT_S8X24_UINT;
				desc.SampleDesc.Count = 1;
				desc.SampleDesc.Quality = 0;
				desc.Usage = D3D11_USAGE_DEFAULT;
				desc.BindFlags = D3D11_BIND_DEPTH_STENCIL;
				desc.CPUAccessFlags = 0;
				desc.MiscFlags = 0;

				HRESULT hr;
				ComPtr<ID3D11Texture2D> depthStencilTexture;
				hr = D3D11Device->CreateTexture2D(&desc, nullptr, &depthStencilTexture);
				CHECKRETURN(hr, TEXT("Create DepthTexture"));

				////////////////////////////////////////
				// Create Depth-Stencil State
				////////////////////////////////////////
				/***
				D3D11_DEPTH_STENCIL_DESC depthStencilDesc;
				ZeroMemory(&depthStencilDesc, sizeof(D3D11_DEPTH_STENCIL_DESC));

				// Depth test parameters
				depthStencilDesc.DepthEnable = true;
				depthStencilDesc.DepthWriteMask = D3D11_DEPTH_WRITE_MASK_ALL;
				depthStencilDesc.DepthFunc = D3D11_COMPARISON_LESS;

				// Stencil test parameters
				depthStencilDesc.StencilEnable = true;
				depthStencilDesc.StencilReadMask = 0xFF;
				depthStencilDesc.StencilWriteMask = 0xFF;

				// Stencil operations if pixel is front-facing
				depthStencilDesc.FrontFace.StencilFailOp = D3D11_STENCIL_OP_KEEP;
				depthStencilDesc.FrontFace.StencilDepthFailOp = D3D11_STENCIL_OP_INCR;
				depthStencilDesc.FrontFace.StencilPassOp = D3D11_STENCIL_OP_KEEP;
				depthStencilDesc.FrontFace.StencilFunc = D3D11_COMPARISON_ALWAYS;

				// Stencil operations if pixel is back-facing.
				depthStencilDesc.BackFace.StencilFailOp = D3D11_STENCIL_OP_KEEP;
				depthStencilDesc.BackFace.StencilDepthFailOp = D3D11_STENCIL_OP_DECR;
				depthStencilDesc.BackFace.StencilPassOp = D3D11_STENCIL_OP_KEEP;
				depthStencilDesc.BackFace.StencilFunc = D3D11_COMPARISON_ALWAYS;
				hr = D3D11Device->CreateDepthStencilState(&depthStencilDesc, &DepthStencilState);
				CHECKRETURN(hr, TEXT("CreateDepthStencilState"));
				/****/

				D3D11_DEPTH_STENCIL_VIEW_DESC descDSV;
				ZeroMemory(&descDSV, sizeof(D3D11_DEPTH_STENCIL_VIEW_DESC));
				descDSV.Format = desc.Format;
				descDSV.ViewDimension = D3D11_DSV_DIMENSION_TEXTURE2D;
				descDSV.Texture2D.MipSlice = 0;

				hr = D3D11Device->CreateDepthStencilView(depthStencilTexture.Get(), &descDSV, &DepthStencilView);
				CHECKRETURN(hr, TEXT("CreateDepthStencilView"));
			}
		}

		private:
		void SetViewport(ID3D11DeviceContext* context) {
			if (context) {
				RECT rect;
				GetClientRect(hWnd, &rect);
				Viewport vp(0, 0, ceilf((float)rect.right - (float)rect.left), ceilf((float)rect.bottom - (float)rect.top));
				// 設定Render好的場景要畫在backbuffer的哪個區域(通常是全部的backbuffer區域)
				context->RSSetViewports(1, vp.Get11());
			}
		}

		private:
		void LoadShader(ID3D11DeviceContext* context) {		
			if (D3D11Device.Get()) {
				HRESULT hr;
				ShaderCode vertexShaderCode;
				vertexShaderCode.LoadFromFile(TEXT("VertexShader.cso"));
				hr = D3D11Device->CreateVertexShader(vertexShaderCode.Code, vertexShaderCode.Length, nullptr, &VertexShader);
				CHECKRETURN(hr, TEXT("CreateVertexShader"));

				D3D11_INPUT_ELEMENT_DESC layout[] =
				{
					{ "POSITION", 0, DXGI_FORMAT_R32G32B32A32_FLOAT, 0, 0,	D3D11_INPUT_PER_VERTEX_DATA, 0 },
					{ "COLOR",    0, DXGI_FORMAT_R32G32B32A32_FLOAT, 0, 16, D3D11_INPUT_PER_VERTEX_DATA, 0 },
					{ "TEXCOORD", 0, DXGI_FORMAT_R32G32_FLOAT,		 0,	32, D3D11_INPUT_PER_VERTEX_DATA, 0 }
				};
				
				// Create the input layout
				hr = D3D11Device->CreateInputLayout(layout, sizeof(layout) / sizeof(D3D11_INPUT_ELEMENT_DESC), vertexShaderCode.Code,
					vertexShaderCode.Length, VertexLayout.ReleaseAndGetAddressOf());
				CHECKRETURN(hr, TEXT("Create VertexLayout"));

				// Set the input layout to the input-assembler
				context->IASetInputLayout(VertexLayout.Get());

				// Load Shader bytecode
				ShaderCode pixelShaderCode;
				pixelShaderCode.LoadFromFile(TEXT("PixelShader.cso"));

				// Get Shader Reflection
				hr = D3DReflect(pixelShaderCode, pixelShaderCode, IID_PPV_ARGS(&Reflector));
				CHECKRETURN(hr, TEXT("Create Shader Reflection"));
				D3D11_SHADER_DESC shaderDesc;
				hr = Reflector->GetDesc(&shaderDesc);
				CHECKRETURN(hr, TEXT("Get Shader Description"));

				// Create Shader
				hr = D3D11Device->CreatePixelShader(pixelShaderCode, pixelShaderCode, nullptr, &PixelShader);
				CHECKRETURN(hr, TEXT("CreatePixelShader"));

				// Set Shader
				context->VSSetShader(VertexShader.Get(), nullptr, 0);
				context->PSSetShader(PixelShader.Get(), nullptr, 0);

				//ShaderCode shaderCode;
				//shaderCode.LoadFromFile(TEXT("Sample.cso"));
				//ComPtr<ID3D11VertexShader> shader;
				//hr = D3D11Device->CreateVertexShader(shaderCode.Code, shaderCode.Length, nullptr, &shader);
				//CHECKRETURN(hr, TEXT("Create Sample.fx VertexShader"));
			}
		}

		private:
		void PrepareData() {
			
			HRESULT hr;
			RECT rect;
			GetClientRect(hWnd, &rect);
			float w = abs((float)rect.right - (float)rect.left);
			float h = abs((float)rect.bottom - (float)rect.top);

			// Hierarchy
			// http://help.autodesk.com/view/FBX/2018/ENU/?guid=FBX_Developer_Help_importing_and_exporting_a_scene_importing_a_scene_html
			if (FbxNode* fbxRootNode = fbxScene->GetRootNode()) {
				for (int i = 0; i < fbxRootNode->GetChildCount(); i++) {
					FbxNode* fbxChildNode = fbxRootNode->GetChild(i);
					if (fbxChildNode->GetNodeAttribute() == NULL) continue;
					const char* name = fbxChildNode->GetName();
					if (strcmp(name, "film_camera") == 0) {
						fbxChildNode = fbxChildNode->GetChild(0);
						name = fbxChildNode->GetName();
						for (int k = 0; k < fbxChildNode->GetChildCount(); k++) {
							
							FbxNode* child = fbxChildNode->GetChild(k);
							if (child->GetNodeAttribute() == NULL) continue;
							name = child->GetName();
							FbxNodeAttribute::EType AttributeType = child->GetNodeAttribute()->GetAttributeType();

							if (AttributeType == FbxNodeAttribute::eMesh) {
								FbxMesh* mesh = (FbxMesh*)child->GetNodeAttribute();
								FbxVector4* fbxVertices = mesh->GetControlPoints();
								int controlPointsCount = mesh->GetControlPointsCount();

								for (int j = 0; j < controlPointsCount; j++) {
									SimpleVertex v;
									v.Position = XMFLOAT4(
										(float)fbxVertices[j].mData[0],
										(float)fbxVertices[j].mData[1],
										(float)fbxVertices[j].mData[2],
										1.0f);
									vertices.push_back(v);
								}

								int cPolygonCount = mesh->GetPolygonCount();
								for (int j = 0; j < cPolygonCount; j++) {
									int iNumVertices = mesh->GetPolygonSize(j);

									for (int k = 0; k < iNumVertices; k++) {
										int iControlPointIndex = mesh->GetPolygonVertex(j, k);
										indices.push_back(iControlPointIndex);
									}
								}
							}
						}
						break;
					}
				}
			}


			// 模型資料
			//SimpleVertex vertices[] =
			//{
			//	XMFLOAT4(-w / 5.0f, h / 5.0f, 200.0f, 1.0f), XMFLOAT4(1.0f, 0.0f, 0.0f, 1.0f), XMFLOAT2(0.0f, 0.0f),
			//	XMFLOAT4(w / 5.0f, h / 5.0f, 200.0f, 1.0f), XMFLOAT4(0.0f, 1.0f, 0.0f, 1.0f), XMFLOAT2(1.0f, 0.0f),
			//	XMFLOAT4(-w / 5.0f, -h / 5.0f, 200.0f, 1.0f), XMFLOAT4(0.0f, 0.0f, 1.0f, 1.0f), XMFLOAT2(0.0f, 1.0f),
			//	XMFLOAT4(w / 5.0f, -h / 5.0f, 200.0f, 1.0f), XMFLOAT4(1.0f, 1.0f, 1.0f, 1.0f), XMFLOAT2(1.0f, 1.0f),
			//	XMFLOAT4(-w / 8.0f, h / 8.0f, -200.0f, 1.0f), XMFLOAT4(1.0f, 0.0f, 0.0f, 1.0f), XMFLOAT2(0.0f, 0.0f),
			//	XMFLOAT4(w / 8.0f, h / 8.0f, -200.0f, 1.0f), XMFLOAT4(0.0f, 1.0f, 0.0f, 1.0f), XMFLOAT2(1.0f, 0.0f),
			//	XMFLOAT4(-w / 8.0f, -h / 8.0f, -200.0f, 1.0f), XMFLOAT4(0.0f, 0.0f, 1.0f, 1.0f), XMFLOAT2(0.0f, 1.0f),
			//	XMFLOAT4(w / 8.0f, -h / 8.0f, -200.0f, 1.0f), XMFLOAT4(1.0f, 1.0f, 1.0f, 1.0f), XMFLOAT2(1.0f, 1.0f),
			//};

			// 建立模型頂點緩衝區
			D3D11_BUFFER_DESC bd;
			ZeroMemory(&bd, sizeof(bd));
			bd.Usage = D3D11_USAGE_DEFAULT;
			bd.ByteWidth = vertices.size() * sizeof(SimpleVertex);
			bd.BindFlags = D3D11_BIND_VERTEX_BUFFER;
			bd.CPUAccessFlags = 0;
			D3D11_SUBRESOURCE_DATA srd;
			ZeroMemory(&srd, sizeof(srd));
			srd.pSysMem = vertices.data();
			hr = D3D11Device->CreateBuffer(&bd, &srd, &VertexBuffer);
			CHECKRETURN(hr, TEXT("Create VertexBuffer"));

			// 建立模型頂點索引緩衝區
			D3D11_BUFFER_DESC indexDesc;
			ZeroMemory(&indexDesc, sizeof(indexDesc));
			indexDesc.ByteWidth = indices.size() * sizeof(int);
			indexDesc.BindFlags = D3D11_BIND_INDEX_BUFFER;
			indexDesc.Usage = D3D11_USAGE_DEFAULT;
			indexDesc.CPUAccessFlags = 0;
			D3D11_SUBRESOURCE_DATA indexsrd;
			ZeroMemory(&indexsrd, sizeof(indexsrd));
			indexsrd.pSysMem = indices.data();
			hr = D3D11Device->CreateBuffer(&indexDesc, &indexsrd, &IndexBuffer);
			CHECKRETURN(hr, TEXT("Create IndexBuffer"));

			// 建立常量緩衝區
			D3D11_BUFFER_DESC constDesc;
			ZeroMemory(&constDesc, sizeof(constDesc));
			constDesc.ByteWidth = sizeof(XMMATRIX);
			constDesc.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
			constDesc.Usage = D3D11_USAGE_DEFAULT;
			constDesc.CPUAccessFlags = 0;
			hr = D3D11Device->CreateBuffer(&constDesc, NULL, &ConstantBuffer);
			CHECKRETURN(hr, TEXT("Create ConstBuffer"));
		}

		private:
		void SetupPipeline(ID3D11DeviceContext* context) {
			UINT stride = sizeof(SimpleVertex);
			UINT offset = 0;
			
			context->IASetVertexBuffers(0, 1, VertexBuffer.GetAddressOf(), &stride, &offset);
			context->IASetIndexBuffer(IndexBuffer.Get(), DXGI_FORMAT_R32_UINT, 0);
			context->VSSetConstantBuffers(0, 1, ConstantBuffer.GetAddressOf());

			// 設定初始狀態
			eye = Vector3(0.0f, 0.0f, -150.0f);
			focus_target = Vector3(0.0f, 0.0f, 0.0f);
			up = Vector3(0.0f, 1.0f, 0.0f);

			// 使用右手坐標系
			view = Matrix::CreateLookAt(eye, focus_target, up);



			RECT rect;
			GetClientRect(hWnd, &rect);
			float width = ceilf(((float)rect.right - (float)rect.left) / 500.0f);
			float height = ceilf(((float)rect.bottom - (float)rect.top) / 500.0f);
			projection = XMMatrixPerspectiveRH(width, height, nearZ, farZ);

			// 設置變換矩陣給著色器
			Matrix transform = world * view * projection;
			context->UpdateSubresource(ConstantBuffer.Get(), 0, NULL, &transform, 0, 0);

			// 設定多邊形拓樸類型
			context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_LINESTRIP);

			// 把DepthStencilView綁定到Output-Merger
			context->OMSetDepthStencilState(DepthStencilState.Get(), 1);
			context->OMSetRenderTargets(1, RenderTargetView.GetAddressOf(), DepthStencilView.Get());
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
				hr = D2DDeviceContext->CreateSolidColorBrush(D2D1::ColorF(D2D1::ColorF::Crimson), TextBrush.GetAddressOf());
				CheckFailed(hr, TEXT("Create Text Brush"));
			}
		}

		private:
		void Direct2DRneder() {
			if (D2DDeviceContext.Get()) {
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
		}

		private:
		void HandleUserControl() {
			MSG msg;
			if (PeekMessage(&msg, NULL, 0, 0, PM_REMOVE)) {
				float offsetX = 0.0f, offsetY = 0.0f;

				switch (msg.message) {
					case WM_KEYDOWN:
					{
						switch (LOBYTE(msg.wParam))
						{
						case 'W':
							offsetY = 10.0f;
							break;
						case 'S':
							offsetY = -10.0f;
							break;
						case 'A':
							offsetX = -10.0f;
							break;
						case 'D':
							offsetX = 10.0f;
							break;
						case VK_PROCESSKEY: // IME key
							break;
						default:
							break;
						}
						world = world * XMMatrixTranslation(offsetX, offsetY, 0.0f);
					}
					break;
					case WM_CHAR:
						OutputDebug(TEXT("Char = %c\n"), LODWORD(msg.wParam));
						break;
					case WM_MOUSEWHEEL:
					{
						int x = (SHORT)HIWORD(msg.wParam) / 120;
						Matrix scale = Matrix::CreateScale(1.0f - 0.1f * x);
						eye = Vector3::Transform(eye, scale);
						view = Matrix::CreateLookAt(eye, focus_target, up);
					}
					break;
					case WM_LBUTTONDOWN:
						point = MAKEPOINTS(msg.lParam);
						break;
					case WM_MOUSEMOVE:
						if (msg.wParam & MK_LBUTTON) {

							POINTS p = MAKEPOINTS(msg.lParam);
							POINTS offs;
							offs.x = p.x - point.x;
							offs.y = p.y - point.y;
							point = p;

							Matrix rotate = Matrix::CreateFromYawPitchRoll(-offs.x * XM_PI / 180.0f / 3.0f, offs.y * XM_PI / 180.0f / 3.0f, 0.0f);
							eye = Vector3::Transform(eye, rotate);
							view = Matrix::CreateLookAt(eye, focus_target, up);
						}
						break;
				}
			}
		}

		private:
		void GetFPS() {
			LARGE_INTEGER now;
			QueryPerformanceCounter(&now);
			__int64 ElapsedCount = (now.QuadPart - time.QuadPart);
			double Elapsed = ElapsedCount * 1000.0 / freq.QuadPart;
			const double UpdatePeriod = 100.0;
			if (Elapsed > UpdatePeriod) {
				double fps = 1000.0 * fpsCounter / Elapsed;
				fpsString.Format(TEXT("%.2lf"), fps);
				fpsCounter = 0;
				time = now;
			}
			fpsCounter++;
		}

		private:
		void Render() {
			
			if (indices.size()) {
				ImmediateContext->OMSetRenderTargets(1, RenderTargetView.GetAddressOf(), nullptr);
				ImmediateContext->ClearDepthStencilView(DepthStencilView.Get(), D3D11_CLEAR_DEPTH, 1.0f, 0);
				FLOAT color[4] = { 47 / 255.0f, 51 / 255.0f, 61 / 255.0f, 255 / 255.0f };
				ImmediateContext->ClearRenderTargetView(RenderTargetView.Get(), color);

				XMMATRIX transform;
				transform = world * view * projection;
				ImmediateContext->UpdateSubresource(ConstantBuffer.Get(), 0, NULL, &transform, 0, 0);

				if (SamplerState.Get() && ResourceView.Get()) {
					ImmediateContext->PSSetSamplers(0, 1, SamplerState.GetAddressOf());
					ImmediateContext->PSSetShaderResources(0, 1, ResourceView.GetAddressOf());
				}

				ImmediateContext->DrawIndexed(indices.size(), 0, 0);

				GetFPS();
				Direct2DRneder();

				// 把畫好的結果輸出到螢幕上！
				SwapChain->Present(0, Tearing ? DXGI_PRESENT_ALLOW_TEARING : 0);
			}
		}

		public:
		HANDLE StartGameLoop(HWND hWnd) {
			if (Running == false) {
				Running = true;
				Initialize(hWnd);
				return CreateThread(NULL, 0, GameLoop, this, 0, NULL);
			}
			return NULL;
		}

		private:
		void Run() {
			while (Running) {
				HandleUserControl();
				Render();
			}
		}

		public:
		void StopGameLoop() {
			Running = false;
		}

		private:
		static DWORD WINAPI GameLoop(PVOID pParam) {
			DirectXPanel* _this = (DirectXPanel*)pParam;
			_this->Run();
			return 0;
		}

		private:
		static DWORD WINAPI CreateDeferredContext(PVOID pParam) {

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
			
			HRESULT hr;
			hr = CreateWICTextureFromMemory(D3D11Device.Get(), ImmediateContext.Get(), data, size, nullptr, &ResourceView);
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
		}

		public:
		void OpenImage() {
			OPENFILENAME ofn;
			ZeroMemory(&ofn, sizeof(ofn));
			ofn.lStructSize = sizeof(ofn);
			ofn.nMaxFile = 1 * MAX_PATH;
			ofn.lpstrFile = new TCHAR[ofn.nMaxFile];
			ofn.lpstrFile[0] = TEXT('\0');
			ofn.lpstrFilter = TEXT("Image Files\0*.png;*.jpg;*.bmp\0\0");

			DWORD len;
			GetPathMyPictures(NULL, &len);
			TCHAR* pictureFolder = (TCHAR*)new BYTE[len];
			GetPathMyPictures(pictureFolder, &len);
			ofn.lpstrInitialDir = pictureFolder;

			ofn.Flags = OFN_EXPLORER | OFN_FILEMUSTEXIST;

			if (GetOpenFileName(&ofn)) {

				HANDLE hFile = CreateFile(ofn.lpstrFile, GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
				if (hFile) {
					LARGE_INTEGER fileSize;
					GetFileSizeEx(hFile, &fileSize);
					DWORD size = fileSize.LowPart;
					HGLOBAL hData = GlobalAlloc(GMEM_FIXED, size);
					if (hData) {
						DWORD nRead;
						if (ReadFile(hFile, hData, size, &nRead, NULL)) {
							OutputDebug(TEXT("Create Texture %s\n"), ofn.lpstrFile);
							CreateTexture((BYTE*)hData, size);
						}
						GlobalFree(hData);
					}
					CloseHandle(hFile);
				}

				//FILE* file;
				//errno_t err = _tfopen_s(&file, ofn.lpstrFile, TEXT("rb"));
				//if (err == 0) {

				//	fseek(file, 0, SEEK_END);
				//	long len = 0;
				//	len = ftell(file);
				//	rewind(file);

				//	char* buffer = new char[len];
				//	char* b = buffer;
				//	size_t _len = len;
				//	size_t nread;

				//	while (_len) {
				//		nread = fread(b, 1, _len, file);
				//		b += nread;
				//		_len -= nread;
				//	}
				//	
				//	fclose(file);
				//	CreateTexture((BYTE*)buffer, len);
				//	delete[] buffer;
				//}
			}
		}

		private:
		void GetWMIData() {
			HRESULT hr;
			hr = CoInitializeSecurity(
				NULL,                       // security descriptor
				-1,                          // use this simple setting
				NULL,                        // use this simple setting
				NULL,                        // reserved
				RPC_C_AUTHN_LEVEL_DEFAULT,   // authentication level  
				RPC_C_IMP_LEVEL_IMPERSONATE, // impersonation level
				NULL,                        // use this simple setting
				EOAC_NONE,                   // no special capabilities
				NULL);                          // reserved
			CHECKRETURN(hr, TEXT("CoInitializeSecurity"));

			ComPtr<IWbemLocator> Locator;
			hr = CoCreateInstance(
				CLSID_WbemLocator,
				NULL,
				CLSCTX_INPROC_SERVER,
				IID_PPV_ARGS(&Locator));

			CHECKRETURN(hr, TEXT("CoCreateInstance IWbemLocator"));

			ComPtr<IWbemServices> Services;
			hr = Locator->ConnectServer(
				_bstr_t(L"ROOT\\CIMV2"), // Object path of WMI namespace
				NULL,                    // User name. NULL = current user
				NULL,                    // User password. NULL = current
				0,                       // Locale. NULL indicates current
				NULL,                    // Security flags.
				0,                       // Authority (for example, Kerberos)
				0,                       // Context object 
				&Services                // pointer to IWbemServices proxy
			);

			CHECKRETURN(hr, TEXT("Fail to Connect WMI Server"));

			OutputDebug(TEXT("Connected to ROOT\\CIMV2 WMI namespace\n"));


			ComPtr<IEnumWbemClassObject> Enumerator;
			hr = Services->ExecQuery(
				bstr_t("WQL"),
				bstr_t("SELECT * FROM Win32_Processor"),
				WBEM_FLAG_FORWARD_ONLY | WBEM_FLAG_RETURN_IMMEDIATELY,
				NULL,
				&Enumerator);

			CHECKRETURN(hr, TEXT("Exec WMI Query"));


			
			ULONG uReturn = 0;

			while (Enumerator.Get())
			{
				IWbemClassObject* clsObj;
				HRESULT hr = Enumerator->Next(WBEM_INFINITE, 1,
					&clsObj, &uReturn);

				if (0 == uReturn)
				{
					break;
				}

				VARIANT vtProp;

				// Get the value of the Name property
				hr = clsObj->Get(L"NumberOfCores", 0, &vtProp, 0, 0);
				if (SUCCEEDED(hr) && vtProp.vt == VT_I4) {
					Info->NumberOfCores = vtProp.intVal;
					OutputDebug(TEXT("NumberOfCores : %d\n"), vtProp.intVal);
				}
				hr = clsObj->Get(L"NumberOfLogicalProcessors", 0, &vtProp, 0, 0);
				if (SUCCEEDED(hr) && vtProp.vt == VT_I4) {
					Info->NumberOfLogicalProcessors = vtProp.intVal;
					OutputDebug(TEXT("NumberOfLogicalProcessors : %d\n"), vtProp.intVal);
				}

				VariantClear(&vtProp);
				clsObj->Release();
			}
		}

		public:
		bool ToggleTearing() {
			if (TearingSupport) {
				Tearing = !Tearing;
			}
			return Tearing;
		}

		public:
		bool GetTearing() {
			return Tearing;
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
		void PrintFBXHierarchy(FbxNode* node, int h = 0) {
			
			int count = node->GetChildCount();

			if (h > 0 && node->GetNodeAttribute()) {
				FbxNodeAttribute::EType AttributeType = node->GetNodeAttribute()->GetAttributeType();

				const char* name = node->GetName();
				String t(strlen(name));
				MultiByteToWideChar(CP_ACP, MB_PRECOMPOSED, name, -1, (wchar_t*)t, t.Length());;
				for (int j = 1; j < h; j++) OutputDebug(TEXT("　　"));
				OutputDebug(TEXT("└─"));
				OutputDebug(TEXT(" %s (%d)\n"), t.c_str(), AttributeType);

				if (AttributeType == FbxNodeAttribute::eNull) {
					for (int i = 0; i < count; i++) {
						FbxNode* n = node->GetChild(i);
						PrintFBXHierarchy(n, h + 1);
					}
				}
			} else {
				for (int i = 0; i < count; i++) {
					FbxNode* n = node->GetChild(i);
					PrintFBXHierarchy(n, h + 1);
				}
			}
		}

		private: 
		void Clear() {
			if (fbxScene) {
				fbxScene->Destroy();
			}
			if (fbxSdkManager) {
				fbxSdkManager->Destroy();
			}
			TextBrush.Reset();
			InfoTextFormat.Reset();
			FPSFormat.Reset();
			DWriteFactory.Reset();
			SamplerState.Reset();
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
		/// 關於ComPtr, 建立新的東西就用ReleaseAndGetAddressOf() 等同於 '&'; 如果只是想要參考那就用GetAddressOf()
		ComPtr<IDXGIFactory> DXGIFactory;
		D3D_FEATURE_LEVEL FeatureLevel;
		ComPtr<ID3D11Device> D3D11Device;
		std::vector<UINT> MsaaQualities;

		ComPtr<ID3D11DeviceContext> ImmediateContext;
		ComPtr<IDXGISwapChain1> SwapChain;
		DXGI_SWAP_CHAIN_DESC1 SwapChainDesc;
		ComPtr<ID2D1DeviceContext> D2DDeviceContext;

		ComPtr<ID3D11RenderTargetView> RenderTargetView;
		ComPtr<ID3D11DepthStencilView> DepthStencilView;
		ComPtr<ID3D11DepthStencilState> DepthStencilState;
		ComPtr<ID3D11InputLayout> VertexLayout;
		ComPtr<ID3D11VertexShader> VertexShader;
		ComPtr<ID3D11PixelShader> PixelShader;
		ComPtr<ID3D11ShaderReflection> Reflector;
		ComPtr<ID3D11ShaderResourceView> ResourceView;

		ComPtr<ID3D11Buffer> VertexBuffer;
		ComPtr<ID3D11Buffer> IndexBuffer;
		ComPtr<ID3D11Buffer> ConstantBuffer;
		ComPtr<ID3D11SamplerState> SamplerState;

		BOOL TearingSupport = false;
		D3D11_FEATURE_DATA_THREADING ThreadingSupport;
		bool Running = false;
		bool Tearing = false;

		ComPtr<IDWriteFactory> DWriteFactory;
		ComPtr<IDWriteTextFormat> FPSFormat;
		ComPtr<IDWriteTextFormat> InfoTextFormat;
		ComPtr<ID2D1SolidColorBrush> TextBrush;

		ComPtr<IWICImagingFactory> WICImagingFactory;

		unique_ptr<DeviceInfo> Info;
		int fpsCounter = 0;
		LARGE_INTEGER time;
		LARGE_INTEGER freq;
		String fpsString;
		Matrix world;
		Matrix view;
		Matrix projection;
		Vector3 eye;
		Vector3 focus_target;
		Vector3 up;
		float nearZ = 5.0f; 
		float farZ = 10000.0f;
		POINTS point;
		FbxScene* fbxScene;
		FbxManager* fbxSdkManager;
		vector<SimpleVertex> vertices;
		vector<int> indices;
	};
}