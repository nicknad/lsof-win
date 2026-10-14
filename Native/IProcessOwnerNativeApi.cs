namespace Lsof.Native;

// Test seam over the token/LSA functions used to resolve a process owner. Each method mirrors the
// native function it forwards to so tests can substitute fakes.
internal interface IProcessOwnerNativeApi
{
    // kernel32!OpenProcess: opens a process with the requested access rights.
    IntPtr OpenProcess(uint access, int processId);

    // advapi32!OpenProcessToken: opens the access token associated with a process.
    bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    // advapi32!GetTokenInformation: copies the requested token information into a caller buffer.
    bool GetTokenInformation(IntPtr tokenHandle, int informationClass, IntPtr information, int informationLength, out int returnLength);

    // advapi32!LookupAccountSidW: resolves a SID to its account and domain names.
    bool LookupAccountSid(IntPtr sid, char[] name, ref int nameLength, char[] domain, ref int domainLength, out int sidNameUse);

    // kernel32!CloseHandle: releases a kernel object handle.
    bool CloseHandle(IntPtr handle);
}
