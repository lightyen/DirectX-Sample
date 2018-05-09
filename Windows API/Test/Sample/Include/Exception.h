#pragma once

#include <windows.h>
#include "String.h"

namespace {
	class Exception {

		public:

		HRESULT		result;
		String		FunctionName;
		DWORD		ErrorCode;
		String 		ErrorMessage;

		Exception(HRESULT result, LPCTSTR funcName) noexcept {
			this->result = result;
			FunctionName.Format(TEXT("%s"), funcName);
			ErrorCode = GetLastError();

			HLOCAL msg;
			// Get LastError Format Message
			FormatMessage(
				FORMAT_MESSAGE_ALLOCATE_BUFFER |
				FORMAT_MESSAGE_FROM_SYSTEM |
				FORMAT_MESSAGE_IGNORE_INSERTS,
				NULL,
				ErrorCode,
				MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT),
				(LPTSTR)&msg,
				0, NULL);

			ErrorMessage.Format(TEXT("%s"), msg);
			LocalFree(msg);
		}

		~Exception() = default;
	};

    class GraphicsException : public Exception
    {
		public:

		GraphicsException(HRESULT result, LPCTSTR func)
			: Exception(result, func) {
			ShowMessage();
		}

		void ShowMessage() noexcept {
			String prev(TEXT("Function:\t%s\nCode:\t0x%08X\n"), FunctionName.c_str(), result);
			String facility;
			String desc;

			switch (HRESULT_FACILITY(result)) {
				case FACILITY_DXGI:
				{
					facility.Format(TEXT("Facility:\t%s\n"), TEXT("DXGI"));
					switch (result) {
					case DXGI_ERROR_INVALID_CALL:
						desc.Format(TEXT("The application provided invalid parameter data; this must be debugged and fixed before the application is released."));
						break;
					}
					break;
				}
			}

			if (desc.IsNullOrEmpty()) {
				switch (result) {
					case E_ABORT:
						desc.Format(TEXT("Operation aborted"));
						break;
					case E_ACCESSDENIED:
						desc.Format(TEXT("General access denied error"));
						break;
					case E_FAIL:
						desc.Format(TEXT("Unspecified failure"));
						break;
					case E_HANDLE:
						desc.Format(TEXT("Handle that is not valid"));
						break;
					case E_INVALIDARG:
						desc.Format(TEXT("One or more arguments are not valid"));
						break;
					case E_NOINTERFACE:
						desc.Format(TEXT("No such interface supported"));
						break;
					case E_NOTIMPL:
						desc.Format(TEXT("Not implemented"));
						break;
					case E_OUTOFMEMORY:
						desc.Format(TEXT("Failed to allocate necessary memory"));
						break;
					case E_POINTER:
						desc.Format(TEXT("Pointer that is not valid"));
						break;
					case E_UNEXPECTED:
						desc.Format(TEXT("Unexpected failure"));
						break;
				}
			}

			MessageBox(NULL, prev + facility + desc, TEXT("DirectX"), MB_ICONERROR);
		}
    };

	BOOL CheckFailed(HRESULT result, LPCTSTR func_name) {
		if (!SUCCEEDED(result)) {
			GraphicsException(result, func_name);
			return TRUE;
		} else return FALSE;
	}
}
