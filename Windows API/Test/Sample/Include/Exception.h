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
			String prev(TEXT("Function:\t%s\nCode:\t0x%08X\nMessage:\t"), FunctionName.c_str(), result);
			String facility;
			String desc;

			/*switch (HRESULT_FACILITY(result)) {
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
			}*/

			LPVOID lpMsgBuf;
			FormatMessage(
				FORMAT_MESSAGE_ALLOCATE_BUFFER |
				FORMAT_MESSAGE_FROM_SYSTEM |
				FORMAT_MESSAGE_IGNORE_INSERTS,
				NULL,
				result,
				MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT),
				(LPTSTR)&lpMsgBuf,
				0, NULL);

			String error_message((LPTSTR)lpMsgBuf);

			MessageBox(NULL, prev + facility + desc + error_message, TEXT("DirectX"), MB_ICONERROR);

			LocalFree(lpMsgBuf);
		}
    };

	BOOL CheckFailed(HRESULT result, LPCTSTR func_name) {
		if (!SUCCEEDED(result)) {
			GraphicsException(result, func_name);
			return TRUE;
		} else return FALSE;
	}
}
