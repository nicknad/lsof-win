# ADR 0001: Migrate lsof to .NET 11 / C# 15

- Status: accepted for the migration; open questions listed at the end
- Date: 2026-11-11

## Context

`lsof` is a Windows-only, AOT-published CLI (`PublishAot`, `PublishSingleFile`,
warnings-as-errors, `latest-recommended` analyzers, generated XML docs,
`InvariantGlobalization`). It enumerates processes, modules, TCP/UDP endpoints and
the system handle table through a small `DllImport` layer; it never launches a
process. Tests are xunit v3 on Microsoft.Testing.Platform.

The project targeted `net10.0-windows` and did not pin an SDK (`global.json` only
selected the test runner), so the installed SDK silently decided the toolchain.

## Decisions

### D1 - Target `net11.0-windows`, single target

The tool is an application, not a library: there are no consumers that need a
`net10.0` asset. Multi-targeting would double the test matrix to protect nothing.

### D2 - Pin SDK 11.0.100 with `latestPatch`

`net11.0` gives C# 15 by default: the SDK computes the language version from the
TFM (`Microsoft.CSharp.Core.targets` maps .NET 11 to `15.0`, capped at the
compiler's `15.0`). No `<LangVersion>` is set, and none should be: `latest` would
float across machines.

### D3 - Leave package references alone

The only package reference is `xunit.v3.mtp-v2` 4.0.1, pinned exactly. AOT and
trimming packages (`Microsoft.DotNet.ILCompiler`, `Microsoft.NET.ILLink.Tasks`)
are implicit and follow the SDK band, so pinning the SDK pins them. A lock file
buys nothing for one exact-versioned package; add one only if a second package
appears.

### D4 - Adopt collection-expression arguments where a count is known

`ProcessCatalog.Create` fills one dictionary entry per process from a snapshot of
known length. `[with(capacity: processes.Length)]` binds to
`Dictionary<int, ProcessInfo>(int capacity)` and removes the resize/rehash chain
while filling. This is the only site in the codebase where a collection is
created with a known size; the handle and entry lists accumulate across
collectors, so their sizes are unknown at construction.

### D5 - Reject the other C# 15 features (no concrete payoff)

| Feature | Verdict | Reason |
| --- | --- | --- |
| Union types | reject | No case-set shape exists. `ProcessSelection` is the closest candidate, but it is a two-field class whose invariant is already enforced by a private constructor, and it is used by eight test files; a union would be churn without a bug. |
| Closed hierarchies | reject | The codebase has no class hierarchy at all; `closed` requires one to exist. |
| Extension indexers | reject | Nothing exposes indexer-shaped access; the tool prints rows, it does not model sequences. |
| Labeled `break`/`continue` | reject | There is no nested-loop-plus-flag pattern; all loops are single-level, and retry loops advance before continuing. |
| Non-virtual static interface members | reject | Only relevant when targeting runtimes without default interface methods; this project targets `net11.0` only. |
| Memory-safety preview (`unsafe(...)`, `safe`) | reject | Needs `LangVersion=preview` plus a feature flag in a shipping AOT binary, and changes the meaning of `unsafe`. The native layer is already isolated behind interfaces; no measured defect motivates the opt-in. |
| Runtime async | reject | The codebase has no `async` methods. |
| `System.Text.Json` union support | reject | No unions in the serialized model (`LsofEntry`). |
| JSON Lines output | defer | Would change the `--json` contract; a product decision, not a migration step. |

### D6 - New process-launch APIs stay out of the product

.NET 11 adds `Process.Run`, `RunAndCaptureText`, `StartAndForget`,
`TryGetProcessById`, `Signal`, `ProcessExitStatus`, `ProcessStartInfo.StartSuspended`
and `SafeProcessHandle.Start/Resume/WaitForExit`. The tool enumerates the process
table; it does not start processes, so none of these replace existing code. The
only plausible consumer is the integration suite, and no current test fails
because of a spawn race.

Probe results (file-based app, `net11.0`, compiled with the SDK 11 compiler):

```text
[RunAndCaptureText]  FAILED: UnauthorizedAccessException: \\.\pipe\... is denied   (sandbox)
[Run]                exit=3 signal=            -> Process.Run returns ProcessExitStatus
[StartAndForget]     pid=70272, TryGetProcessById -> true, WaitForExitStatus exit=1
[StartSuspended]     SafeProcessHandle.Start(info) -> pid, Resume(), WaitForExit()  -> works
```

`RunAndCaptureText` failed only because it creates an OS pipe for redirection,
which this sandbox denies; the API itself resolves and binds correctly. `Process.Run`
without redirection works and reports an exit status. `SafeProcessHandle.Start`
is the supported way to launch suspended and resume: calling `Resume` on a handle
opened by PID throws (`Resume can only be called on a process that was started with
StartSuspended set to true`).

### D7 - No memory-safety preview opt-in

See D5. If interop modernization is wanted later, `[LibraryImport]` (available
since .NET 7, unrelated to C# 15) is the smaller, non-preview step, and it should be
its own change with its own tests.

## Evidence

- Baseline (SDK 10.0.403, `net10.0-windows`): build clean, 181 tests, 180 pass,
  1 fail (`Handle_enumeration_finds_a_temporary_file_opened_by_the_current_process`).
- After migration (SDK 11.0.100, `net11.0-windows`, C# 15): identical result - 181
  tests, 180 pass, same single failure. The failure is environmental: the sandbox
  denies `NtQuerySystemInformation(SystemExtendedHandleInformation)` with
  `STATUS_ACCESS_DENIED (0xC0000022)`, so no handles are returned. It is not caused
  by the migration.
- App assembly: 108,032 bytes (net10) -> 107,008 bytes (net11).
- `--help` startup, single sample: 94 ms -> 78 ms (indicative only; not a controlled
  benchmark).
- Live check: for a spawned `powershell.exe` (PID 68144), `--modules --process=68144`
  reports name `powershell` and the expected module paths; `Get-Process -Id 68144`
  agrees on the PID.
- API surface: `System.Diagnostics.Process` metadata has 367 entries at net10.0 and
  479 at net11.0, with 3 removals (`ProcessStartInfo.UseCredentialsForNetworkingOnly`).
  No type the tool uses was removed.
- C# 15 breaking changes do not apply: the source has no identifier named `with`,
  `union`, `closed` or `safe`, no `nameof(this.X)` in attributes, no `dynamic` and
  no loop-escaped spans.

## Environment limits on this verification

The sandbox that produced this change denies named pipes and has no network, which
blocks three things that are not code problems:

1. `dotnet build` needs a pipe for the C# compiler server; the migration was
   verified by resolving references with MSBuild and compiling with the SDK's
   `csc.dll` directly (same Roslyn 5.12, C# 15, `-warnaserror+`, CA analyzers).
2. `dotnet test` needs MTP's IPC pipe; the test executable was run directly.
3. AOT publish needs `Microsoft.DotNet.ILCompiler` 11.0.0 and
   `Microsoft.NET.ILLink.Tasks` 11.0.0 from NuGet (only `11.0.0-rc.1` is cached) and
   runs ILLink through an out-of-process task host (a pipe). It is therefore
   unverified here; on a networked machine run:

   ```powershell
   dotnet publish .\lsof.csproj -c Release -r win-x64
   ```

   A cold clone needs: the .NET 11 SDK (pinned by `global.json`), nuget.org for the
   two AOT packages plus xunit, and named-pipe access for MSBuild/Roslyn.

## Decisions taken on the open questions (2026-11-11)

1. **Single target, `net11.0-windows`.** No `net10.0` asset is published; nothing
   consumes the tool as a library.
2. **No memory-safety preview opt-in.** The project keeps the default language
   version and no `Features` switch, so `unsafe` keeps its shipped meaning.
3. **No package lock file.** One exact-versioned package; the SDK pin covers the
   implicit AOT/trimming packages. Revisit when a second package is added.
4. **No process-launch feature.** The tool enumerates; it does not spawn. The probe
   results stay in D6 as the record of why.
5. **`--json` stays a single JSON array.** JSON Lines would be a separate output
   format with its own flag and compatibility story.
6. **Hardware requirement is documented for release.** See the Requirements section
   in `README.md`.

## Follow-up not taken here

`[LibraryImport]` source-generated marshalling (available since .NET 7, unrelated to
C# 15) would remove runtime marshalling from the `DllImport` layer and is the natural
next interop change, but it is a behavior-adjacent rewrite of the native boundary and
does not belong in a TFM migration. Treat it as its own change with its own tests.

## Verification still owed outside this sandbox

AOT publish (`dotnet publish -c Release -r win-x64`) has not been executed here for
the reasons in the previous section. Run it on a networked machine before tagging a
release; a cold clone needs the pinned SDK, nuget.org, and named-pipe access.

