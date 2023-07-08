using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NotificationIcon.NET
{
    public struct HeapAlloc<T> : IHeapAlloc where T : struct
    {
        public IntPtr Ptr { get; }

        private readonly int arraySize;
        private readonly IEnumerable<IDisposable>? dependencies;

        private HeapAlloc(nint ptr, int arraySize, IEnumerable<IDisposable>? dependencies = null)
        {
            Ptr = ptr;
            this.arraySize = arraySize;
            this.dependencies = dependencies;
        }

        private static int GetElementSize()
        {
            unsafe
            {
                return sizeof(T);
            }
        }

        /// <summary>
        /// Creates a copy of the given struct on the heap.
        /// </summary>
        /// <returns>A safe pointer to the unmanaged memory.</returns>
        public static HeapAlloc<T> Copy(T @struct, IEnumerable<IDisposable>? dependencies = null)
        {
            int elementSize = GetElementSize();
            IntPtr result = Marshal.AllocHGlobal(elementSize);
            Marshal.StructureToPtr(@struct, result, false);
            return new HeapAlloc<T>(result, 1, dependencies);
        }

        /// <summary>
        /// Creates a copy of the given struct array on the heap.
        /// </summary>
        /// <returns>A safe pointer to the unmanaged memory.</returns>
        public static HeapAlloc<T> Copy(T[] structs, IEnumerable<IDisposable>? dependencies = null)
        {
            int elementSize = GetElementSize();
            IntPtr result = Marshal.AllocHGlobal(elementSize * structs.Length);
            IntPtr current = result;
            for (int i = 0; i < structs.Length; i++)
            {
                Marshal.StructureToPtr(structs[i], current, false);
                current += elementSize;
            }
            return new HeapAlloc<T>(result, structs.Length, dependencies);
        }

        public void Dispose()
        {
            int elementSize = GetElementSize();
            IntPtr current = Ptr;
            for (int i = 0; i < arraySize; i++)
            {
                Marshal.DestroyStructure<T>(current);
                current += elementSize;
            }
            Marshal.FreeHGlobal(Ptr);
            if (dependencies != null)
            {
                foreach (IDisposable dependency in dependencies)
                {
                    dependency.Dispose();
                }
            }
        }
    }
}