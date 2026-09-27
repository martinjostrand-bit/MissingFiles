using System.Runtime.InteropServices;

using MissingFiles.Core.Scanning;

namespace MissingFiles.Core.Copying;

internal static partial class DiskSpace
{
    /// <summary>
    /// Free bytes available to the current user on the volume of <paramref name="directory"/>,
    /// or null if unknown. Works for drive letters, mounted folders and UNC paths.
    /// </summary>
    public static long? GetAvailableFreeBytes(string directory)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        return GetDiskFreeSpaceEx(ScanValidation.WithTrailingSeparator(directory), out var available, out _, out _)
            ? (long)Math.Min(available, long.MaxValue)
            : null;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "GetDiskFreeSpaceExW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetDiskFreeSpaceEx(
        string directoryName,
        out ulong freeBytesAvailable,
        out ulong totalNumberOfBytes,
        out ulong totalNumberOfFreeBytes);
}
