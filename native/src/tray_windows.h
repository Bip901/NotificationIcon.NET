/*
MIT License

Copyright (c) 2023 Ori Almagor
Copyright (c) 2017 Serge Zaitsev

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

#pragma once
#define UNICODE

#include <windows.h>
#include <shellapi.h>

#define EXPORT __declspec(dllexport)
#define WM_TRAY_CALLBACK_MESSAGE (WM_USER + 1)
#define WC_TRAY_CLASS_NAME L"TRAY"
#define ID_TRAY_FIRST 1000

static WNDCLASSEX wc;
static NOTIFYICONDATA nid;
static HWND hwnd;
static HMENU hmenu = NULL;

static LRESULT CALLBACK _tray_wnd_proc(HWND hwnd, UINT msg, WPARAM wparam, LPARAM lparam) {
	switch (msg) {
	case WM_CLOSE:
		DestroyWindow(hwnd);
		return 0;
	case WM_DESTROY:
		PostQuitMessage(0);
		return 0;
	case WM_TRAY_CALLBACK_MESSAGE:
		if (lparam == WM_LBUTTONUP || lparam == WM_RBUTTONUP) {
			POINT p;
			if (!GetCursorPos(&p)) {
				break;
			}
			SetForegroundWindow(hwnd);
			if (hmenu == NULL) {
				break;
			}
			WORD cmd = TrackPopupMenu(
				hmenu,
				TPM_LEFTALIGN | TPM_RIGHTBUTTON | TPM_RETURNCMD | TPM_NONOTIFY,
				p.x, p.y, 0, hwnd, NULL
			);
			if (cmd != 0) {
				SendMessage(hwnd, WM_COMMAND, cmd, 0);
			}
			return 0;
		}
		break;
	case WM_COMMAND:
		{
			UINT menuItemId = (UINT)wparam;
			if (menuItemId >= ID_TRAY_FIRST) {
				MENUITEMINFO item = {
					.cbSize = sizeof(MENUITEMINFO), .fMask = MIIM_ID | MIIM_DATA,
				};
				if (hmenu == NULL) {
					break;
				}
				if (GetMenuItemInfo(hmenu, menuItemId, FALSE, &item)) {
					struct tray_menu* menu = (struct tray_menu*)item.dwItemData;
					if (menu != NULL && menu->cb != NULL) {
						menu->cb(menu);
					}
				}
				return 0;
			}
		}
		break;
	}
	return DefWindowProc(hwnd, msg, wparam, lparam);
}

static HMENU _tray_menu(struct tray_menu* m, UINT* id) {
	HMENU hmenu = CreatePopupMenu();
	for (; m != NULL && m->text != NULL; m++, (*id)++) {
		if (strcmp(m->text, "-") == 0) {
			InsertMenu(hmenu, *id, MF_SEPARATOR, TRUE, L"");
		}
		else {
			MENUITEMINFO item;
			memset(&item, 0, sizeof(item));
			item.cbSize = sizeof(MENUITEMINFO);
			item.fMask = MIIM_ID | MIIM_TYPE | MIIM_STATE | MIIM_DATA;
			item.fType = 0;
			item.fState = 0;
			if (m->submenu != NULL) {
				item.fMask = item.fMask | MIIM_SUBMENU;
				item.hSubMenu = _tray_menu(m->submenu, id);
			}
			if (m->disabled) {
				item.fState |= MFS_DISABLED;
			}
			if (m->checked && m->checked != -1) {
				item.fState |= MFS_CHECKED;
			}
			item.wID = *id;
			item.dwTypeData = (LPWSTR)m->text;
			item.dwItemData = (ULONG_PTR)m;

			InsertMenuItem(hmenu, *id, TRUE, &item);
		}
	}
	return hmenu;
}

EXPORT void tray_update(struct tray* tray) {
	HMENU prevmenu = hmenu;
	UINT id = ID_TRAY_FIRST;
	hmenu = _tray_menu(tray->menu, &id);
	SendMessage(hwnd, WM_INITMENUPOPUP, (WPARAM)hmenu, 0);
	HICON icon;
	ExtractIconEx((LPCWSTR)tray->icon, 0, NULL, &icon, 1);
	if (nid.hIcon) {
		DestroyIcon(nid.hIcon);
	}
	nid.hIcon = icon;
	Shell_NotifyIcon(NIM_MODIFY, &nid);

	if (prevmenu != NULL) {
		DestroyMenu(prevmenu);
	}
}

EXPORT int tray_init(struct tray* tray) {
	memset(&wc, 0, sizeof(wc));
	wc.cbSize = sizeof(WNDCLASSEX);
	wc.lpfnWndProc = _tray_wnd_proc;
	wc.hInstance = GetModuleHandle(NULL);
	wc.lpszClassName = WC_TRAY_CLASS_NAME;
	if (!RegisterClassEx(&wc)) {
		return -1;
	}

	hwnd = CreateWindowEx(0, WC_TRAY_CLASS_NAME, NULL, 0, 0, 0, 0, 0, 0, 0, 0, 0);
	if (hwnd == NULL) {
		return -1;
	}
	UpdateWindow(hwnd);

	memset(&nid, 0, sizeof(nid));
	nid.cbSize = sizeof(NOTIFYICONDATA);
	nid.hWnd = hwnd;
	nid.uID = 0;
	nid.uFlags = NIF_ICON | NIF_MESSAGE;
	nid.uCallbackMessage = WM_TRAY_CALLBACK_MESSAGE;
	Shell_NotifyIcon(NIM_ADD, &nid);

	tray_update(tray);
	return 0;
}

EXPORT int tray_loop(int blocking) {
	MSG msg;
	if (blocking) {
		if (GetMessage(&msg, NULL, 0, 0) == -1)
		{
			return 7; //Stop loop on GetMessage error
		}
	}
	else {
		PeekMessage(&msg, NULL, 0, 0, PM_REMOVE);
	}
	if (msg.message == WM_QUIT) {
		return -1;
	}
	TranslateMessage(&msg);
	DispatchMessage(&msg);
	return 0;
}

EXPORT DWORD get_current_thread_id() {
	return GetCurrentThreadId();
}

EXPORT void tray_exit_from_another_thread(DWORD ownerThreadId) {
	Shell_NotifyIcon(NIM_DELETE, &nid);
	if (nid.hIcon != 0) {
		DestroyIcon(nid.hIcon);
	}
	if (hmenu != NULL) {
		DestroyMenu(hmenu);
		hmenu = NULL;
	}
	if (ownerThreadId == 0) { //Use current thread
		PostQuitMessage(0);
	}
	else {
		PostThreadMessage(ownerThreadId, WM_QUIT, 0, 0);
	}
	UnregisterClass(WC_TRAY_CLASS_NAME, GetModuleHandle(NULL));
}

EXPORT void tray_exit() {
	tray_exit_from_another_thread(0);
}
