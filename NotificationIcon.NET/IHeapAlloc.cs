using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotificationIcon.NET
{
    public interface IHeapAlloc : IDisposable
    {
        public IntPtr Ptr { get; }
    }
}