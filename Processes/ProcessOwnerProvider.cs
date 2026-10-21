using System.Runtime.InteropServices;
using Lsof.Native;

namespace Lsof.Processes;

// Resolves a process owner by opening the process token and translating its user SID into a
// domain-qualified account name. This needs only PROCESS_QUERY_LIMITED_INFORMATION and TOKEN_QUERY,
// which the current user normally holds for its own processes; protected processes resolve to null.
internal sealed class ProcessOwnerProvider : IProcessOwnerProvider
{
    private const int TokenUser = 1; // TOKEN_INFORMATION_CLASS value for the token's user SID.
    private const int AccountNameBufferLength = 512; // Account names are far shorter than this cap.

    private static readonly IProcessOwnerNativeApi DefaultApi = new ProcessOwnerNativeApi();

    private readonly IProcessOwnerNativeApi _nativeApi;

    public ProcessOwnerProvider()
        : this(DefaultApi) { }

    internal ProcessOwnerProvider(IProcessOwnerNativeApi nativeApi)
    {
        _nativeApi = nativeApi;
    }

    public string? GetOwner(int processId)
    {
        IntPtr processHandle = _nativeApi.OpenProcess(
            ProcessOwnerNativeApi.ProcessQueryLimitedInformation,
            processId
        );
        if (processHandle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            if (
                !_nativeApi.OpenProcessToken(
                    processHandle,
                    ProcessOwnerNativeApi.TokenQuery,
                    out IntPtr tokenHandle
                )
            )
            {
                return null;
            }

            try
            {
                return ReadOwner(tokenHandle);
            }
            finally
            {
                _nativeApi.CloseHandle(tokenHandle);
            }
        }
        finally
        {
            _nativeApi.CloseHandle(processHandle);
        }
    }

    private string? ReadOwner(IntPtr tokenHandle)
    {
        // First call with a null buffer reports the required token-user size.
        _ = _nativeApi.GetTokenInformation(
            tokenHandle,
            TokenUser,
            IntPtr.Zero,
            0,
            out int requiredLength
        );
        if (requiredLength <= 0)
        {
            return null;
        }

        IntPtr buffer = Marshal.AllocHGlobal(requiredLength);
        try
        {
            if (
                !_nativeApi.GetTokenInformation(
                    tokenHandle,
                    TokenUser,
                    buffer,
                    requiredLength,
                    out _
                )
            )
            {
                return null;
            }

            // TOKEN_USER starts with a SID_AND_ATTRIBUTES whose first field is the PSID.
            IntPtr sid = Marshal.ReadIntPtr(buffer);
            return sid == IntPtr.Zero ? null : LookupAccount(sid);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private string? LookupAccount(IntPtr sid)
    {
        char[] name = new char[AccountNameBufferLength];
        char[] domain = new char[AccountNameBufferLength];
        int nameLength = name.Length;
        int domainLength = domain.Length;
        if (
            !_nativeApi.LookupAccountSid(sid, name, ref nameLength, domain, ref domainLength, out _)
            || nameLength <= 0
        )
        {
            return null;
        }

        string account = new(name, 0, nameLength);
        return domainLength <= 0
            ? account
            : string.Concat(new string(domain, 0, domainLength), "\\", account);
    }
}
