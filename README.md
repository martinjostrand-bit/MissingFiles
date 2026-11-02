# MissingFiles

Windows tool that finds files in a **source** folder that are missing from a **destination** folder, and can copy them over.

- Files are matched by **name + size**, anywhere in the destination (folder location does not matter).
- Which file types to compare is defined in a JSON file types file (default: pictures and videos).
- Missing files are written to a JSON result file; a separate copy step copies them to the destination, never overwriting anything.
- WPF desktop app and a command line interface with the same functions.

See [MissingFiles_specification_v2.md](MissingFiles_specification_v2.md) for the full specification.

> Status: early development. Done: WP0 (project skeleton), WP1 (file types, scan, scan result file), WP2 (copy, copy log), WP3 (command line), WP4 (desktop app: scan and result view), WP5 (desktop app: copy), WP6 (functional tests, test data generator, performance test), WP7 (full pipeline: static analysis, security scanning, UI smoke tests), WP9 (Claude configuration). Next: WP10 (configuration management: four-part revision).

## Desktop app

Start `MissingFiles.exe`, choose the source and destination folders, tick the file type groups to compare,
and press **Start scan**. The result shows the counters, the path of the result file, and the list of missing
files, which can be sorted and filtered. **Open result file...** shows the result of an earlier scan.
The folders and the file types file are remembered for the next start.

**Copy missing files...** shows what will be copied and asks for confirmation, then copies each file to the
same relative path in the destination. Existing files are never overwritten. When the copy ends, the copy result
lists the counters, the copy log and any files that were not copied; **Last copy result...** shows it again.

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

`tests/MissingFiles.FunctionalTests` runs the test cases of the specification (§5) through the command line:
a scan of every case TC-01…TC-23 checked against the expected result file, and scan → copy → scan for each.
The files are generated per test in the temp folder and deleted afterwards.

`tests/MissingFiles.UiTests` runs the smoke tests of the main workflows with [FlaUI](https://github.com/FlaUI/FlaUI):
it starts the real `MissingFiles.exe`, so `dotnet test` opens windows for about half a minute and needs a desktop
session. The controls are used through UI Automation, not with the mouse and keyboard.

The tests keep away from your settings with the environment variable `MISSINGFILES_DATA_FOLDER`: the desktop app then
uses `settings.json` and `FileTypes.json` in that folder, which is created if needed, and not those in
`%APPDATA%\MissingFiles`. An empty value counts as not set, and any other value that is not an absolute path stops
the start with an error message. The output folder does not change, and the command line ignores the variable.

The performance test (FT-10, 1,000,000 files per side) is skipped unless enabled. It runs nightly in the
*Performance* workflow, or locally:

```powershell
$env:MISSINGFILES_PERF = '1'   # optional: MISSINGFILES_PERF_FILES, MISSINGFILES_PERF_MAX_SECONDS, MISSINGFILES_PERF_MAX_MB
dotnet test tests/MissingFiles.FunctionalTests -c Release --filter Category=Performance
```

Every push and pull request runs these on GitHub Actions (`windows-latest`):

| Check | Tooling |
|---|---|
| Build | `dotnet build -c Release`, warnings are errors, .NET analyzers (`latest-recommended`) |
| Tests | `dotnet test`: unit, functional and UI smoke tests, with at least 80 % line coverage on `MissingFiles.Core` |
| Lint | `dotnet format --verify-no-changes`, `.editorconfig` |
| Security | CodeQL, `dotnet list package --vulnerable --include-transitive` |
| Artifacts | Self-contained single-file `MissingFiles.exe` and `MissingFiles.Cli.exe` |

Dependabot keeps the NuGet packages and GitHub Actions up to date. The UX checklist in §6.5 of the specification is
done by hand for each release. Secret scanning and push protection are GitHub repository settings, not pipeline
steps; `/release` confirms that both are enabled before publishing (DEV-07).

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
| `tests/MissingFiles.FunctionalTests` | Functional tests (spec §5), test data generator and performance test. |
| `tests/MissingFiles.UiTests` | Smoke tests of the main workflows in the real desktop app (FlaUI). |

## Windows SmartScreen warning

The executables are not code-signed. On first start Windows may show *"Windows protected your PC"*. Click **More info → Run anyway**.
