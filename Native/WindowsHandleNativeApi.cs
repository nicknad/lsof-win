using System.Runtime.InteropServices;

namespace Lsof.Native;

internal sealed class WindowsHandleNativeApi : IWindowsHandleNativeApi
{
    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(int infoClass, IntPtr information, int informationLength, out int returnLength);

    [DllImport("kernel32.dll", EntryPoint = "OpenProcess", SetLastError = true)]
    private static extern IntPtr OpenProcessNative(int access, bool inherit, int processId);

    [DllImport("kernel32.dll", EntryPoint = "DuplicateHandle", SetLastError = true)]
    private static extern bool DuplicateHandleNative(
        IntPtr sourceProcess,
        IntPtr sourceHandle,
        IntPtr targetProcess,
        out IntPtr targetHandle,
        uint access,
        bool inherit,
        uint options);

    [DllImport("kernel32.dll", EntryPoint = "GetCurrentProcess")]
    private static extern IntPtr GetCurrentProcessNative();

    [DllImport("kernel32.dll", EntryPoint = "CloseHandle", SetLastError = true)]
    private static extern bool CloseHandleNative(IntPtr handle);

    [DllImport("kernel32.dll", EntryPoint = "GetFileType")]
    private static extern uint GetFileTypeNative(IntPtr handle);

    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandle", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetFinalPathNameByHandleNative(
        IntPtr handle,
        [Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U2, SizeParamIndex = 2)] char[] path,
        uint pathLength,
        uint flags);

    public int QuerySystemInformation(int infoClass, IntPtr information, int informationLength, out int returnLength)
    {
        return NtQuerySystemInformation(infoClass, information, informationLength, out returnLength);
    }

    public IntPtr OpenProcess(int access, bool inherit, int processId)
    {
        return OpenProcessNative(access, inherit, processId);
    }

    public bool DuplicateHandle(
        IntPtr sourceProcess,
        IntPtr sourceHandle,
        IntPtr targetProcess,
        out IntPtr targetHandle,
        uint access,
        bool inherit,
        uint options)
    {
        return DuplicateHandleNative(sourceProcess, sourceHandle, targetProcess, out targetHandle, access, inherit, options);
    }

    public IntPtr GetCurrentProcess()
    {
        return GetCurrentProcessNative();
    }

    public bool CloseHandle(IntPtr handle)
    {
        return CloseHandleNative(handle);
    }

    public uint GetFileType(IntPtr handle)
    {
        return GetFileTypeNative(handle);
    }

    public uint GetFinalPathNameByHandle(IntPtr handle, char[] path, uint pathLength, uint flags)
    {
        return GetFinalPathNameByHandleNative(handle, path, pathLength, flags);
    }
}
