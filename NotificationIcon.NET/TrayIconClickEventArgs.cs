using System;

namespace NotificationIcon.NET
{
    /// <summary>
    /// Event arguments for tray icon click events (e.g. double-click).
    /// </summary>
    public class TrayIconClickEventArgs : EventArgs
    {
        /// <summary>
        /// The <see cref="NotifyIcon"/> that was clicked.
        /// </summary>
        public NotifyIcon Icon { get; }

        public TrayIconClickEventArgs(NotifyIcon icon)
        {
            Icon = icon;
        }
    }
}