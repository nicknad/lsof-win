namespace Lsof.Native;

internal interface IWindowsHandleNativeApi
{
    int QuerySystemInformation(int infoClass, IntPtr information, int informationLength, out int returnLength);

    IntPtr OpenProcess(int access, bool inherit, int processId);

    bool DuplicateHandle(
        IntPtr sourceProcess,
        IntPtr sourceHandle,
        IntPtr targetProcess,
        out IntPtr targetHandle,
        uint access,
        bool inherit,
        uint options);

    IntPtr GetCurrentProcess();

    bool CloseHandle(IntPtr handle);

    uint GetFileType(IntPtr handle);

    uint GetFinalPathNameByHandle(IntPtr handle, char[] path, uint pathLength, uint flags);
}
