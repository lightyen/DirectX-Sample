// Sample.cpp : 定義應用程式的進入點。
//

#include "stdafx.h"
#include "Sample.h"
#include "Include\DirectXEnvironment.h"
using namespace MyGame;
#define MAX_LOADSTRING 100

// 全域變數: 
HINSTANCE hInst;                                // 目前執行個體
WCHAR szTitle[MAX_LOADSTRING];                  // 標題列文字
WCHAR szWindowClass[MAX_LOADSTRING];            // 主視窗類別名稱
HWND hWnd;
HANDLE RenderThread;
BOOL Exit;
BOOL IsFocus;
LARGE_INTEGER time;
LARGE_INTEGER frequency;
DirectXPanel helloworld;

// 這個程式碼模組中所包含之函式的向前宣告: 
ATOM                MyRegisterClass(HINSTANCE hInstance);
BOOL                InitInstance(HINSTANCE, int);
LRESULT CALLBACK    WndProc(HWND, UINT, WPARAM, LPARAM);
INT_PTR CALLBACK    About(HWND, UINT, WPARAM, LPARAM);

int APIENTRY wWinMain(_In_ HINSTANCE hInstance,
                     _In_opt_ HINSTANCE hPrevInstance,
                     _In_ LPWSTR    lpCmdLine,
                     _In_ int       nCmdShow)
{
    UNREFERENCED_PARAMETER(hPrevInstance);
    UNREFERENCED_PARAMETER(lpCmdLine);

    // TODO: 在此置入程式碼。

    // 初始化全域字串
    LoadStringW(hInstance, IDS_APP_TITLE, szTitle, MAX_LOADSTRING);
    LoadStringW(hInstance, IDC_SAMPLE, szWindowClass, MAX_LOADSTRING);
    MyRegisterClass(hInstance);

    // 執行應用程式初始設定: 
    if (!InitInstance (hInstance, nCmdShow))
    {
        return FALSE;
    }

    HACCEL hAccelTable = LoadAccelerators(hInstance, MAKEINTRESOURCE(IDC_SAMPLE));

    MSG msg;

    // 主訊息迴圈: 
    while (GetMessage(&msg, nullptr, 0, 0))
    {
        if (!TranslateAccelerator(msg.hwnd, hAccelTable, &msg))
        {
            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }
    }

    return (int) msg.wParam;
}



//
//  函式: MyRegisterClass()
//
//  用途: 註冊視窗類別。
//
ATOM MyRegisterClass(HINSTANCE hInstance)
{
    WNDCLASSEXW wcex;

    wcex.cbSize = sizeof(WNDCLASSEX);

    wcex.style          = CS_HREDRAW | CS_VREDRAW;
    wcex.lpfnWndProc    = WndProc;
    wcex.cbClsExtra     = 0;
    wcex.cbWndExtra     = 0;
    wcex.hInstance      = hInstance;
    wcex.hIcon          = LoadIcon(hInstance, MAKEINTRESOURCE(IDI_SAMPLE));
    wcex.hCursor        = LoadCursor(nullptr, IDC_ARROW);
    wcex.hbrBackground  = (HBRUSH)(COLOR_WINDOW+1);
    wcex.lpszMenuName   = MAKEINTRESOURCEW(IDC_SAMPLE);
    wcex.lpszClassName  = szWindowClass;
    wcex.hIconSm        = LoadIcon(wcex.hInstance, MAKEINTRESOURCE(IDI_SMALL));

    return RegisterClassExW(&wcex);
}

//
//   函式: InitInstance(HINSTANCE, int)
//
//   用途: 儲存執行個體控制代碼並且建立主視窗
//
//   註解: 
//
//        在這個函式中，我們會將執行個體控制代碼儲存在全域變數中，
//        並且建立和顯示主程式視窗。
//
BOOL InitInstance(HINSTANCE hInstance, int nCmdShow)
{
   hInst = hInstance; // 將執行個體控制代碼儲存在全域變數中

   hWnd = CreateWindowW(szWindowClass, szTitle, WS_OVERLAPPEDWINDOW | (WS_MINIMIZEBOX | WS_MAXIMIZEBOX),
      CW_USEDEFAULT, 0, CW_USEDEFAULT, 0, nullptr, nullptr, hInstance, nullptr);

   if (!hWnd)
   {
      return FALSE;
   }

   ShowWindow(hWnd, SW_MAXIMIZE);

   RenderThread = helloworld.StartGameLoop(hWnd);

   UpdateWindow(hWnd);

   return TRUE;
}

