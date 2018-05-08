#pragma once
#include <DirectXMath.h>

namespace DirectX {
	struct SimpleVertex {
		XMFLOAT4 Position;
		XMFLOAT4 Color;
		XMFLOAT2 TexCoord;
	};
}