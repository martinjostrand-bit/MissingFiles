# MissingFiles – Project Specification

Version: 0.3 · Date: 2026-09-27 · Author: Martin Strand

All open decisions are resolved (see §7).

---

## 1. Introduction

### 1.1 Purpose
MissingFiles scans a **source folder** (recursively) for files of selected types and checks whether a file with the same name exists anywhere in the **destination folder** (recursively). Files without a match are reported as *missing*. In a separate step, the missing files can be copied to the destination.

Typical use case: verify that all photos and videos from a camera card, phone backup or old disk have been transferred to the main photo archive, and copy the ones that were not.

### 1.2 Glossary
| Term | Meaning |
|---|---|
| Source root | Folder selected by the user as source. Scanned recursively. |
| Destination root | Folder selected by the user as destination. Scanned recursively. |
| Relative path | Path of a file relative to its root, e.g. `2021\Summer\IMG_0001.JPG`. |
| Match | A source file for which the destination contains a file satisfying the match rule (§4.3). |
| Missing file | A source file with no match. |
| Scan | The comparison operation (source vs. destination). |
| Scan result file | JSON file listing missing files produced by a scan (§4.4). |
| Copy | The operation that copies files listed in a scan result file to the destination. |
| Progress dialog | Modal window shown while a scan or copy is running. |

### 1.3 Out of scope (v1)
- Deleting or moving files.
- Two-way synchronisation.
- Detecting renamed files (same content, different name).
- Cloud storage APIs (only local/mapped/UNC file system paths).

---

## 2. Work packages

| WP | Content | Depends on |
|---|---|---|
| WP0 | Repository, solution structure, `.gitignore`, CI pipeline skeleton | – |
| WP1 | Core library: file types file (load/validate), file enumeration, filtering, matching, scan result JSON | WP0 |
| WP2 | Core library: copy engine, copy log | WP1 |
| WP3 | CLI (`scan`, `copy`) | WP1, WP2 |
| WP4 | WPF UI: folder selection, scan, progress dialog, result view | WP1 |
| WP5 | WPF UI: copy workflow, progress dialog, result view | WP2, WP4 |
| WP6 | Functional test suite and test data generator | WP1, WP2 |
| WP7 | Full pipeline: static analysis, security scanning, UI smoke tests | WP0 |
| WP8 | Packaging/distribution, user documentation | WP3, WP5 |

---

## 3. Build environment

### 3.1 GitHub account
Use the GitHub account `martinjostrand-bit` (email: martin.jo.strand@gmail.com) for this project — the same account used for my other repositories. When creating or initializing the repository, authenticating `gh`/`git`, or pushing code, use this account rather than any other account that might be available on this machine.

- When a change is complete and tested: make a git commit with a short, descriptive message.
- After each commit: push to GitHub (`git push`) so the code is always backed up.

### 3.2 Tech stack
- **.NET 10 (LTS)**, C#.
- **UI: WPF** (decided). Rationale: mature, runs on Windows 10 and 11 without extra runtime dependencies (WinUI 3 requires the Windows App SDK), good UI-automation test support.
- Solution structure:
  - `MissingFiles.Core` – all logic (scan, match, copy, JSON, logging). No UI dependencies.
  - `MissingFiles.Cli` – console app, thin layer over Core.
  - `MissingFiles.App` – WPF app (MVVM), thin layer over Core.
  - `MissingFiles.Core.Tests` – unit tests (xUnit).
  - `MissingFiles.FunctionalTests` – functional tests (§5).
- Before committing:
  - `dotnet build` succeeds with no errors **and no warnings** (warnings treated as errors in CI).
  - `dotnet test` passes.
  - `bin/` and `obj/` are excluded via `.gitignore` (`dotnet new gitignore`) — never commit build output.

### 3.3 Target platforms
- Windows 11 and Windows 10 (22H2), x64 only. ARM64 is not supported.

### 3.4 Build pipeline (GitHub Actions, `windows-latest`)
Runs on every push and pull request:

