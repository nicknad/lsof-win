using Lsof.Native;

namespace Lsof.Tests.Support;

internal delegate int FakeSystemInformationHandler(int infoClass, IntPtr information, int informationLength, out int returnLength);

internal sealed record SystemInformationCall(int InfoClass, IntPtr Buffer, int BufferSize);

internal sealed class FakeWindowsHandleNativeApi : IWindowsHandleNativeApi
{
    public FakeSystemInformationHandler? SystemInformationHandler { get; set; }

    public List<SystemInformationCall> SystemInformationCalls { get; } = new();

    public List<int> OpenedProcessIds { get; } = new();

    public List<IntPtr> ClosedHandles { get; } = new();

    public IntPtr ProcessHandle { get; set; } = new(100);

    public IntPtr CurrentProcessHandle { get; set; } = new(200);

    public IntPtr DuplicatedHandle { get; set; } = new(300);

    public IntPtr LastDuplicatedSourceHandle { get; private set; }

    public uint FileType { get; set; } = 1;

    public bool OpenProcessSucceeds { get; set; } = true;

    public bool DuplicateHandleSucceeds { get; set; } = true;

    public bool CloseHandleSucceeds { get; set; } = true;

    public string? FinalPath { get; set; } = @"\\?\C:\fixture.txt";

    public int FinalPathCallCount { get; private set; }

    public int QuerySystemInformation(int infoClass, IntPtr information, int informationLength, out int returnLength)
    {
        SystemInformationCalls.Add(new SystemInformationCall(infoClass, information, informationLength));
        if (SystemInformationHandler is null)
        {
            returnLength = 0;
            return unchecked((int)0xC0000004);
        }

        return SystemInformationHandler(infoClass, information, informationLength, out returnLength);
    }

    public IntPtr OpenProcess(int access, bool inherit, int processId)
    {
        OpenedProcessIds.Add(processId);
        return OpenProcessSucceeds ? ProcessHandle : IntPtr.Zero;
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
        LastDuplicatedSourceHandle = sourceHandle;
        targetHandle = DuplicateHandleSucceeds ? DuplicatedHandle : IntPtr.Zero;
        return DuplicateHandleSucceeds;
    }

    public IntPtr GetCurrentProcess()
    {
        return CurrentProcessHandle;
    }

    public bool CloseHandle(IntPtr handle)
    {
        ClosedHandles.Add(handle);
        return CloseHandleSucceeds;
    }

    public uint GetFileType(IntPtr handle)
    {
        return FileType;
    }

    public uint GetFinalPathNameByHandle(IntPtr handle, char[] path, uint pathLength, uint flags)
    {
        FinalPathCallCount++;
        if (FinalPath is null)
        {
            return 0;
        }

        if (FinalPath.Length >= pathLength)
        {
            return (uint)FinalPath.Length;
        }

        FinalPath.CopyTo(0, path, 0, FinalPath.Length);
        return (uint)FinalPath.Length;
    }
}
