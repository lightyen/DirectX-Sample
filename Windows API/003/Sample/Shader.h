#pragma once

#include "stdafx.h"
#include <stdio.h>
#include <d3dcompiler.h>


class ShaderCode {

public:

	byte* Code;
	size_t Length;

	~ShaderCode() {
		if (Code != nullptr) {
			delete[] Code;
			Code = nullptr;
		}
	}

	bool IsOK = false;

	void LoadFromFile(String file) {
		FILE* stream;
		errno_t err = _tfopen_s(&stream, file, TEXT("rb"));
		if (err == 0) {
			
			int result = fseek(stream, 0, SEEK_END);

			if (result != 0) {
				fclose(stream);
				IsOK = false;
				return;
			}

			size_t file_len = ftell(stream);
			rewind(stream);
			Length = file_len;
			Code = new byte[file_len];
			file_len = fread(Code, 1, file_len, stream);
			if (Length != file_len) {
				fclose(stream);
				return;
			}

			fclose(stream);


			IsOK = true;
			return;
		}
		IsOK = false;
	}

private:
	
	void OutputShaderErrorMessage(ID3D10Blob* message, HWND hWnd) {
		size_t size = message->GetBufferSize();
		const char* msg = (char*)(message->GetBufferPointer());

		char* output = new char[size + 1];
		strncpy_s(output, size + 1, msg, size);
		output[size] = NULL;
		MessageBoxA(hWnd, output, "", 0);
		delete output;
	}

};