| Step | Tooling |
|---|---|
| Build | `dotnet build -c Release` with `TreatWarningsAsErrors` |
| Unit tests + coverage | `dotnet test`, Coverlet; target ≥ 80 % line coverage on `MissingFiles.Core` |
| Functional tests | `MissingFiles.FunctionalTests` (§5) |
| Static analysis / lint | .NET analyzers (`AnalysisLevel=latest-recommended`), `dotnet format --verify-no-changes`, `.editorconfig` |
| Security | GitHub CodeQL (C#), `dotnet list package --vulnerable --include-transitive`, Dependabot, secret scanning |
| UX / UI | FlaUI-based smoke test of main workflows; manual UX checklist per release (§6.5) |
| Artifacts | Publish self-contained single-file `.exe` for CLI and UI as build artifacts |

---

## 4. Requirements

Requirements use “shall”. Each has an ID for traceability to tests.

### 4.1 Input and configuration
- **REQ-01** The user shall select a source root and a destination root (UI: folder picker and text box; CLI: arguments).
- **REQ-02** The application shall reject the scan if either folder does not exist, is not accessible, or if one root is the same as or inside the other, with a clear error message.
- **REQ-03** Both roots shall be scanned recursively, including all subfolders.
- **REQ-03a** A drive root (e.g. `D:\`) shall be accepted as source or destination. The folders `$Recycle.Bin`, `System Volume Information` and `$WinREAgent` shall always be excluded.
- **REQ-04** The UI shall remember the last used source, destination, and output folder between sessions.

### 4.2 File types
The application works with **any file type**. Which types are compared is defined in a **file types file** (JSON), not hard-coded. Pictures and videos are only the shipped default.

- **REQ-05** The file types to compare shall be read from a file types file with this structure:

```json
{
  "schemaVersion": 1,
  "groups": [
    {
      "name": "Pictures",
      "enabled": true,
      "extensions": [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tif", ".tiff", ".heic", ".heif",
                     ".webp", ".raw", ".cr2", ".cr3", ".nef", ".arw", ".dng", ".orf", ".rw2"]
    },
    {
      "name": "Videos",
      "enabled": true,
      "extensions": [".mp4", ".mov", ".avi", ".mkv", ".m4v", ".wmv", ".mts", ".m2ts", ".3gp", ".mpg", ".mpeg"]
    },
    {
      "name": "Documents",
      "enabled": false,
      "extensions": [".pdf", ".docx", ".xlsx", ".pptx", ".txt"]
    }
  ]
}
```

- **REQ-05a** Rules for the file types file:
  - Extensions are compared case-insensitively; a missing leading dot is added automatically (`jpg` = `.jpg`).
  - Only groups with `"enabled": true` are used. An extension may appear in several groups; it is counted once.
  - The special entry `"*"` means **all files, regardless of extension** (including files without an extension).
  - The entry `""` (empty string) means files **without** an extension.
  - Unknown properties are ignored (forward compatibility).
- **REQ-05b** The application shall ship with a default file `FileTypes.default.json` (pictures + videos, as above). On first start it is copied to `%APPDATA%\MissingFiles\FileTypes.json`, which the user may edit. The shipped default is never modified.
- **REQ-05c** The user shall be able to choose another file types file (UI: file picker; CLI: `--types-file`). The UI shall remember the last used file.
- **REQ-05d** The file types file shall be validated before the scan starts. If it is missing, not valid JSON, or has no enabled extensions, the scan shall not start and a message shall name the file and the problem (with line number for JSON errors).
- **REQ-06** The UI shall show the groups from the file types file as checkboxes (initial state = `enabled`), with the extensions of each group visible, so the user can switch groups on/off for a single scan without editing the file. The UI shall **not** edit or save the file types file; editing is done in a text editor. The UI shall provide an “Open file types file” button (opens it in the default text editor) and a “Reload” button (re-reads and validates it).
- **REQ-06a** The scan result file shall record the file types file path and the effective list of extensions used (field `extensions`), so a scan can be reproduced.
- **REQ-07** Hidden and system files shall be skipped by default. Symbolic links and junctions shall not be followed (prevents loops).

### 4.3 Matching
- **REQ-08** A source file matches if the destination contains at least one file, located in any subfolder, with:
  - the **same file name including extension**, compared **case-insensitively**, **and**
  - the **same file size in bytes**.
- **REQ-09** `.jpg` and `.jpeg` are different extensions for matching purposes (no extension aliasing).
- **REQ-10** A destination file with the same name but a different size is **not** a match. Such source files are reported as missing and flagged `"sameNameDifferentSize": true` in the scan result, so the user can see that a different file with that name already exists.
- **REQ-11** If several source files share the same name (in different folders), each is evaluated individually against the destination. One destination file can match several source files; the result shall report the number of such duplicate-name groups as a warning.
- **REQ-11a** File size shall be taken from the directory enumeration metadata (no files are opened during the scan), so name + size matching has no extra I/O cost compared to name-only.
- *Future (not v1):* optional *name + content* mode (SHA-256 hash, computed only for name + size candidates).

### 4.4 Scan
- **REQ-12** The scan shall be started with a “Start scan” button (UI) or `scan` command (CLI).
- **REQ-13** During the scan a modal progress dialog shall show: phase (enumerating destination / scanning source), number of source files scanned, number of matches found, number of missing files, elapsed time, and a “Cancel” button.
- **REQ-14** The UI shall remain responsive during the scan; progress shall update at least every 500 ms.
- **REQ-15** “Cancel” shall stop the scan within 2 seconds. No scan result file shall be written for a cancelled scan.
- **REQ-16** Files or folders that cannot be read (access denied, path too long, file removed during scan) shall not abort the scan; they are counted and listed as errors in the result file.
- **REQ-17** Paths longer than 260 characters shall be supported.

### 4.5 Scan result file
- **REQ-18** On completion, the scan result shall be written to `MissingFiles_YYYYMMDD_HHMMSS.json` (local time) in the output folder. Default output folder: `%USERPROFILE%\Documents\MissingFiles\`; user-configurable.
- **REQ-19** The file shall be UTF-8 JSON with the following structure:

```json
{
  "schemaVersion": 1,
  "application": "MissingFiles 1.0.0",
  "scanStarted": "2026-09-27T14:03:12+02:00",
  "scanFinished": "2026-09-27T14:04:40+02:00",
  "sourceRoot": "D:\\CameraBackup",
  "destinationRoot": "E:\\PhotoArchive",
  "matchMode": "NameAndSize",
  "fileTypesFile": "C:\\Users\\marti\\AppData\\Roaming\\MissingFiles\\FileTypes.json",
  "extensions": [".jpg", ".mp4"],
  "summary": {
    "sourceFilesScanned": 12450,
    "matched": 12310,
    "missing": 138,
    "errors": 2,
    "duplicateNameGroups": 17,
    "sameNameDifferentSize": 5
  },
  "missingFiles": [
    {
      "relativePath": "2021\\Summer\\IMG_0001.JPG",
      "sizeBytes": 4839211,
      "lastWriteTimeUtc": "2021-07-14T10:22:05Z",
      "sameNameDifferentSize": false
    }
  ],
  "errors": [
    { "path": "D:\\CameraBackup\\locked\\VID_01.MP4", "message": "Access denied" }
  ]
}
```

### 4.6 Scan result presentation
- **REQ-20** After the scan, a result view shall show: files scanned, matched, missing, errors, duplicate-name warnings, elapsed time, and the full path of the result file (with a button to open its folder).
- **REQ-21** The result view shall list the missing files with columns: relative path, size, last-write date, and a “same name, different size” indicator. The list shall be sortable by each column, filterable by text, and virtualised (see NFR-03a).
- **REQ-21a** The result view shall also be shown when a previous scan result file is opened (REQ-22), so the user can review the list before copying.

### 4.7 Copy
- **REQ-22** The user shall select a scan result file (defaults to the one from the last scan).
- **REQ-23** Before copying, the application shall show source root, destination root, number of files and total size, and check that the destination drive has enough free space.
- **REQ-24** Copy shall be started with a “Start copy” button (UI) or `copy` command (CLI).
- **REQ-25** Each file shall be copied from `sourceRoot\relativePath` to `destinationRoot\relativePath`. Missing folders are created.
- **REQ-26** A file shall **never be overwritten**. If the target path already exists, the file is skipped and logged as “skipped – exists”.
- **REQ-27** If the source file no longer exists or its size/last-write time differs from the scan result, it is skipped and logged as “skipped – source changed”.
- **REQ-28** Files shall be copied to a temporary name and renamed on completion, so that an aborted copy never leaves a partial file under the real name. The original last-write time shall be preserved.
- **REQ-29** A failed file (I/O error, access denied) shall not abort the copy; it is logged and counted.
- **REQ-30** During the copy a modal progress dialog shall show: files copied / total, bytes copied / total, skipped, failed, elapsed time, estimated time remaining, and a “Cancel” button. Cancel stops after the current file.
- **REQ-31** After the copy, a result view shall show the same counters and the path of the copy log.

### 4.8 Copy log
- **REQ-32** Each copy run shall write a UTF-8 text log `MissingFilesCopy_YYYYMMDD_HHMMSS.log` in the output folder containing:
  1. **Header** – application version, start/end time, scan result file used, source root, destination root, user, machine.
  2. **Summary** – total, copied, skipped (exists), skipped (source changed), failed, cancelled (yes/no), elapsed time.
  3. **Details** – one line per file: timestamp, status, relative path, size, error message (if any).

### 4.9 Command line interface
- **REQ-33** The CLI shall provide the same functions as the UI:

```
MissingFiles scan --source <path> --dest <path> [--out <folder>] [--types-file <file.json>] [--groups Pictures,Videos]
MissingFiles copy --result <file.json> [--out <folder>] [--dry-run]
MissingFiles --help | --version
```

- **REQ-33a** Without `--types-file`, the CLI uses `%APPDATA%\MissingFiles\FileTypes.json` (or the shipped default if that does not exist). `--groups` overrides which groups are enabled; unknown group names are an error (exit code 3).
- **REQ-34** The CLI shall print progress and a final summary to stdout, errors to stderr, and support Ctrl+C for cancellation.
- **REQ-35** Exit codes: `0` success, no missing files / all copied · `1` success, missing files found / some files skipped · `2` completed with file errors · `3` invalid arguments · `4` cancelled · `5` fatal error.

---

## 5. Functional test specification

### 5.1 Test data
- **FT-01** A test data generator shall create source and destination folders under a temp directory, populated with tiny dummy files (≤ 1 KB) using realistic names (`IMG_0001.JPG`, `DSC04512.ARW`, `VID_20210714_102205.mp4`, …). Test data is created per test and deleted afterwards.

### 5.2 Test cases
Each test case defines the folder content **and the expected result** (counts and listed files).

| ID | Scenario | Expected |
|---|---|---|
| TC-01 | All files match (same relative paths, same sizes) | missing = 0 |
| TC-02 | All files match, different subfolders in destination | missing = 0 |
| TC-03 | No files match | missing = all |
| TC-04 | Some files missing | exactly those listed |
| TC-05 | Duplicate names in destination, one with matching size | treated as match |
| TC-06 | Duplicate names in source (different folders) | each evaluated; duplicate warning reported |
| TC-07 | Case differences (`img_1.jpg` vs `IMG_1.JPG`) | match |
| TC-08 | `.jpg` vs `.jpeg` same base name | no match |
| TC-09 | Unsupported types (`.txt`, `.docx`) in source | ignored |
| TC-10 | Empty source / empty destination | valid result, no crash |
| TC-11 | Deeply nested folders and path > 260 chars | handled |
| TC-12 | Unreadable file/folder | listed as error, scan completes |
| TC-13 | Same name, different size | missing, flagged `sameNameDifferentSize` |
| TC-14 | Destination inside source | rejected (REQ-02) |
| TC-15 | Same name, different size, same relative path; then copy | copy skips it (“skipped – exists”), never overwrites |
| TC-16 | Zero-byte files with same name | match |
| TC-17 | Source root is a drive root containing `$Recycle.Bin` / `System Volume Information` | those folders ignored |
| TC-18 | Custom file types file with only `.pdf` | only PDFs compared, pictures ignored |
| TC-19 | File types file with `"*"` | all files compared, incl. files without extension |
| TC-20 | File types file with `""` | only files without extension compared |
| TC-21 | Extensions written as `JPG`, `jpg`, `.Jpg` in the file | all treated as `.jpg` |
| TC-22 | Group disabled in file, enabled via UI checkbox / `--groups` | group’s extensions compared |
| TC-23 | File types file missing, invalid JSON, or no enabled extensions | scan not started, clear error, CLI exit code 3 |

### 5.3 Scan tests
- **FT-02** Run a scan for each of TC-01…TC-23 and verify counters and the JSON content against the expected result.

### 5.4 Copy tests
- **FT-03** Copy each file listed in a scan result; verify files exist at the correct relative path with identical content and timestamp.
- **FT-04** Target already exists → skipped, not overwritten.
- **FT-05** Source file deleted or modified after the scan → skipped – source changed.
- **FT-06** Cancel during copy → no partial files remain.
- **FT-07** `--dry-run` → no files written, log lists what would be copied.

### 5.5 Round-trip test
- **FT-08** For each test case: scan → copy → scan again. The second scan shall report missing = 0 (except files that failed or were skipped by design, e.g. TC-15).

### 5.7 Performance test
- **FT-10** Generate a synthetic tree of 1,000,000 zero-byte files (source) and 1,000,000 (destination) and verify NFR-01…NFR-03. Runs nightly/manually, not on every push.

### 5.6 CLI tests
- **FT-09** Verify arguments, `--help`, and exit codes (REQ-35).

---

## 6. Non-functional requirements

### 6.1 Performance
Design case: source and/or destination is a **full hard drive** (e.g. 4 TB of photos/videos, several hundred thousand media files among up to ~2,000,000 files in total). The application shall be designed and tested for up to **1,000,000 media files per side**.

Reference machine: 8 GB RAM, internal NVMe SSD or external USB 3 HDD.

- **NFR-01** Scan of 1,000,000 source files against 1,000,000 destination files shall complete in:
  - < 2 min on a local NVMe SSD (warm or cold file system cache),
  - < 10 min on an external USB 3 HDD (cold cache). On an HDD, enumeration is I/O-bound; the target is that MissingFiles is no slower than `dir /s` on the same drive.
- **NFR-02** Matching shall use a dictionary keyed by (lower-cased name, size) built from a single enumeration of the destination; lookup per source file O(1). No file contents are read during the scan.
- **NFR-02a** Enumeration shall use the fast .NET `FileSystemEnumerable` / `Directory.EnumerateFiles` API with `EnumerationOptions` (not `FileInfo` per file), and the source and destination may be enumerated in parallel when they are on different physical drives.
- **NFR-03** Memory usage shall stay below 500 MB for 1,000,000 files per side.
- **NFR-03a** The UI shall stay responsive and the result list shall use UI virtualisation, so listing 100,000+ missing files does not freeze the window.
- **NFR-04** Copy throughput shall be at least 90 % of Windows Explorer copy speed on the same hardware.

### 6.2 Reliability and data safety
- **NFR-05** The application shall never delete, move, or overwrite user files.
- **NFR-06** The source is opened read-only in all operations.

### 6.3 Usability
- **NFR-07** UI, CLI output, logs and documentation in English only. No localisation in v1.
- **NFR-08** Supports Windows light/dark theme, keyboard navigation, and screen readers (UI Automation names on all controls).
- **NFR-09** Supports high-DPI displays (per-monitor DPI aware).

### 6.4 Deployment
- **NFR-10** Distributed as self-contained single-file x64 executables (`MissingFiles.exe` for the UI, `MissingFiles.Cli.exe` for the CLI; no separate .NET install needed, no installer). Published as a zip on GitHub Releases, together with `FileTypes.default.json`.
- **NFR-10a** The executables are not code-signed in v1. Windows SmartScreen will show an “unrecognised app” warning on first start; the README shall explain this (“More info → Run anyway”).
- **NFR-11** No administrator rights required.
- **NFR-12** No network access, no telemetry.

### 6.5 UX checklist (manual, per release)
- Main workflow (select folders → scan → copy) can be done without documentation.
- All errors show a message explaining what happened and what to do.
- Progress dialogs update smoothly; cancel responds promptly.
- Windows resize correctly; nothing is clipped at 150 % scaling.

---

## 7. Decisions log

| Date | Decision | Ref |
|---|---|---|
| 2026-09-27 | UI framework: WPF | §3.2 |
| 2026-09-27 | Match rule: name + size | REQ-08 |
| 2026-09-27 | Design case: full hard drive, up to 1,000,000 files per side | §6.1 |
| 2026-09-27 | Any file type, defined in a JSON file types file | §4.2 |
| 2026-09-27 | Platform: x64 only, no ARM64 | §3.3 |
| 2026-09-27 | File types file edited in a text editor only (UI: open + reload) | REQ-06 |
| 2026-09-27 | Result view lists the missing files | REQ-21 |
| 2026-09-27 | Language: English only | NFR-07 |
| 2026-09-27 | Distribution: single .exe in a zip, no installer, no code signing | NFR-10 |

No open decisions.
