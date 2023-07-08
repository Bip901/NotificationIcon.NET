using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotificationIcon.NET
{
    public class MenuItemClickEventArgs : EventArgs
    {
        public NotifyIcon Icon { get; }

        public MenuItemClickEventArgs(NotifyIcon parent)
        {
            Icon = parent;
        }
    }
}