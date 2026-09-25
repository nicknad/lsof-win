namespace Lsof.Native;

internal static class NativeBufferSizing
{
    internal static int GetNextSize(int currentSize, int requiredSize, int maximumSize, int growth)
    {
        if (requiredSize > maximumSize)
        {
            return 0;
        }

        long nextSize = requiredSize > currentSize
            ? (long)requiredSize + growth
            : (long)currentSize * 2;
        nextSize = Math.Min(nextSize, maximumSize);
        return nextSize > currentSize ? (int)nextSize : 0;
    }
}
