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

struct tray_menu;

struct tray {
	char* icon;
	struct tray_menu* menu;
};

struct tray_menu {
	char* text;
	int disabled;
	int checked; //1 if checked, 0 if unchecked, -1 if cannot be checked

	void (*cb)(struct tray_menu*);
	void* context;

	struct tray_menu* submenu;
};

#if defined (_WIN32) || defined (_WIN64)
#include "tray_windows.h"
#elif defined (__linux__) || defined (linux) || defined (__linux)
#include "tray_linux.h"
#elif defined (__APPLE__) || defined (__MACH__)
#include "tray_apple.h"
#endif
