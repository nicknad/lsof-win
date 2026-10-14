namespace Lsof.Native;

internal interface INativeMemoryAllocator
{
    IntPtr Allocate(int byteCount);

    void Free(IntPtr buffer);
}
