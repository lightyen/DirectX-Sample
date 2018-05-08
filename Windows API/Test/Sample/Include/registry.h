#pragma once

#include <windows.h>

HRESULT GetPathPersonal(TCHAR* personalFolder, PDWORD length)
{
	HRESULT result;
	HKEY hKey;
	result = RegOpenKey(
		HKEY_CURRENT_USER,
		TEXT("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Shell Folders"),
		&hKey);
	if (FAILED(result)) return result;

	RegQueryValueEx(hKey, TEXT("Personal"), NULL, NULL, reinterpret_cast<BYTE*>(personalFolder), length);
	RegCloseKey(hKey);
	return result;
}

HRESULT GetPathMyPictures(TCHAR* personalFolder, PDWORD length)
{
	HRESULT result;
	HKEY hKey;

	result = RegOpenKey(
		HKEY_CURRENT_USER,
		TEXT("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Shell Folders"),
		&hKey);
	if (FAILED(result)) return result;

	RegQueryValueEx(hKey, TEXT("My Pictures"), NULL, NULL, reinterpret_cast<BYTE*>(personalFolder), length);
	RegCloseKey(hKey);
	return result;
}
