# lsof for Windows

List open file handles, TCP/UDP connections, and loaded modules.

## Usage

```text
lsof [options]
```

## Options

The block below is copied verbatim from `Cli/CliReference.cs`
(`--help` output). That file is the source of truth; `CliReferenceTests`
fails if this block drifts.

```text
  -p, --process <name|pid>  Filter by process name (wildcards allowed) or PID
      --process=<name|pid>  Inline form; use when the value begins with '-'
                            Comma-separated and repeatable
  -c, --command <name>      Filter by process (command) name; alias of -p for names
  -u, --user <user>         Filter by owning user (name, domain\name, or SID)
      --user=<user>         Inline form; comma-separated and repeatable
  -P, --port <port>         Filter network rows by port
      --port=<port>         Inline form
                            Local for UDP; local or remote for TCP
                            Comma-separated and repeatable
  -i, --network             Show TCP/UDP connections
      -i:<port>             Also accepts a port spec: -i:22, -i :22, -i tcp:443
  -f, --files               Show open file handles
  -m, --modules             Show loaded modules (DLLs)
  -a, --all                 Show connections, files, and modules
      --unique              Remove duplicate rows
  -j, --json                Output JSON
  <path>                    Show only handles for the given file path;
                            defaults to --files when no type flag is present
  -h, --help, /?            Show this help
                            Help exits immediately; later options are ignored
```

Default (no type flags): `--network --files`, or `--files` when a `<path>` is given.

If no type option is specified, network connections and file handles are shown.
Process-name filters are case-insensitive and support `*` and `?` wildcards. Run
elevated to inspect more file handles across users.
Process filters resolve to PIDs from the initial process snapshot, and that same
selection is used by each collector. Numeric PIDs not present in that snapshot
produce no rows. Windows tables identify owners by PID only, so PID reuse during
a run remains best-effort.

Exit codes are `0` for complete collection (including no matching rows), `1` for
invalid arguments, `2` when collection warnings indicate incomplete data, and
`130` when cancelled with Ctrl+C. Long handle scans report progress to stderr.

## Examples

```text
lsof -i -P 443
lsof -i:22
lsof -f -p chrome
lsof -u DOMAIN\alice
lsof C:\Windows\notepad.exe
lsof -a -p 1234 --json
```

## Build

```powershell
dotnet build .\lsof.csproj -c Release
dotnet publish .\lsof.csproj -c Release -r win-x64
dotnet test .\Tests\Lsof.Tests.csproj
```
