﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NotificationIcon.NET
{
    internal partial class LinuxNotifyIcon : NotifyIcon
    {
        private const string SO_NAME = "libnotification_icon.so";

        /// <exception cref="InvalidOperationException"></exception>
        public LinuxNotifyIcon(string iconPath, IReadOnlyList<MenuItem> menuItems) : base(SO_NAME, iconPath, menuItems)
        { }

        #region Native
        [LibraryImport(SO_NAME)]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static partial int tray_init(IntPtr tray);

        [LibraryImport(SO_NAME)]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static partial void tray_exit(IntPtr tray);

        [LibraryImport(SO_NAME)]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static partial int tray_loop(int blocking);

        [LibraryImport(SO_NAME)]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static partial void tray_update(IntPtr tray);

        [StructLayout(LayoutKind.Sequential)]
        private struct Tray
        {
            [MarshalAs(UnmanagedType.LPUTF8Str)]
            public string iconPath;
            public IntPtr menus;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TrayMenuItem
        {
            [MarshalAs(UnmanagedType.LPUTF8Str)]
            public string? text;
            public int isDisabled;
            public int isChecked;
            public IntPtr callback;
            public IntPtr context;
            public IntPtr submenu;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void MenuItemCallback(IntPtr menu);
        #endregion

        protected override int TrayInit(nint tray)
        {
            return tray_init(tray);
        }

        protected override void TrayExit(nint tray)
        {
            tray_exit(tray);
        }

        protected override void TrayUpdate(nint tray)
        {
            tray_update(tray);
        }

        protected override int TrayLoop(int blocking)
        {
            return tray_loop(blocking);
        }

        protected override IHeapAlloc AllocateMenuItems()
        {
            return AllocateMenuItems(MenuItems);
        }

        private HeapAlloc<TrayMenuItem> AllocateMenuItems(IReadOnlyList<MenuItem> menuItems)
        {
            List<IDisposable>? dependencies = null;
            TrayMenuItem[] nativeMenuItems = new TrayMenuItem[menuItems.Count + 1];
            for (int i = 0; i < menuItems.Count; i++)
            {
                MenuItem current = menuItems[i];
                nativeMenuItems[i] = new TrayMenuItem()
                {
                    text = current.Text,
                    isDisabled = current.IsDisabled ? 1 : 0,
                    isChecked = current.IsChecked == null ? -1 : (current.IsChecked == true ? 1 : 0),
                    callback = Marshal.GetFunctionPointerForDelegate<MenuItemCallback>(menuPtr => current.OnClick(new MenuItemClickEventArgs(this))),
                };
                if (current.SubMenu != null)
                {
                    var submenu = AllocateMenuItems(current.SubMenu);
                    nativeMenuItems[i].submenu = submenu.Ptr;
                    dependencies ??= new List<IDisposable>();
                    dependencies.Add(submenu);
                }
            }
            return HeapAlloc<TrayMenuItem>.Copy(nativeMenuItems, dependencies);
        }

        protected override IHeapAlloc AllocateTray(nint menuItemsPtr)
        {
            Tray tray = new()
            {
                iconPath = IconPath,
                menus = menuItemsPtr
            };
            return HeapAlloc<Tray>.Copy(tray);
        }
    }
}
