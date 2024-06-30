using System;

namespace NotificationIcon.NET;

/// <summary>
/// Represents a non-garbage collected, global heap allocation.
/// </summary>
public interface IHeapAlloc : IDisposable
{
    /// <summary>
    /// A pointer to the beginning of the allocation.
    /// </summary>
    public IntPtr Ptr { get; }
}