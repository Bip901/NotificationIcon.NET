using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace NotificationIcon.NET;

public record class MenuItem
{
    public class UpdateRequiredEventArgs : EventArgs
    {
        public static UpdateRequiredEventArgs Default => _default ??= new UpdateRequiredEventArgs();
        private static UpdateRequiredEventArgs? _default;

        public bool SubmenuChanged { get; init; }
    }

    internal event EventHandler<UpdateRequiredEventArgs>? UpdateRequired;

    public string Text
    {
        get => _text;

        [MemberNotNull(nameof(_text))]
        set
        {
            if (_text != value)
            {
                _text = value;
                UpdateRequired?.Invoke(this, UpdateRequiredEventArgs.Default);
            }
        }
    }
    private string _text;

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
    /// Whether the item is currently checked, or null if the item is not a checkbox.
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

    public MenuItem(string text)
    {
        Text = text;
    }

    internal virtual void OnClick(MenuItemClickEventArgs args)
    {
        Click?.Invoke(this, args);
    }
}