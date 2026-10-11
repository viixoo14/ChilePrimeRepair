using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Text;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.Win32;

namespace ChilePrimeRepair.Core;

[SupportedOSPlatform("windows")]
public static class WindowsInstallationDiscovery
{
    public static IEnumerable<string?> Candidates(CancellationToken token)
    {
        // Prefer the client actually in use when more than one copy exists.
        foreach (var path in RunningClientPaths(token)) yield return path;
        foreach (var path in RegisteredPaths(token)) yield return path;
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FiveM", "FiveM.app");
        var shortcutFolders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Internet Explorer", "Quick Launch", "User Pinned", "TaskBar")
        };
        foreach (var path in ShortcutTargets(shortcutFolders, token)) yield return path;
        var commonFolders = new List<string>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            AppContext.BaseDirectory
        };
        foreach (var drive in DriveInfo.GetDrives())
        {
            token.ThrowIfCancellationRequested();
            if (!IsAvailableFixedDrive(drive)) continue;
            foreach (var name in new[] { "FiveM", "Games", "Juegos", "Program Files", "Program Files (x86)" })
                commonFolders.Add(Path.Combine(drive.RootDirectory.FullName, name));
        }
        foreach (var path in CommonFolderCandidates(commonFolders, token)) yield return path;
    }

    public static IEnumerable<string> RunningClientPaths(CancellationToken token)
    {
        foreach (var process in Process.GetProcesses())
        {
            token.ThrowIfCancellationRequested();
            string? executable = null;
            using (process)
            {
                try
                {
                    if (process.ProcessName.StartsWith("FiveM", StringComparison.OrdinalIgnoreCase))
                        executable = process.MainModule?.FileName;
                }
                catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or NotSupportedException) { }
            }
            if (executable is null) continue;
            if (Path.GetFileName(executable).Equals("FiveM.exe", StringComparison.OrdinalIgnoreCase)) yield return executable;
            var directory = Path.GetDirectoryName(executable);
            for (var depth = 0; directory is not null && depth < 8; depth++, directory = Path.GetDirectoryName(directory))
                if (Path.GetFileName(directory).Equals("FiveM.app", StringComparison.OrdinalIgnoreCase))
                { yield return directory; break; }
        }
    }

    private static IEnumerable<string?> RegisteredPaths(CancellationToken token)
    {
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            token.ThrowIfCancellationRequested();
            foreach (var protocol in new[] { "FiveM.ProtocolHandler", "fivem" })
                yield return Installation.LauncherFromCommand(ReadRegistry(hive, view, @"Software\Classes\" + protocol + @"\shell\open\command", ""));
            const string uninstall = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\CitizenFX_FiveM";
            yield return ReadRegistry(hive, view, uninstall, "InstallLocation");
            yield return Installation.LauncherFromCommand(ReadRegistry(hive, view, uninstall, "DisplayIcon"));
            yield return Installation.LauncherFromCommand(ReadRegistry(hive, view, uninstall, "UninstallString"));
        }
    }

    private static string? ReadRegistry(RegistryHive hive, RegistryView view, string path, string name)
    {
        try
        {
            using var root = RegistryKey.OpenBaseKey(hive, view);
            using var key = root.OpenSubKey(path, writable: false);
            return key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException or ArgumentException) { return null; }
    }

    public static IEnumerable<string?> ShortcutTargets(IEnumerable<string> folders, CancellationToken token = default)
    {
        foreach (var folder in folders.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase))
        foreach (var directory in SearchFolders(folder, 3, 500, token))
        foreach (var file in GetFiles(directory, "*FiveM*.lnk"))
        {
            token.ThrowIfCancellationRequested();
            yield return ShortcutTarget(file);
        }
    }

    private static string? ShortcutTarget(string path)
    {
        object? shortcut = null;
        try
        {
            Safety.EnsureNoLinks(path);
            shortcut = new WindowsShellLink();
            // STGM_READ + SLGP_RAWPATH: read metadata, without resolving or executing the link.
            ((IPersistFile)shortcut).Load(path, 0);
            var target = new StringBuilder(260);
            ((IWindowsShellLink)shortcut).GetPath(target, target.Capacity, IntPtr.Zero, 4);
            return Installation.LauncherFromCommand('"' + target.ToString() + '"');
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or COMException or
            SecurityException or ArgumentException) { return null; }
        finally
        {
            if (shortcut is not null && Marshal.IsComObject(shortcut)) Marshal.FinalReleaseComObject(shortcut);
        }
    }

    public static IEnumerable<string> CommonFolderCandidates(IEnumerable<string> folders, CancellationToken token = default)
    {
        foreach (var folder in folders.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase))
        foreach (var directory in SearchFolders(folder, 3, 2000, token))
        {
            token.ThrowIfCancellationRequested();
            var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(directory));
            if (name.Equals("FiveM", StringComparison.OrdinalIgnoreCase) || name.Equals("FiveM.app", StringComparison.OrdinalIgnoreCase))
                yield return directory;
        }
    }

    private static IEnumerable<string> SearchFolders(string root, int maxDepth, int maxFolders, CancellationToken token)
    {
        var pending = new Queue<(string Path, int Depth)>();
        pending.Enqueue((root, 0));
        var examined = 0;
        while (pending.TryDequeue(out var item) && examined++ < maxFolders)
        {
            token.ThrowIfCancellationRequested();
            if (!IsDirectFolder(item.Path)) continue;
            yield return item.Path;
            if (item.Depth == maxDepth) continue;
            foreach (var child in GetDirectories(item.Path))
            {
                if (pending.Count + examined >= maxFolders) break;
                pending.Enqueue((child, item.Depth + 1));
            }
        }
    }

    private static bool IsDirectFolder(string path)
    {
        try
        {
            if (!Installation.IsLocalPath(path)) return false;
            Safety.EnsureNoLinks(path);
            return Directory.Exists(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException or ArgumentException) { return false; }
    }

    private static bool IsAvailableFixedDrive(DriveInfo drive)
    {
        try { return drive.DriveType == DriveType.Fixed && drive.IsReady; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException) { return false; }
    }

    private static string[] GetDirectories(string path)
    {
        try { return Directory.GetDirectories(path); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException) { return []; }
    }

    private static string[] GetFiles(string path, string pattern)
    {
        try { return Directory.GetFiles(path, pattern); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException) { return []; }
    }
}
