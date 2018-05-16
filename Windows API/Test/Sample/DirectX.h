#pragma once

#if (_WIN32_WINNT >= _WIN32_WINNT_WIN10)

// Direct3D
#include <d3d11.h>
#include <d3d11_1.h>
#include <d3d11_2.h>
#include <d3d11_3.h>
#include <d3d11_4.h>

// DXGI
#include <dxgi.h>
#include <dxgi1_2.h>
#include <dxgi1_3.h>
#include <dxgi1_4.h>
#include <dxgi1_5.h>

// Direct2D
#include <d2d1.h>
#include <d2d1_1.h>
#include <d2d1_2.h>
#include <d2d1_3.h>

// DirectWrite
#include <Dwrite.h>

// Anything else
#include <DirectXColors.h>
#include <DirectXMath.h>
#include <d3dcompiler.h>
#else
#error 開發環境需要 Windows 10 或者以上版本
#endif

