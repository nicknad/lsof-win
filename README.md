# lsof for Windows

List open file handles, TCP/UDP connections, and loaded modules.

## Usage

```text
lsof [options]
```

## Options

```text
  -p, --process <name|pid>  Filter by process name (wildcards allowed) or PID
                            Comma-separated and repeatable
      --process=<name|pid>  Inline form for values beginning with `-`
  -P, --port <port>         Filter network rows by port
                            Local for UDP; local or remote for TCP
                            Comma-separated and repeatable
      --port=<port>         Inline form
  -i, --network             Show TCP/UDP connections
  -f, --files               Show open file handles
  -m, --modules             Show loaded modules (DLLs)
  -a, --all                 Show connections, files, and modules
  -u, --unique              Remove duplicate rows
  -j, --json                Output JSON
  -h, --help, /?            Show this help
```

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
lsof -f -p chrome
lsof -a -p 1234 --json
```

## Build

```powershell
dotnet build .\lsof.csproj -c Release
dotnet publish .\lsof.csproj -c Release -r win-x64
dotnet test .\Tests\Lsof.Tests.csproj
```
