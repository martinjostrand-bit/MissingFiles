# MissingFiles

Windows tool that finds files in a **source** folder that are missing from a **destination** folder, and can copy them over.

- Files are matched by **name + size**, anywhere in the destination (folder location does not matter).
- Which file types to compare is defined in a JSON file types file (default: pictures and videos).
- Missing files are written to a JSON result file; a separate copy step copies them to the destination, never overwriting anything.
- WPF desktop app and a command line interface with the same functions.

See [MissingFiles_specification_v2.md](MissingFiles_specification_v2.md) for the full specification.

> Status: early development. Done: WP0 (project skeleton), WP1 (file types, scan, scan result file), WP2 (copy, copy log). Next: WP3 (CLI).

## Requirements

- Windows 10 (22H2) or Windows 11, x64
- To build: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Build and test

```powershell
dotnet build
dotnet test
dotnet format --verify-no-changes
```

Build single-file executables:

```powershell
dotnet publish src/MissingFiles.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
dotnet publish src/MissingFiles.Cli -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish
```

## Project structure

| Project | Purpose |
|---|---|
| `src/MissingFiles.Core` | All logic: scanning, matching, copying, JSON, logging. No UI code. |
| `src/MissingFiles.Cli` | Command line interface (`MissingFiles.Cli.exe`). |
| `src/MissingFiles.App` | WPF desktop app (`MissingFiles.exe`). |
| `tests/MissingFiles.Core.Tests` | Unit tests. |
| `tests/MissingFiles.FunctionalTests` | Functional tests (spec §5). |

## Windows SmartScreen warning

The executables are not code-signed. On first start Windows may show *"Windows protected your PC"*. Click **More info → Run anyway**.
