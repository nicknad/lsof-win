namespace Lsof.Native;

// Test seam over WindowsHandleNativeApi; each method mirrors the native function it forwards to.
internal interface IWindowsHandleNativeApi
{
    // ntdll!NtQuerySystemInformation: returns the requested system information class.
    int QuerySystemInformation(int infoClass, IntPtr information, int informationLength, out int returnLength);

    // kernel32!OpenProcess: opens a process and returns a handle with the requested access rights.
    IntPtr OpenProcess(int access, bool inherit, int processId);

    // kernel32!DuplicateHandle: copies a handle from one process into another.
    bool DuplicateHandle(
        IntPtr sourceProcess,
        IntPtr sourceHandle,
        IntPtr targetProcess,
        out IntPtr targetHandle,
        uint access,
        bool inherit,
        uint options);

    // kernel32!GetCurrentProcess: returns the calling process's pseudo-handle.
    IntPtr GetCurrentProcess();

    // kernel32!CloseHandle: releases a kernel object handle.
    bool CloseHandle(IntPtr handle);

    // kernel32!GetFileType: returns the object type behind a handle.
    uint GetFileType(IntPtr handle);

    // kernel32!GetFinalPathNameByHandle: resolves a file handle to its normalized path.
    uint GetFinalPathNameByHandle(IntPtr handle, char[] path, uint pathLength, uint flags);
}
