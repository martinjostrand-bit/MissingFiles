using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;

namespace MissingFiles.FunctionalTests;

/// <summary>Real Windows conditions for test cases: unreadable folders and drive roots.</summary>
internal static class WindowsEnvironment
{
    /// <summary>Denies the current user listing the folder (TC-12) until disposed.</summary>
    public static IDisposable DenyListing(string folder)
    {
        var directory = new DirectoryInfo(folder);
        var rule = new FileSystemAccessRule(
            WindowsIdentity.GetCurrent().User!, FileSystemRights.ListDirectory, AccessControlType.Deny);
        var security = directory.GetAccessControl();
        security.AddAccessRule(rule);
        directory.SetAccessControl(security);

        return new Cleanup(() =>
        {
            security.RemoveAccessRule(rule);
            directory.SetAccessControl(security);
        });
    }

    /// <summary>
    /// Maps <paramref name="folder"/> to a free drive letter with <c>subst</c>, so it can be scanned
    /// as a drive root such as <c>X:\</c> (TC-17). No administrator rights are needed.
    /// </summary>
    /// <returns>The drive root, e.g. <c>X:\</c>, and the cleanup that removes the drive again.</returns>
    public static (string Root, IDisposable Cleanup) Subst(string folder)
    {
        var used = DriveInfo.GetDrives().Select(d => char.ToUpperInvariant(d.Name[0])).ToHashSet();
        var letter = "ZYXWVUTSRQPONMLKJIHGFED".FirstOrDefault(c => !used.Contains(c));
        if (letter == default)
        {
            throw new InvalidOperationException("No free drive letter for subst.");
        }

        var drive = $"{letter}:";
        Run("subst.exe", drive, folder);
        return (drive + @"\", new Cleanup(() => Run("subst.exe", drive, "/D")));
    }

    private static void Run(string program, params string[] arguments)
    {
        var start = new ProcessStartInfo(program) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{program} {string.Join(' ', arguments)} failed ({process.ExitCode}): {output}");
        }
    }

    private sealed class Cleanup(Action action) : IDisposable
    {
        public void Dispose() => action();
    }
}
