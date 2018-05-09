#pragma once
#include "DirectX.h"
#include "String.h"

class DeviceInfo {
public:
	D3D_FEATURE_LEVEL FeatureLevel;
	int VendorId;
	int DeviceId;
	SIZE_T DedicatedVideoMemory;
	String Description;

	String Vender() {
		switch (VendorId) {
		case 0x8086:
			return TEXT("Intel");
		case 0x10DE:
			return TEXT("NVIDIA");
		case 0x1022:
			return TEXT("AMD");
		default:
			return String(TEXT("%4X"), VendorId);
		}
	}

	String D3DVersion() {
		switch (FeatureLevel) {
		case D3D_FEATURE_LEVEL_12_1:
			return TEXT("DirectX 12.1");
		case D3D_FEATURE_LEVEL_12_0:
			return TEXT("DirectX 12.0");
		case D3D_FEATURE_LEVEL_11_1:
			return TEXT("DirectX 11.1");
		case D3D_FEATURE_LEVEL_11_0:
			return TEXT("DirectX 11.0");
		case D3D_FEATURE_LEVEL_10_1:
			return TEXT("DirectX 11.1");
		case D3D_FEATURE_LEVEL_10_0:
			return TEXT("DirectX 10.0");
		case D3D_FEATURE_LEVEL_9_3:
			return TEXT("DirectX 9.3");
		case D3D_FEATURE_LEVEL_9_2:
			return TEXT("DirectX 9.2");
		case D3D_FEATURE_LEVEL_9_1:
			return TEXT("DirectX 9.1");
		default:
			return TEXT("DirectX Not Support");
		}
	}

	const String ToString() {
		if (this != nullptr) {
			String VideoMemory;
			double size = static_cast<double>(DedicatedVideoMemory);
			if (size < (1 << 10)) {
				VideoMemory.Format(TEXT("%u bytes"), DedicatedVideoMemory);
			} else if (size < (1 << 20)) {
				VideoMemory.Format(TEXT("%.0lf KB"), round(size / (1 << 10)));
			} else if (size < (1 << 30)) {
				VideoMemory.Format(TEXT("%.0lf MB"), round(size / (1 << 20)));
			} else {
				VideoMemory.Format(TEXT("%.0lf GB"), round(size / (1 << 30)));
			}

			return String(TEXT("%s\n%s\n%s\n%s"), D3DVersion().c_str(), Vender().c_str(), Description.c_str(), VideoMemory.c_str());
		} else {
			return String(TEXT("unknown device"));
		}
	}
};
