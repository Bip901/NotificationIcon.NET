using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace NotificationIcon.NET;

/// <summary>
/// Represents a non-garbage collected, global heap allocation.
/// </summary>
/// <typeparam name="T">The struct contained in this allocation.</typeparam>
public struct HeapAlloc<T> : IHeapAlloc where T : struct
{
    /// <summary>
    /// A pointer to the beginning of the allocation.
    /// </summary>
    /// <exception cref="ObjectDisposedException"/>
    public readonly IntPtr Ptr
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return ptr;
        }
    }

    private readonly IntPtr ptr;
    private readonly int arraySize;
    private readonly IEnumerable<IDisposable>? dependencies;
    private bool disposed;

    private HeapAlloc(nint ptr, int arraySize, IEnumerable<IDisposable>? dependencies = null)
    {
        this.ptr = ptr;
        this.arraySize = arraySize;
        this.dependencies = dependencies;
    }

    private static int GetElementSize()
    {
        unsafe
        {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
            return sizeof(T);
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
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

    /// <summary>
    /// Frees this allocation.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
            return;
        int elementSize = GetElementSize();
        IntPtr current = ptr;
        for (int i = 0; i < arraySize; i++)
        {
            Marshal.DestroyStructure<T>(current);
            current += elementSize;
        }
        Marshal.FreeHGlobal(ptr);
        disposed = true;
        if (dependencies != null)
        {
            foreach (IDisposable dependency in dependencies)
            {
                dependency.Dispose();
            }
        }
    }
}