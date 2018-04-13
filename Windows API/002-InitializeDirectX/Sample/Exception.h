#pragma once

#include <windows.h>
#include "String.h"

namespace DirectX
{
	class Exception {

		public:

		HRESULT		result;
		String		FunctionName;
		DWORD		ErrorCode;
		String 		ErrorMessage;

		Exception(HRESULT result, LPCTSTR funcName) noexcept;

		~Exception() = default;
	};

    class GraphicsException : public Exception
    {
		public:

		GraphicsException(HRESULT result, LPCTSTR func);

		void ShowMessage() noexcept;
    };

	BOOL CheckFailed(HRESULT result, LPCTSTR func_name);
}
