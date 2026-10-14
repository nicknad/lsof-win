namespace Lsof.Processes;

// Resolves the account name that owns a process, or null when it cannot be determined
// (for example, when the process is protected or has already exited).
internal interface IProcessOwnerProvider
{
    string? GetOwner(int processId);
}
