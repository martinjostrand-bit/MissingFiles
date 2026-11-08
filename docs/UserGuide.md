# MissingFiles user guide

MissingFiles finds the files in a **source** folder that are missing from a **destination** folder, and can copy
them there. A typical use is to check that all photos and videos from a camera card, a phone backup or an old disk
are in your photo archive, and to copy the ones that are not.

MissingFiles works in two steps:

1. **Scan.** MissingFiles compares the source with the destination and writes the missing files to a
   *scan result file*. Nothing is copied yet.
2. **Copy.** MissingFiles copies the files listed in a scan result file to the destination, and writes a *copy log*.

Both steps are available in the desktop app (`MissingFiles.exe`) and on the command line (`MissingFiles.Cli.exe`).

- [Install](#install)
- [How files are compared](#how-files-are-compared)
- [Desktop app](#desktop-app)
- [Command line](#command-line)
- [File types file](#file-types-file)
- [Scan result file](#scan-result-file)
- [Copy log](#copy-log)
- [Your files are safe](#your-files-are-safe)
- [Troubleshooting](#troubleshooting)

## Install

MissingFiles runs on Windows 10 (22H2) and Windows 11, x64. It needs no installer, no .NET installation and no
administrator rights.

1. Download `MissingFiles-XX.YY.ZZ.BB-win-x64.zip` from the
   [releases page](https://github.com/martinjostrand-bit/MissingFiles/releases), where `XX.YY.ZZ.BB` is the revision.
2. Extract the zip to a folder of your choice, for example `C:\Tools\MissingFiles`.
3. Start `MissingFiles.exe`.

The zip holds these files:

| File | Purpose |
|---|---|
| `MissingFiles.exe` | The desktop app. |
| `MissingFiles.Cli.exe` | The command line interface. |
| `FileTypes.default.json` | A copy of the built-in file types, as a starting point for your own [file types file](#file-types-file). The applications do not need it. |
| `LICENSE` | The license. The applications also show it under **About** and `--about`. |

### Windows SmartScreen warning

The executables are not code-signed. On first start Windows may show *"Windows protected your PC"*. Click
**More info → Run anyway**. To prevent the warning for all files, right-click the zip before you extract it,
choose **Properties**, tick **Unblock** and click **OK**.

### Update and uninstall

To update, extract the new zip over the old files. Your settings and your file types file are kept.

To uninstall, delete the folder with the executables. MissingFiles also keeps these, which you can delete when you
no longer need them:

- `%APPDATA%\MissingFiles`: the settings of the desktop app (`settings.json`) and your file types file (`FileTypes.json`).
- `Documents\MissingFiles`: scan result files and copy logs, unless you chose another folder.

MissingFiles does not use the network, and sends no telemetry.

## How files are compared

A source file is **found** (matched) if the destination contains a file, in any subfolder, with:

- the **same file name**, including the extension, where upper and lower case do not matter
  (`img_1.jpg` matches `IMG_1.JPG`), **and**
- the **same size** in bytes.

The folder does not matter: `2021\Summer\IMG_0001.JPG` in the source matches `Archive\IMG_0001.JPG` in the
destination. File contents are not compared, and no file is opened during a scan, so a scan is fast even for a full disk.

Every other source file is **missing**. Note:

- `.jpg` and `.jpeg` are different extensions, so `IMG_1.jpg` does not match `IMG_1.jpeg`.
- If the destination has a file with the same name but another size, the source file is missing and marked
  **same name, other size**. Check those files before you copy: the copy never overwrites, so a file with the same
  name at the same relative path is skipped.
- If several source files have the same name (in different folders), each is compared on its own, and one destination
  file can match all of them. The scan then shows a warning with the number of such file names.

What is scanned:

- Both folders are scanned with all their subfolders. A drive, such as `D:\`, can be the source or the destination.
- Only the file types you select are compared (see [File types file](#file-types-file)). Other files are ignored.
- Hidden and system files and folders are skipped. So are `$Recycle.Bin`, `System Volume Information` and
  `$WinREAgent`.
- Folders that are symbolic links or junctions are not followed. Files that are links, such as OneDrive files that are
  only online, are included.
- Paths longer than 260 characters are supported.
- The source and the destination cannot be the same folder, and one cannot be inside the other.
- A folder that cannot be read does not stop the scan. It is counted, and listed in the result.

## Desktop app

### Scan

1. Under **Folders**, choose the **Source folder** (the folder to check) and the **Destination folder** (the folder
   that should contain the files). Type a path or use **Browse...**.
2. **Save results in** is the folder for scan result files and copy logs. The default is `Documents\MissingFiles`.
3. Under **File types**, tick the groups of file types to compare. Next to each group are its extensions. The ticks
   apply to this scan only; to change the groups for good, edit the file types file (see below).
4. Press **Start scan**.

A progress window shows the phase (reading the destination folder, then scanning the source folder), the number of
files read and scanned, the matches, the missing files, the folders that could not be read, and the time. **Cancel**
stops the scan within a few seconds. A cancelled scan writes no result file.

### Result

When the scan ends, the **Result** shows:

- the number of files scanned, matched and missing, how many of the missing files have the same name but another size,
  and how many folders could not be read,
- a warning if file names occur more than once in the source,
- the path of the result file. **Show in folder** opens its folder in Explorer.
- **Folders that could not be read**, with the reason for each, if there are any,
- the list of **missing files**, with relative path, size, last modified time and *same name, other size*. Click a column
  header to sort, and type in **Filter** to show only the files whose path contains that text.

**Open result file...** shows the result of an earlier scan, so you can check it and copy from it.

### Copy

The box **Copy to the destination** shows where the files are copied from and to, the number of files, their total
size, and the free space on the destination drive.

1. Press **Copy missing files...**. MissingFiles asks for confirmation.
2. Each file is copied to the same relative path in the destination, for example from
   `D:\CameraBackup\2021\Summer\IMG_0001.JPG` to `E:\PhotoArchive\2021\Summer\IMG_0001.JPG`. Missing folders are created.
3. A progress window shows the files and data copied, the files skipped and failed, the time, and an estimate of the
   time remaining. **Cancel** stops after the current file.

The copy does not start if there are no missing files, if a folder no longer exists, or if the destination drive does
not have enough free space.

A file is **skipped** if:

- a file already exists at its path in the destination (*skipped - exists*). It is never overwritten.
- the source file no longer exists, or its size or last modified time differs from the scan (*skipped - source changed*).
  Scan again to include it.

A file that cannot be copied, for example because access is denied, is **failed**. It does not stop the copy.

When the copy ends, the **Copy result** shows the counters, the path of the copy log (**Show in folder** opens its
folder), and the files that were not copied, with the reason. **Last copy result...** shows it again.

### File types

The **File types file** box shows which file types file is used. It is `%APPDATA%\MissingFiles\FileTypes.json`,
which the app creates from the built-in pictures and videos list on its first start.

- **Choose...** uses another file types file.
- **Edit** opens the file types file in your text editor.
- **Reload** reads the file again after you have saved it.

If the file has an error, a message under the box says what and where, and the scan does not start until it is
fixed. See [File types file](#file-types-file) for the format.

### Remembered settings

The app remembers the source folder, the destination folder, the folder for results and the file types file for the
next start, in `%APPDATA%\MissingFiles\settings.json`.

### About

**About** shows the name, revision, author, copyright and license. The text can be selected and copied. **Close** or
Esc closes it.

## Command line

`MissingFiles.Cli.exe` has the same functions as the desktop app. Open a terminal in the folder with the executables,
or add that folder to your `PATH`.

```powershell
# 1. Find the files in D:\CameraBackup that are missing from E:\PhotoArchive
MissingFiles.Cli scan --source D:\CameraBackup --dest E:\PhotoArchive

# 2. Check what would be copied, then copy
MissingFiles.Cli copy --result "$env:USERPROFILE\Documents\MissingFiles\MissingFiles_20260927_140312.json" --dry-run
MissingFiles.Cli copy --result "$env:USERPROFILE\Documents\MissingFiles\MissingFiles_20260927_140312.json"
```

Progress and a summary are printed to the console, errors to the error output. The summary names the result file or
copy log. Press Ctrl+C to cancel; press it again to stop at once.

Options can be written `--name value` or `--name=value`. Put paths with spaces in quotes.

### scan

```
MissingFiles.Cli scan --source <folder> --dest <folder> [--out <folder>] [--types-file <file.json>] [--groups <g1,g2>]
```

| Option | Meaning |
|---|---|
| `--source <folder>` | The folder to check. Required. |
| `--dest <folder>` | The folder that should contain the files. Required. |
| `--out <folder>` | Where to write the scan result file. Default: `Documents\MissingFiles`. |
| `--types-file <file>` | The file types file. Default: `%APPDATA%\MissingFiles\FileTypes.json` if it exists, otherwise the built-in pictures and videos list. The command line does not create the file. |
| `--groups <g1,g2>` | Compare only these groups of the file types file, separated by commas, instead of the groups marked `"enabled"`. For example `--groups Pictures,Documents`. An unknown group name is an error. |

### copy

```
MissingFiles.Cli copy --result <file.json> [--out <folder>] [--dry-run]
```

| Option | Meaning |
|---|---|
| `--result <file>` | The scan result file to copy from. Required. |
| `--out <folder>` | Where to write the copy log. Default: `Documents\MissingFiles`. |
| `--dry-run` | Check each file and log what would be copied, without writing any file to the destination. |

The copy follows the same rules as in the desktop app: it checks the folders and the free space first, never
overwrites, and skips files that have changed since the scan.

### Other options

| Option | Meaning |
|---|---|
| `--help` | Show the usage. `scan --help` and `copy --help` show the options of a command. |
| `--version` | Show the name and revision, for example `MissingFiles 1.0.0.123`. |
| `--about` | Show the name, revision, author, copyright and license. |

### Exit codes

Use the exit code in scripts, for example with `$LASTEXITCODE` in PowerShell.

| Exit code | scan | copy |
|---|---|---|
| 0 | Nothing is missing | All files copied |
| 1 | Missing files found | Some files skipped |
| 2 | Some folders could not be read | Some files could not be copied |
| 3 | Invalid arguments, or input that prevents the start: a folder is missing or inside the other, or the file types file is invalid | Invalid arguments, or input that prevents the start: the scan result file is invalid, a folder is missing, or there is not enough free space |
| 4 | Cancelled | Cancelled |
| 5 | Fatal error | Fatal error |

## File types file

The file types file says which file types are compared. It is a JSON file that you edit in a text editor, such as
Notepad. The default is `%APPDATA%\MissingFiles\FileTypes.json`:

```json
{
  // MissingFiles file types file.
  // Only groups with "enabled": true are compared.
  // "*" = all files, "" = files without an extension.
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

Rules:

- Each group has a `name`, `enabled` and a list of `extensions`. You can add, change and remove groups.
- Only groups with `"enabled": true` are compared. A group without `enabled` counts as enabled. In the desktop app
  and with `--groups` you can choose other groups for one scan.
- Upper and lower case do not matter, and the dot is optional: `JPG`, `jpg` and `.Jpg` all mean `.jpg`.
  An extension can be in more than one group.
- `"*"` means all files, whatever their extension, including files without one.
- `""` means files without an extension.
- An extension is the part after the last dot. Wildcards such as `"*.jpg"`, spaces, path characters and extensions
  with more than one dot, such as `".tar.gz"`, are errors.
- Comments (`// ...`) and trailing commas are allowed. Unknown properties are ignored.

If the file is missing, is not valid JSON, has an invalid extension, or has no enabled extensions, the scan does not
start. The message names the file and the problem, with the line number for JSON errors.

## Scan result file

Each scan writes a scan result file `MissingFiles_YYYYMMDD_HHMMSS.json`, named after the start of the scan in
local time, to the folder for results. If the name exists, `_2`, `_3` and so on is added; an existing file is never
overwritten. It is a UTF-8 JSON file:

```json
{
  "schemaVersion": 1,
  "application": "MissingFiles 1.0.0.123",
  "scanStarted": "2026-09-27T14:03:12+02:00",
  "scanFinished": "2026-09-27T14:04:40+02:00",
  "sourceRoot": "D:\\CameraBackup",
  "destinationRoot": "E:\\PhotoArchive",
  "matchMode": "NameAndSize",
  "fileTypesFile": "C:\\Users\\you\\AppData\\Roaming\\MissingFiles\\FileTypes.json",
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
    { "path": "D:\\CameraBackup\\locked", "message": "Access denied" }
  ]
}
```

- `fileTypesFile` and `extensions` record which file types were compared, so the scan can be repeated.
  `fileTypesFile` is `null` when the command line used the built-in list.
- `missingFiles` lists the missing files, relative to `sourceRoot`. The copy uses this list.
- `errors` lists the folders that could not be read.

You can remove entries from `missingFiles` in a text editor to copy only some of the files. The copy never reads or
writes outside the two folders.

## Copy log

Each copy writes a UTF-8 text file `MissingFilesCopy_YYYYMMDD_HHMMSS.log` to the folder for results. It has three
parts:

1. **Header**: application and revision, start and end time, the scan result file, the source and destination
   folders, whether it was a dry run, the user and the machine.
2. **Summary**: the number of files in the scan result, copied, skipped (exists), skipped (source changed), failed,
   not processed (after a cancel), whether the copy was cancelled, the bytes copied and the elapsed time.
3. **Details**: one line per file, with the time, the status (*Copied*, *Would copy*, *Skipped - exists*,
   *Skipped - source changed* or *Failed*), the relative path, the size in bytes, and the error message, if any.

## Your files are safe

- MissingFiles never deletes, moves or overwrites your files. A file that already exists in the destination is skipped.
- The source is only read, never changed.
- Each file is first copied to a temporary name ending in `.missingfiles-tmp`, and renamed when it is complete, so a
  stopped copy never leaves a partial file under the real name. The last modified time of the original is kept.
  If the computer stops during a copy, a `.missingfiles-tmp` file can remain in the destination; you can delete it.
- MissingFiles does not use the network, and sends no telemetry.

## Troubleshooting

| Problem | What to do |
|---|---|
| *"Windows protected your PC"* | Click **More info → Run anyway**. See [Windows SmartScreen warning](#windows-smartscreen-warning). |
| The scan does not start, with a message about a folder | Check that both folders exist and can be read, and that neither is inside the other. |
| The scan does not start, with a message about the file types file | Fix the file at the line in the message, save it and press **Reload**, or choose another file. See [File types file](#file-types-file). |
| "No file types are selected" | Tick at least one group that has extensions. |
| Folders that could not be read | Usually access is denied. Check the permissions of the folders listed, or ignore them if they do not hold your files. |
| Many files *same name, other size* | The destination has other files with the same names, such as edited photos or files from another camera. Check them before you copy. |
| The copy does not start: not enough free space | Free space on the destination drive, or remove entries from the scan result file. |
| Files *skipped - source changed* | The source changed after the scan. Scan again, then copy. |
| Files *skipped - exists* | A file is already at that path in the destination. MissingFiles never overwrites it. Compare the two files yourself. |
