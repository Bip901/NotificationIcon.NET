using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace NotificationIcon.NET;

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

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void MenuItemCallback(IntPtr menu);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void TrayCallback(IntPtr tray);

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
    /// The tooltip text shown when hovering over the tray icon. Null means no tooltip.
    /// On Linux this property is silently ignored (AppIndicator does not support tooltips).
    /// </summary>
    public string? Tooltip
    {
        get => _tooltip;
        set
        {
            if (!string.Equals(_tooltip, value))
            {
                _tooltip = value;
                AllocateNewTray(true);
            }
        }
    }
    private string? _tooltip;

    /// <summary>
    /// Returns true if there are any subscribers to the <see cref="DoubleClick"/> event.
    /// </summary>
    protected bool HasDoubleClickSubscribers => DoubleClick != null;

    /// <summary>
    /// Raised when the user double-clicks the tray icon.
    /// On Linux this event is never raised (AppIndicator does not support click events).
    /// </summary>
    public event EventHandler<TrayIconClickEventArgs>? DoubleClick;

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

    private readonly List<MenuItemCallback> _callbacks;
    private TrayCallback? _doubleClickCallback;
    private IHeapAlloc trayHandle;
    private IHeapAlloc menuItemsHandle;
    private Exception? trayLoopException;
    private bool disposed;

    /// <summary>
    /// Creates a new <see cref="NotifyIcon"/>.
    /// </summary>
    /// <param name="iconPath">A path to an icon on the file system. For Windows, this should be an ICO file. For Unix, this should be a PNG.</param>
    /// <param name="menuItems">The menu items to display to the user when clicking the icon.</param>
    /// <param name="tooltip">Optional tooltip shown when hovering over the icon. Ignored on Linux.</param>
    /// <param name="onDoubleClick">Optional action invoked on double-click. Ignored on Linux.</param>
    /// <exception cref="PlatformNotSupportedException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public static NotifyIcon Create(
        string iconPath,
        IReadOnlyList<MenuItem> menuItems,
        string? tooltip = null,
        Action<NotifyIcon>? onDoubleClick = null)
    {
        NotifyIcon icon;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            icon = new WindowsNotifyIcon(iconPath, menuItems);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            icon = new LinuxNotifyIcon(iconPath, menuItems);
        else
            throw new PlatformNotSupportedException();

        icon.Tooltip = tooltip;
        if (onDoubleClick != null)
            icon.DoubleClick += (s, e) => onDoubleClick(icon);
        return icon;
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
            string baseDirectory = AppContext.BaseDirectory;
            string rid = GetNonVersionSpecificRID();
            //If the project using this library is built for a specific RID (e.g. when publishing with NativeAOT),
            //the native library is directly next to the executable.
            string path = Path.Join(baseDirectory, nativeLibraryName);
            if (!File.Exists(path))
            {
                // e.g. ./runtimes/win-x64/native/notification_icon.dll
                path = Path.Join(baseDirectory, "runtimes", rid, "native", nativeLibraryName);
            }
            try
            {
                NativeLibrary.Load(path);
            }
            catch (DllNotFoundException ex)
            {
                throw new PlatformNotSupportedException($"Native support not found for platform \"{rid}\".", ex);
            }
            loadedNativeLibrary = true;
        }
        _callbacks = new();
        _iconPath = iconPath;
        _menuItems = menuItems;
        AllocateNewTray(false);
        int tray_init_result = TrayInit(trayHandle.Ptr);
        if (tray_init_result < 0)
        {
            throw new InvalidOperationException($"Failed to initialize tray. ({tray_init_result})");
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
        int result = TrayLoop(blocking ? 1 : 0);
        if (trayLoopException != null)
        {
            Exception ex = trayLoopException;
            trayLoopException = null;
            throw ex;
        }
        return result;
    }

    /// <summary>
    /// Allocates and sets up the menu items of this <see cref="NotifyIcon"/>.
    /// </summary>
    /// <returns>The allocation.</returns>
    protected abstract IHeapAlloc AllocateMenuItems();

    /// <summary>
    /// Allocates the native structure representing this <see cref="NotifyIcon"/>.
    /// </summary>
    /// <param name="menuItemsPtr"></param>
    /// <returns></returns>
    protected abstract IHeapAlloc AllocateTray(IntPtr menuItemsPtr);

    /// <summary>
    /// Initializes the tray for the first time.
    /// </summary>
    /// <param name="tray">The tray, allocated by <see cref="AllocateTray(nint)"/>.</param>
    /// <returns>A negative result if failed, zero or positive otherwise.</returns>
    protected abstract int TrayInit(IntPtr tray);

    /// <summary>
    /// Destructs the tray.
    /// </summary>
    /// <param name="tray">The tray, allocated by <see cref="AllocateTray(nint)"/>.</param>
    protected abstract void TrayExit(IntPtr tray);

    /// <summary>
    /// Called when this <see cref="NotifyIcon"/> has changed, e.g. the menu items were updated.
    /// </summary>
    /// <param name="tray">The tray, allocated by <see cref="AllocateTray(nint)"/>.</param>
    protected abstract void TrayUpdate(IntPtr tray);

    /// <summary>
    /// Does one iteration of the tray loop (e.g. handling menu item clicks).
    /// </summary>
    /// <param name="blocking">Whether to block the thread until a message arrives, or just handle existing messages in the queue.</param>
    /// <returns>Status code (where 0 is success).</returns>
    protected abstract int TrayLoop(int blocking);

    /// <summary>
    /// Creates a new OnClick menu item delegate that will not be garbaged collected until this object is garbage collected or disposed.
    /// </summary>
    /// <param name="menuItem">The menu item that triggers the click event.</param>
    /// <returns>The native pointer to the function.</returns>
    protected nint CreateNativeClickCallback(MenuItem menuItem)
    {
        MenuItemCallback callback = menuPtr =>
        {
            try
            {
                menuItem.OnClick(new MenuItemClickEventArgs(this));
            }
            catch (Exception ex)
            {
                trayLoopException = ex;
            }
        };
        _callbacks.Add(callback); //Prevent callback from being garbage-collected
        return Marshal.GetFunctionPointerForDelegate(callback);
    }

    /// <summary>
    /// Creates (or returns cached) a native callback for double-click events.
    /// </summary>
    protected nint GetOrCreateNativeDoubleClickCallback()
    {
        if (_doubleClickCallback == null)
        {
            _doubleClickCallback = trayPtr =>
            {
                try
                {
                    DoubleClick?.Invoke(this, new TrayIconClickEventArgs(this));
                }
                catch (Exception ex)
                {
                    trayLoopException = ex;
                }
            };
        }
        return Marshal.GetFunctionPointerForDelegate(_doubleClickCallback);
    }

    /// <summary>
    /// Shows this icon in the notification area and reacts to user events.
    /// Keeps blocking the thread as long as this icon is shown.
    /// </summary>
    /// <remarks>Call this on the thread that owns the NotifyIcon.</remarks>
    /// <param name="cancellationToken">A cancellation token that, when fired, disposes this object and stops the loop.</param>
    /// <exception cref="ObjectDisposedException"/>
    public virtual void Show(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        using (cancellationToken.Register(Dispose))
        {
            while (MessageLoopIteration(true) == 0)
            { }
        }
    }

    /// <summary>
    /// Stops showing this notification icon and releases all resources associated with it.
    /// </summary>
    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
        if (!disposed)
        {
            TrayExit(trayHandle.Ptr);
            trayHandle.Dispose();
            menuItemsHandle.Dispose();
            _callbacks.Clear();
            _doubleClickCallback = null;
            disposed = true;
        }
    }
}