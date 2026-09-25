using System.Runtime.InteropServices;

namespace Lsof.Native;

// Thin P/Invoke layer over the native functions used to enumerate the system handle table.
// Each method forwards one Windows/NT function so tests can substitute IWindowsHandleNativeApi.
internal sealed class WindowsHandleNativeApi : IWindowsHandleNativeApi
{
    // ntdll!NtQuerySystemInformation: queries kernel-wide system information by class.
    // Class 64 (SystemExtendedHandleInformation) returns every open handle in the system.
    // Returns an NTSTATUS: 0 is success, 0xC0000004 means the buffer was too small.
    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(int infoClass, IntPtr information, int informationLength, out int returnLength);

    // kernel32!OpenProcess: opens a running process and returns a handle with the requested access.
    // PROCESS_DUP_HANDLE is requested so handles can be duplicated out of the process for inspection.
    // Returns IntPtr.Zero on failure and sets the last Win32 error.
    [DllImport("kernel32.dll", EntryPoint = "OpenProcess", SetLastError = true)]
    private static extern IntPtr OpenProcessNative(int access, bool inherit, int processId);

    // kernel32!DuplicateHandle: copies a handle from a source process into a target process.
    // Used to copy another process's handle into this process so the referenced object can be queried.
    // Returns false on failure; DUPLICATE_SAME_ACCESS preserves the original access rights.
    [DllImport("kernel32.dll", EntryPoint = "DuplicateHandle", SetLastError = true)]
    private static extern bool DuplicateHandleNative(
        IntPtr sourceProcess,
        IntPtr sourceHandle,
        IntPtr targetProcess,
        out IntPtr targetHandle,
        uint access,
        bool inherit,
        uint options);

    // kernel32!GetCurrentProcess: returns the pseudo-handle (-1) for the calling process.
    // The pseudo-handle is always valid and must not be closed.
    [DllImport("kernel32.dll", EntryPoint = "GetCurrentProcess")]
    private static extern IntPtr GetCurrentProcessNative();

    // kernel32!CloseHandle: releases a kernel object handle (process or file). Returns false on failure.
    [DllImport("kernel32.dll", EntryPoint = "CloseHandle", SetLastError = true)]
    private static extern bool CloseHandleNative(IntPtr handle);

    // kernel32!GetFileType: returns the object type behind a handle (disk, char, pipe, or unknown).
    // FILE_TYPE_DISK (1) identifies regular on-disk files.
    [DllImport("kernel32.dll", EntryPoint = "GetFileType")]
    private static extern uint GetFileTypeNative(IntPtr handle);

    // kernel32!GetFinalPathNameByHandle: writes the normalized final path of a file handle into the
    // caller buffer and returns the required character count excluding the null terminator.
    // flags = 0 requests a DOS path (with a \\?\ or \\?\UNC\ prefix). Returns 0 on failure.
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
