#pragma once
#include <DirectXMath.h>
using namespace DirectX;

namespace MyGame {
	struct SimpleVertex {
		XMFLOAT4 Position;
		XMFLOAT4 Color;
		XMFLOAT2 TexCoord;
	};
}