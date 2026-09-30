# MissingFiles

Windows tool that finds files in a **source** folder that are missing from a **destination** folder, and can copy them over.

- Files are matched by **name + size**, anywhere in the destination (folder location does not matter).
- Which file types to compare is defined in a JSON file types file (default: pictures and videos).
- Missing files are written to a JSON result file; a separate copy step copies them to the destination, never overwriting anything.
- WPF desktop app and a command line interface with the same functions.

See [MissingFiles_specification_v2.md](MissingFiles_specification_v2.md) for the full specification.

> Status: early development. Done: WP0 (project skeleton), WP1 (file types, scan, scan result file), WP2 (copy, copy log), WP3 (command line), WP4 (desktop app: scan and result view). Next: WP5 (desktop app: copy).

## Desktop app

Start `MissingFiles.exe`, choose the source and destination folders, tick the file type groups to compare,
and press **Start scan**. The result shows the counters, the path of the result file, and the list of missing
files, which can be sorted and filtered. **Open result file...** shows the result of an earlier scan.
The folders and the file types file are remembered for the next start.

File types come from `%APPDATA%\MissingFiles\FileTypes.json`, created from the built-in default on first start.
Use **Edit** to open it in a text editor and **Reload** after saving it.

## Command line

```powershell
# 1. Find files in D:\CameraBackup that are missing from E:\PhotoArchive
MissingFiles.Cli scan --source D:\CameraBackup --dest E:\PhotoArchive

# 2. Check what would be copied, then copy
MissingFiles.Cli copy --result "$env:USERPROFILE\Documents\MissingFiles\MissingFiles_20260927_140312.json" --dry-run
MissingFiles.Cli copy --result "$env:USERPROFILE\Documents\MissingFiles\MissingFiles_20260927_140312.json"
```

Result files and copy logs are written to `Documents\MissingFiles` (change with `--out`).
File types come from `%APPDATA%\MissingFiles\FileTypes.json` if it exists, otherwise the built-in
pictures + videos list; use `--types-file` and `--groups` to choose others.
Run `MissingFiles.Cli scan --help` or `MissingFiles.Cli copy --help` for all options.
Press Ctrl+C to cancel.

| Exit code | Meaning |
|---|---|
| 0 | Nothing missing / all files copied |
| 1 | Missing files found / some files skipped |
| 2 | Some folders could not be read / some files could not be copied |
| 3 | Invalid arguments, or input that prevents starting (folders, files, free space) |
| 4 | Cancelled |
| 5 | Fatal error |

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
| `tests/MissingFiles.Core.Tests` | Unit tests of the core library. |
| `tests/MissingFiles.App.Tests` | Unit tests of the desktop app view models. |
| `tests/MissingFiles.FunctionalTests` | Functional tests (spec §5). |

## Windows SmartScreen warning

The executables are not code-signed. On first start Windows may show *"Windows protected your PC"*. Click **More info → Run anyway**.
