using System.Runtime.InteropServices;

namespace Lsof.Native;

internal interface INativeMemoryAllocator
{
    IntPtr Allocate(int byteCount);

    void Free(IntPtr buffer);
}

internal sealed class HGlobalMemoryAllocator : INativeMemoryAllocator
{
    public static readonly HGlobalMemoryAllocator Instance = new();

    private HGlobalMemoryAllocator()
    {
    }

    public IntPtr Allocate(int byteCount)
    {
        return Marshal.AllocHGlobal(byteCount);
    }

    public void Free(IntPtr buffer)
    {
        Marshal.FreeHGlobal(buffer);
    }
}
