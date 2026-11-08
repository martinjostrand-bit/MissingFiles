<#
.SYNOPSIS
Builds the release zip MissingFiles-XX.YY.ZZ.BB-win-x64.zip (spec NFR-10, CM-06).

.DESCRIPTION
Publishes MissingFiles.exe and MissingFiles.Cli.exe as self-contained single-file x64 executables, and
zips them with FileTypes.default.json and LICENSE. The revision comes from Directory.Build.props, with BB
from the variable MissingFilesBuildNumber: ci.yml sets it to its run number, and other builds use 0 (CM-02).

The output folder must be new or empty, so the script never deletes or overwrites a file. It gets:
  publish\                            the four files of the zip
  MissingFiles-XX.YY.ZZ.BB-win-x64.zip

Runs in Windows PowerShell 5.1 and in PowerShell 7.

.PARAMETER Output
The output folder. Default: artifacts in the repository.

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File build\Package.ps1
#>
[CmdletBinding()]
param(
    [string] $Output
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Stop-Packaging([string] $Message) {
    [Console]::Error.WriteLine("Error: $Message")
    exit 1
}

$root = Split-Path -Parent $PSScriptRoot
if (-not $Output) {
    $Output = Join-Path $root 'artifacts'
}

# Relative to the current folder of PowerShell, not of the process.
$Output = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Output)
if ((Test-Path -LiteralPath $Output) -and @(Get-ChildItem -LiteralPath $Output -Force).Count -gt 0) {
    Stop-Packaging "The output folder '$Output' is not empty. Choose a new or empty folder."
}

$publish = Join-Path $Output 'publish'
$executables = 'MissingFiles.exe', 'MissingFiles.Cli.exe'
$published = $executables + 'FileTypes.default.json'

# Self-contained single file (NFR-10): no .NET install needed, and no other files next to the executables.
# DebugType=none leaves out the .pdb files.
$arguments = @('-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
               '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true',
               '-p:EnableCompressionInSingleFile=true', '-p:DebugType=none', '-o', $publish)
foreach ($project in 'MissingFiles.App', 'MissingFiles.Cli') {
    & dotnet publish (Join-Path $root "src\$project") @arguments
    if ($LASTEXITCODE -ne 0) {
        Stop-Packaging "dotnet publish of $project failed with exit code $LASTEXITCODE."
    }
}

# A file that is not in the zip would be missing at run time, so any other file stops the packaging.
$files = @(Get-ChildItem -LiteralPath $publish -Force | ForEach-Object { $_.Name })
$unexpected = @($files | Where-Object { $published -notcontains $_ })
$missing = @($published | Where-Object { $files -notcontains $_ })
if ($unexpected.Count -gt 0 -or $missing.Count -gt 0) {
    Stop-Packaging ("The publish folder does not have exactly $($published -join ', '). " +
        "Unexpected: $(if ($unexpected) { $unexpected -join ', ' } else { 'none' }). " +
        "Missing: $(if ($missing) { $missing -join ', ' } else { 'none' }).")
}

# One revision for both executables (CM-05), and that revision in the name of the zip (CM-06).
$versions = @($executables | ForEach-Object {
    $info = (Get-Item -LiteralPath (Join-Path $publish $_)).VersionInfo
    $info.FileVersion
    $info.ProductVersion
} | Select-Object -Unique)
if ($versions.Count -ne 1 -or $versions[0] -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    Stop-Packaging "The executables do not have one four-part revision (CM-01, CM-05): $($versions -join ', ')."
}

$revision = $versions[0]
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $publish 'LICENSE')

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = Join-Path $Output "MissingFiles-$revision-win-x64.zip"
[System.IO.Compression.ZipFile]::CreateFromDirectory($publish, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $false)

Write-Output $zip
