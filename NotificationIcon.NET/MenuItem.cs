using System;
using System.Collections.Generic;

namespace NotificationIcon.NET;

public record class MenuItem(string Text)
{
    public class UpdateRequiredEventArgs : EventArgs
    {
        public static UpdateRequiredEventArgs Default => _default ??= new UpdateRequiredEventArgs();
        private static UpdateRequiredEventArgs? _default;

        public bool SubmenuChanged { get; init; }
    }

    internal event EventHandler<UpdateRequiredEventArgs>? UpdateRequired;

    public bool IsDisabled
    {
        get => _isDisabled;
        set
        {
            if (_isDisabled != value)
            {
                _isDisabled = value;
                UpdateRequired?.Invoke(this, UpdateRequiredEventArgs.Default);
            }
        }
    }
    private bool _isDisabled;

    /// <summary>
    /// Whether the item is currently checked, or null if the item is uncheckable.
    /// </summary>
    public bool? IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked != value)
            {
                _isChecked = value;
                UpdateRequired?.Invoke(this, UpdateRequiredEventArgs.Default);
            }
        }
    }
    private bool? _isChecked;

    public EventHandler<MenuItemClickEventArgs>? Click { get; init; }

    public IReadOnlyList<MenuItem>? SubMenu
    {
        get => _subMenu;
        set
        {
            _subMenu = value;
            UpdateRequired?.Invoke(this, new UpdateRequiredEventArgs() { SubmenuChanged = true });
        }
    }
    private IReadOnlyList<MenuItem>? _subMenu;

    internal virtual void OnClick(MenuItemClickEventArgs args)
    {
        Click?.Invoke(this, args);
    }
}