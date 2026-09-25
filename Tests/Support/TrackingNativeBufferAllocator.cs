using System.Runtime.InteropServices;
using Lsof.Native;

namespace Lsof.Tests.Support;

internal sealed class TrackingNativeBufferAllocator : INativeMemoryAllocator, IDisposable
{
    private readonly HashSet<IntPtr> _outstandingBuffers = new();

    public List<int> AllocationSizes { get; } = new();

    public int FreedCount { get; private set; }

    public int OutstandingCount => _outstandingBuffers.Count;

    public IntPtr Allocate(int byteCount)
    {
        IntPtr buffer = Marshal.AllocHGlobal(byteCount);
        _outstandingBuffers.Add(buffer);
        AllocationSizes.Add(byteCount);
        return buffer;
    }

    public void Free(IntPtr buffer)
    {
        if (!_outstandingBuffers.Remove(buffer))
        {
            throw new InvalidOperationException("A native buffer was freed more than once or was not allocated by this allocator.");
        }

        Marshal.FreeHGlobal(buffer);
        FreedCount++;
    }

    public void Dispose()
    {
        foreach (IntPtr buffer in _outstandingBuffers)
        {
            Marshal.FreeHGlobal(buffer);
        }

        _outstandingBuffers.Clear();
    }
}
