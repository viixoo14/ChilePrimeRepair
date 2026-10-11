namespace ChilePrimeRepair.Core;

public sealed record Installation(string AppDirectory, string? Launcher)
{
    public static Installation FromPath(string selected)
    {
        if (string.IsNullOrWhiteSpace(selected))
            throw new IOException("No se encontró una ruta válida de FiveM.");
        var path = Safety.FullPath(Environment.ExpandEnvironmentVariables(selected.Trim().Trim('"')));
        if (File.Exists(path))
        {
            if (!Path.GetFileName(path).Equals("FiveM.exe", StringComparison.OrdinalIgnoreCase))
                throw new IOException("El archivo detectado no es FiveM.exe.");
            path = Path.GetDirectoryName(path)!;
        }
        if (Directory.Exists(Path.Combine(path, "FiveM.app"))) path = Path.Combine(path, "FiveM.app");
        path = Safety.FullPath(path);
        Safety.EnsureNoLinks(path);
        if (!Directory.Exists(path) || !File.Exists(Path.Combine(path, "CitizenFX.ini")) ||
            !(Directory.Exists(Path.Combine(path, "citizen")) || File.Exists(Path.Combine(path, "components.json"))))
            throw new IOException("La instalación detectada no parece válida. Abre FiveM una vez, ciérralo y vuelve a intentarlo.");
        var candidates = new[] { Path.Combine(Path.GetDirectoryName(path)!, "FiveM.exe"), Path.Combine(path, "FiveM.exe") };
        return new Installation(path, candidates.FirstOrDefault(File.Exists));
    }

    public static string? Detect() => Detect(CancellationToken.None);

    public static string? Detect(CancellationToken token)
    {
        if (OperatingSystem.IsWindows()) return DetectCandidates(WindowsInstallationDiscovery.Candidates(token), token);
        return DetectCandidates([Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FiveM", "FiveM.app")], token);
    }

    public static string? DetectCandidates(IEnumerable<string?> candidates, CancellationToken token = default)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(candidate) || !seen.Add(candidate)) continue;
            try
            {
                var expanded = Environment.ExpandEnvironmentVariables(candidate.Trim().Trim('"'));
                if (!IsLocalPath(expanded)) continue;
                return FromPath(expanded).AppDirectory;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                ArgumentException or NotSupportedException or System.Security.SecurityException) { }
        }
        return null;
    }

    internal static bool IsLocalPath(string path)
    {
        if (!Path.IsPathFullyQualified(path)) return false;
        if (!OperatingSystem.IsWindows()) return true;
        var root = Path.GetPathRoot(path)!;
        // Check roots before accessing folders, including redirected shortcut/search folders.
        if (root.StartsWith(@"\\") && !root.StartsWith(@"\\?\")) return false;
        var driveRoot = root.StartsWith(@"\\?\") ? root[4..] : root;
        return !driveRoot.StartsWith("UNC", StringComparison.OrdinalIgnoreCase) &&
            new DriveInfo(driveRoot).DriveType != DriveType.Network;
    }

    public static string? LauncherFromCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        var text = Environment.ExpandEnvironmentVariables(command.Trim());
        string executable;
        if (text.StartsWith('"'))
        {
            var closing = text.IndexOf('"', 1);
            if (closing < 0) return null;
            executable = text[1..closing];
        }
        else
        {
            var end = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (end < 0) return null;
            executable = text[..(end + ".exe".Length)].Trim();
        }
        if (!Path.IsPathFullyQualified(executable) ||
            !Path.GetFileName(executable).Equals("FiveM.exe", StringComparison.OrdinalIgnoreCase)) return null;
        return executable;
    }
}
