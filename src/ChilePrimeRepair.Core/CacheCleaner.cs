namespace ChilePrimeRepair.Core;

public sealed record CacheFile(string Path, string Root, long Length);
public sealed record CachePlan(string AppDirectory, IReadOnlyList<CacheFile> Files, IReadOnlyList<string> Directories, IReadOnlyList<string> Roots)
{
    public long Bytes => Files.Sum(f => f.Length);
}
public sealed record CleanupResult(int DeletedFiles, long DeletedBytes, int PlannedFiles, string? Error)
{
    public bool Success => Error is null;
}

public static class CacheCleaner
{
    private static readonly string[] Names = ["cache", "server-cache", "server-cache-priv"];

    public static CachePlan Scan(Installation installation, CancellationToken token = default)
    {
        _ = Installation.FromPath(installation.AppDirectory);
        var data = Path.Combine(installation.AppDirectory, "data");
        Safety.EnsureNoLinks(data);
        var files = new List<CacheFile>();
        var directories = new List<string>();
        var roots = new List<string>();
        foreach (var name in Names)
        {
            var root = Path.Combine(data, name);
            Safety.EnsureBelow(root, installation.AppDirectory);
            Safety.EnsureNoLinks(root);
            if (!Directory.Exists(root)) continue;
            roots.Add(root);
            ScanDirectory(root, root, files, directories, token);
        }
        return new CachePlan(installation.AppDirectory, files.AsReadOnly(), directories.AsReadOnly(), roots.AsReadOnly());
    }

    private static void ScanDirectory(string directory, string root, List<CacheFile> files, List<string> directories, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Safety.EnsureNoLinks(directory);
        foreach (var path in Directory.EnumerateFileSystemEntries(directory))
        {
            token.ThrowIfCancellationRequested();
            Safety.EnsureBelow(path, root);
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Hay un enlace dentro de la caché. La limpieza se bloqueó para proteger otras carpetas.");
            if ((attributes & FileAttributes.Directory) != 0)
            {
                // Preserve downloaded game storage even if an older client places it under cache.
                if (Path.GetFileName(path).Equals("game", StringComparison.OrdinalIgnoreCase)) continue;
                ScanDirectory(path, root, files, directories, token);
                directories.Add(path);
            }
            else files.Add(new CacheFile(path, root, new FileInfo(path).Length));
        }
    }

    public static void Preflight(CachePlan plan, Action ensureGameClosed, CancellationToken token)
    {
        ValidatePlan(plan);
        ensureGameClosed();
        foreach (var file in plan.Files)
        {
            token.ThrowIfCancellationRequested();
            EnsureAuthorized(file);
            if (!File.Exists(file.Path)) continue;
            if ((File.GetAttributes(file.Path) & FileAttributes.ReadOnly) != 0)
                throw new IOException("Un archivo de caché es de solo lectura. No se inició la limpieza.");
            try
            {
                using var stream = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (IOException)
            {
                throw new IOException("Un archivo de caché está en uso. Cierra FiveM y vuelve a intentarlo.");
            }
        }
        ensureGameClosed();
    }

    public static CleanupResult Clean(CachePlan plan, Action ensureGameClosed, IProgress<string>? progress, CancellationToken token, IProgress<RepairProgress>? stages = null)
    {
        var count = 0;
        long bytes = 0;
        var processed = 0;
        var total = plan.Files.Count + plan.Directories.Count;
        void ReportEntry()
        {
            processed++;
            if (processed % 100 == 0 || processed == total)
                stages?.Report(new(RepairStage.Cleaning, total == 0 ? 1 : processed / (double)total));
        }
        try
        {
            ValidatePlan(plan);
            stages?.Report(new(RepairStage.Cleaning, total == 0 ? 1 : 0));
            foreach (var file in plan.Files)
            {
                token.ThrowIfCancellationRequested();
                ensureGameClosed();
                EnsureAuthorized(file);
                if (!File.Exists(file.Path)) { ReportEntry(); continue; }
                File.Delete(file.Path);
                count++;
                bytes += file.Length;
                ReportEntry();
                if (count % 100 == 0) progress?.Report($"Limpiando caché: {count:N0} de {plan.Files.Count:N0} archivos…");
            }
            foreach (var directory in plan.Directories)
            {
                token.ThrowIfCancellationRequested();
                ensureGameClosed();
                var root = plan.Roots.First(r => Safety.FullPath(directory).StartsWith(Safety.FullPath(r) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
                Safety.EnsureBelow(directory, root);
                Safety.EnsureNoLinks(directory);
                if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                    Directory.Delete(directory, false);
                ReportEntry();
            }
            return new CleanupResult(count, bytes, plan.Files.Count, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            return new CleanupResult(count, bytes, plan.Files.Count,
                exception is OperationCanceledException ? "Limpieza interrumpida." : exception.Message);
        }
    }

    private static void EnsureAuthorized(CacheFile file)
    {
        Safety.EnsureBelow(file.Path, file.Root);
        Safety.EnsureNoLinks(file.Path);
        var relative = Path.GetRelativePath(file.Root, file.Path);
        if (relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => part.Equals("game", StringComparison.OrdinalIgnoreCase)))
            throw new IOException("La carpeta de archivos del juego está protegida.");
    }

    private static void ValidatePlan(CachePlan plan)
    {
        var installation = Installation.FromPath(plan.AppDirectory);
        var allowed = Names.Select(name => Safety.FullPath(Path.Combine(installation.AppDirectory, "data", name))).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var root in plan.Roots)
            if (!allowed.Contains(Safety.FullPath(root))) throw new IOException("La limpieza contiene una carpeta no autorizada.");
        foreach (var file in plan.Files)
            if (!allowed.Contains(Safety.FullPath(file.Root))) throw new IOException("La limpieza contiene un archivo fuera de las carpetas autorizadas.");
        foreach (var directory in plan.Directories)
            if (!allowed.Any(root => Safety.FullPath(directory).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                throw new IOException("La limpieza contiene una subcarpeta no autorizada.");
    }
}
