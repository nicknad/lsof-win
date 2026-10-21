using System.Runtime.InteropServices;

namespace Lsof.Native;

// Thin P/Invoke layer over the functions used to determine which user owns a process.
// Each method forwards one Windows function so tests can substitute IProcessOwnerNativeApi.
internal sealed class ProcessOwnerNativeApi : IProcessOwnerNativeApi
{
    public const uint ProcessQueryLimitedInformation = 0x1000; // PROCESS_QUERY_LIMITED_INFORMATION access right.
    public const uint TokenQuery = 0x0008; // TOKEN_QUERY access right.

    // kernel32!OpenProcess: opens a running process and returns a handle with the requested access.
    // PROCESS_QUERY_LIMITED_INFORMATION is the least-privilege right needed to read the token.
    // Returns IntPtr.Zero on failure and sets the last Win32 error.
    [DllImport("kernel32.dll", EntryPoint = "OpenProcess", SetLastError = true)]
    private static extern IntPtr OpenProcessNative(uint access, bool inherit, int processId);

    // advapi32!OpenProcessToken: opens the access token associated with a process.
    // Returns false on failure and sets the last Win32 error.
    [DllImport("advapi32.dll", EntryPoint = "OpenProcessToken", SetLastError = true)]
    private static extern bool OpenProcessTokenNative(
        IntPtr processHandle,
        uint desiredAccess,
        out IntPtr tokenHandle
    );

    // advapi32!GetTokenInformation: copies the requested TOKEN_INFORMATION_CLASS into the buffer.
    // When the buffer is null/zero-length the call fails with ERROR_INSUFFICIENT_BUFFER and
    // returnLength receives the required size.
    [DllImport("advapi32.dll", EntryPoint = "GetTokenInformation", SetLastError = true)]
    private static extern bool GetTokenInformationNative(
        IntPtr tokenHandle,
        int tokenInformationClass,
        IntPtr tokenInformation,
        int tokenInformationLength,
        out int returnLength
    );

    // advapi32!LookupAccountSidW: resolves a SID to its account name and the domain that owns it.
    // The two buffers receive UTF-16 text; the length parameters are their capacities on input and
    // the written character counts (excluding the terminator) on output.
    [DllImport(
        "advapi32.dll",
        EntryPoint = "LookupAccountSidW",
        SetLastError = true,
        CharSet = CharSet.Unicode
    )]
    private static extern bool LookupAccountSidNative(
        string? systemName,
        IntPtr sid,
        [Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U2)] char[] name,
        ref int nameLength,
        [Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U2)] char[] domain,
        ref int domainLength,
        out int sidNameUse
    );

    // kernel32!CloseHandle: releases a kernel object handle. Returns false on failure.
    [DllImport("kernel32.dll", EntryPoint = "CloseHandle", SetLastError = true)]
    private static extern bool CloseHandleNative(IntPtr handle);

    public IntPtr OpenProcess(uint access, int processId) =>
        OpenProcessNative(access, false, processId);

    public bool OpenProcessToken(
        IntPtr processHandle,
        uint desiredAccess,
        out IntPtr tokenHandle
    ) => OpenProcessTokenNative(processHandle, desiredAccess, out tokenHandle);

    public bool GetTokenInformation(
        IntPtr tokenHandle,
        int informationClass,
        IntPtr information,
        int informationLength,
        out int returnLength
    ) =>
        GetTokenInformationNative(
            tokenHandle,
            informationClass,
            information,
            informationLength,
            out returnLength
        );

    public bool LookupAccountSid(
        IntPtr sid,
        char[] name,
        ref int nameLength,
        char[] domain,
        ref int domainLength,
        out int sidNameUse
    ) =>
        LookupAccountSidNative(
            null,
            sid,
            name,
            ref nameLength,
            domain,
            ref domainLength,
            out sidNameUse
        );

    public bool CloseHandle(IntPtr handle) => CloseHandleNative(handle);
}
