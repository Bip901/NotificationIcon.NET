using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NotificationIcon.NET
{
    /// <summary>
    /// Represents a notification icon in the system "tray" area.
    /// </summary>
    /// <remarks>
    /// This class is NOT thread safe.
    /// In particular, the thread that created this instance should be the one to call <see cref="Show(CancellationToken)"/> and dispose of this object,
    /// although the cancellation token may be signalled from another thread.
    /// <para>
    /// Updates to the properties of this object should be done from the owner thread.
    /// Click events will run on the owner thread.
    /// </para>
    /// </remarks>
    public abstract class NotifyIcon : IDisposable
    {
        private static bool loadedNativeLibrary;

        /// <summary>
        /// A path to an icon on the file system. For Windows, this should be an ICO file. For Unix, this should be a PNG.
        /// </summary>
        public string IconPath
        {
            get => _iconPath;
            set
            {
                if (!string.Equals(_iconPath, value))
                {
                    _iconPath = value;
                    AllocateNewTray(true);
                }
            }
        }
        private string _iconPath;

        /// <summary>
        /// The currently displayed menu items.
        /// </summary>
        public IReadOnlyList<MenuItem> MenuItems
        {
            get => _menuItems;
            set
            {
                _menuItems = value;
                AllocateNewTray(true);
                SubscribeToUpdates(_menuItems);
            }
        }
        private IReadOnlyList<MenuItem> _menuItems;

        private IHeapAlloc trayHandle;
        private IHeapAlloc menuItemsHandle;
        private bool disposed;

        /// <summary>
        /// Creates a new <see cref="NotifyIcon"/>.
        /// </summary>
        /// <param name="iconPath">A path to an icon on the file system. For Windows, this should be an ICO file. For Unix, this should be a PNG.</param>
        /// <exception cref="PlatformNotSupportedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public static NotifyIcon Create(string iconPath, IReadOnlyList<MenuItem> menuItems)
        {
#if PORTABLE
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
#endif
#if PORTABLE || WINDOWS
                return new WindowsNotifyIcon(iconPath, menuItems);
#endif

#if PORTABLE
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
#endif
#if PORTABLE || LINUX
                return new LinuxNotifyIcon(iconPath, menuItems);
#endif
            throw new PlatformNotSupportedException();
        }

        /// <summary>
        /// Returns the non-version-specific Runtime ID of the current system.
        /// </summary>
        /// <returns>A string similar to e.g. "win-x64", "win-x86", "linux-arm64".</returns>
        /// <exception cref="PlatformNotSupportedException"></exception>
        private static string GetNonVersionSpecificRID()
        {
            string rid;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                rid = "win";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                rid = "linux";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                rid = "osx";
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
            return rid + "-" + RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();
        }

        /// <summary>
        /// Creates a new <see cref="NotifyIcon"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        protected NotifyIcon(string nativeLibraryName, string iconPath, IReadOnlyList<MenuItem> menuItems)
        {
            if (!loadedNativeLibrary)
            {
                //Attempt loading from '.' first (if the hosting project was compiled with a RID)
                if (!NativeLibrary.TryLoad(nativeLibraryName, out _))
                {
                    //Fallback to runtimes/{RID}/native (if the hosting project is portable)
                    string rid = GetNonVersionSpecificRID();
                    string path = string.Join(Path.DirectorySeparatorChar, "runtimes", rid, "native", nativeLibraryName);
                    try
                    {
                        NativeLibrary.Load(path);
                    }
                    catch (DllNotFoundException ex)
                    {
                        throw new PlatformNotSupportedException($"Native support not found for platform \"{rid}\".", ex);
                    }
                }
                loadedNativeLibrary = true;
            }
            _iconPath = iconPath;
            _menuItems = menuItems;
            AllocateNewTray(false);
            int tray_init_result = TrayInit(trayHandle.Ptr);
            if (tray_init_result < 0)
            {
                throw new InvalidOperationException("Failed to initialize tray.");
            }
            SubscribeToUpdates(menuItems);
        }

        private void SubscribeToUpdates(IEnumerable<MenuItem> menuItems)
        {
            foreach (MenuItem item in menuItems)
            {
                item.UpdateRequired -= UpdateRequired;
                item.UpdateRequired += UpdateRequired;
                if (item.SubMenu != null)
                {
                    SubscribeToUpdates(item.SubMenu);
                }
            }
        }

        private void UpdateRequired(object? sender, MenuItem.UpdateRequiredEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                AllocateNewTray(true);
                if (e.SubmenuChanged && menuItem.SubMenu != null)
                {
                    SubscribeToUpdates(menuItem.SubMenu);
                }
            }
        }

        [MemberNotNull(nameof(trayHandle))]
        [MemberNotNull(nameof(menuItemsHandle))]
        private void AllocateNewTray(bool update)
        {
            using (trayHandle)
            {
                using (menuItemsHandle)
                {
                    menuItemsHandle = AllocateMenuItems();
                    trayHandle = AllocateTray(menuItemsHandle.Ptr);
                    if (update)
                    {
                        TrayUpdate(trayHandle.Ptr);
                    }
                }
            }
        }

        /// <summary>
        /// Does one iteration of the tray loop (e.g. handling menu item clicks).
        /// </summary>
        /// <param name="blocking">Whether to block the thread until a message arrives, or just handle existing messages in the queue.</param>
        /// <returns>Status code (where 0 is success).</returns>
        public int MessageLoopIteration(bool blocking)
        {
            return TrayLoop(blocking ? 1 : 0);
        }

        protected abstract IHeapAlloc AllocateMenuItems();

        protected abstract IHeapAlloc AllocateTray(IntPtr menuItemsPtr);

        protected abstract int TrayInit(IntPtr tray);

        protected abstract void TrayExit(IntPtr tray);

        protected abstract void TrayUpdate(IntPtr tray);

        protected abstract int TrayLoop(int blocking);

        /// <summary>
        /// Shows this icon in the notification area and reacts to user events.
        /// Keeps blocking the thread as long as this icon is shown.
        /// </summary>
        /// <remarks>Call this on the thread that owns the NotifyIcon.</remarks>
        /// <param name="cancellationToken">A cancellation token that, when fired, disposes this object and stops the loop.</param>
        public abstract void Show(CancellationToken cancellationToken = default);

        protected void DisposeWithoutExiting()
        {
            if (!disposed)
            {
                trayHandle.Dispose();
                menuItemsHandle.Dispose();
                disposed = true;
            }
        }

        public virtual void Dispose()
        {
            GC.SuppressFinalize(this);
            if (!disposed)
            {
                TrayExit(trayHandle.Ptr);
            }
            DisposeWithoutExiting();
        }
    }
}