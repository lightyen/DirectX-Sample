// stdafx.h : 可在此標頭檔中包含標準的系統 Include 檔，
// 或是經常使用卻很少變更的
// 專案專用 Include 檔案
//

#pragma once

#include "targetver.h"

#define WIN32_LEAN_AND_MEAN             // 從 Windows 標頭排除不常使用的成員
// Windows 標頭檔: 
#include <tchar.h>
#include <windows.h>
#include <Shlwapi.h>
#include <Strsafe.h>
#include <wincodec.h>
#include <commdlg.h>
#include <wrl\client.h>
using namespace Microsoft::WRL;
// C RunTime 標頭檔
#include <stdio.h>
#include <stdlib.h>
#include <memory>
#include <utility>
#include <vector>
using namespace std;

// TODO:  在此參考您的程式所需要的其他標頭

#include "DirectX.h"
using namespace DirectX;

#include "Include/Exception.h"
#include "Include/Registry.h"