//
//  函式: WndProc(HWND, UINT, WPARAM, LPARAM)
//
//  用途:     處理主視窗的訊息。
//
LRESULT CALLBACK WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam)
{
    switch (message)
    {
	case WM_CREATE:
		QueryPerformanceCounter(&time);
		QueryPerformanceFrequency(&frequency);
		break;
	case WM_SIZE:
		return FALSE;
    case WM_COMMAND:
        {
            int wmId = LOWORD(wParam);
            // 剖析功能表選取項目: 
            switch (wmId)
            {
            case IDM_ABOUT:
                DialogBox(hInst, MAKEINTRESOURCE(IDD_ABOUTBOX), hWnd, About);
                break;
			case IDM_OPENIMAGE:
				helloworld.OpenImage();
				break;
			case IDM_SAVEIMAGE:
				break;
			case IDM_TEST:
				helloworld.Test();
				break;
			case IDM_TEARING:
				if (helloworld.ToggleTearing()) {
					CheckMenuItem(GetMenu(hWnd), IDM_TEARING, MF_UNCHECKED);
				} else {
					CheckMenuItem(GetMenu(hWnd), IDM_TEARING, MF_CHECKED);
				}
				break;
            case IDM_EXIT:
				Exit = TRUE;
                DestroyWindow(hWnd);
                break;
            default:
                return DefWindowProc(hWnd, message, wParam, lParam);
            }
        }
        break;
	case WM_KEYDOWN:
		switch (LOBYTE(wParam)) {
			case VK_ESCAPE:
				Exit = TRUE;
				DestroyWindow(hWnd);
				break;
			default:
				if (!PostThreadMessage(GetThreadId(RenderThread), message, wParam, lParam))
				{
					OutputDebugString(TEXT("Post Message Failed\n"));
				}
				break;
		}
		break;
	case WM_CHAR:
		if (!PostThreadMessage(GetThreadId(RenderThread), message, wParam, lParam))
		{
			OutputDebugString(TEXT("Post Message Failed\n"));
		}
		break;
	case WM_MOUSEWHEEL:
		if (!PostThreadMessage(GetThreadId(RenderThread), message, wParam, lParam)) {
			OutputDebugString(TEXT("Post Message Failed\n"));
		}
		break;
	case WM_LBUTTONDOWN:
	case WM_RBUTTONDOWN:
		if (!PostThreadMessage(GetThreadId(RenderThread), message, wParam, lParam)) {
			OutputDebugString(TEXT("Post Message Failed\n"));
		} else {
			OutputDebugString(TEXT("Button up down\n"));
		}
		break;
	case WM_MOUSEMOVE:
		if ((wParam & MK_LBUTTON) && IsFocus)
		{
			LARGE_INTEGER now;
			QueryPerformanceCounter(&now);
			__int64 ElapsedCount = (now.QuadPart - time.QuadPart);
			double Elapsed = ElapsedCount * 1000.0 / frequency.QuadPart;
			if (Elapsed > 16.0f) { // 如果輸入頻率超過60FPS就忽略
				if (!PostThreadMessage(GetThreadId(RenderThread), message, wParam, lParam)) {
					OutputDebugString(TEXT("Post Message Failed\n"));
				}
				time = now;
			}
		}
		break;
	case WM_KILLFOCUS:
		if (!Exit) {
			SuspendThread(RenderThread);
			IsFocus = false;
			OutputDebugString(TEXT("lost focus\n"));
		}
		break;
	case WM_SETFOCUS:
		if (!Exit) {
			ResumeThread(RenderThread);
			IsFocus = true;
			OutputDebugString(TEXT("got focus\n"));
		}
		break;
	case WM_SYSCOMMAND:
		{
			switch (wParam)
			{
			case SC_CLOSE:
				Exit = TRUE;
				DestroyWindow(hWnd);
				break;
			default:
				return DefWindowProc(hWnd, message, wParam, lParam);
			}
		}
		break;
    case WM_DESTROY:
		helloworld.StopGameLoop();
		WaitForSingleObject(RenderThread, INFINITE);
        PostQuitMessage(0);
        break;
    default:
        return DefWindowProc(hWnd, message, wParam, lParam);
    }
    return 0;
}

// [關於] 方塊的訊息處理常式。
INT_PTR CALLBACK About(HWND hDlg, UINT message, WPARAM wParam, LPARAM lParam)
{
    UNREFERENCED_PARAMETER(lParam);
    switch (message)
    {
    case WM_INITDIALOG:
		return (INT_PTR)TRUE;
    case WM_COMMAND:
        if (LOWORD(wParam) == IDOK || LOWORD(wParam) == IDCANCEL)
        {
            EndDialog(hDlg, LOWORD(wParam));
            return (INT_PTR)TRUE;
        }
        break;
    }
    return (INT_PTR)FALSE;
}
