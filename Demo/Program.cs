using NotificationIcon.NET;
using System.Runtime.InteropServices;

namespace Demo
{
    internal static partial class Program
    {
        static void Main()
        {
            string iconPath = AppContext.BaseDirectory;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                iconPath = Path.Join(iconPath, "icon.ico");
            }
            else
            {
                iconPath = Path.Join(iconPath, "icon.png");
            }
            NotifyIcon icon = NotifyIcon.Create(iconPath, new List<MenuItem>()
            {
                new MenuItem("Example Button"),
                new MenuItem("Example Checkbox")
                {
                    IsChecked = true,
                    Click = (s, e) =>
                    {
                        MenuItem me = (MenuItem)s!;
                        me.IsChecked = !me.IsChecked;
                    }
                },
                new MenuItem("Quit")
                {
                    Click = (s, e) =>
                    {
                        e.Icon.Dispose();
                    }
                }
            });
            icon.Show();
        }
    }
}