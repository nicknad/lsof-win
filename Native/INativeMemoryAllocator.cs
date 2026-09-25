using System.Runtime.InteropServices;

namespace Lsof.Native;

internal interface INativeMemoryAllocator
{
    IntPtr Allocate(int byteCount);

    void Free(IntPtr buffer);
}

// Production allocator built on the Marshal unmanaged memory API; a seam for tests to track leaks.
internal sealed class HGlobalMemoryAllocator : INativeMemoryAllocator
{
    public static readonly HGlobalMemoryAllocator Instance = new();

    private HGlobalMemoryAllocator()
    {
    }

    public IntPtr Allocate(int byteCount)
    {
        // Marshal.AllocHGlobal reserves unmanaged memory that native APIs can write into.
        return Marshal.AllocHGlobal(byteCount);
    }

    public void Free(IntPtr buffer)
    {
        // Marshal.FreeHGlobal releases memory returned by Marshal.AllocHGlobal.
        Marshal.FreeHGlobal(buffer);
    }
}
