using System.Text.Json;

namespace ChilePrimeRepair.Core;

public sealed record Diagnosis(Installation Installation, CachePlan Cache, bool NuiEnabled, bool GameRunning);
public sealed record RepairOptions(bool CleanCache, bool FixNui);
public sealed record RepairResult(bool Success, CleanupResult? Cache, bool ConfigurationChanged, string Message);

public sealed class RepairService(string configPath, string stateDirectory, Func<bool> gameRunning)
{
    private sealed class Journal
    {
        public int Format { get; set; } = 1;
        public string ConfigPath { get; set; } = "";
        public string? BeforeHash { get; set; }
        public string AfterHash { get; set; } = "";
        public string[] BeforeLines { get; set; } = [];
        public string[] AfterLines { get; set; } = [];
        public string State { get; set; } = "prepared";
        public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string ConfigPath { get; } = Safety.FullPath(configPath);
    public string StateDirectory { get; } = Safety.FullPath(stateDirectory);

    public void EnsureGameClosed()
    {
        if (gameRunning()) throw new IOException("FiveM está abierto. Ciérralo por completo antes de reparar o restaurar.");
    }

    public Diagnosis Diagnose(string installationPath, CancellationToken token = default)
    {
        var installation = Installation.FromPath(installationPath);
        Safety.EnsureNoLinks(ConfigPath);
        var bytes = File.Exists(ConfigPath) ? File.ReadAllBytes(ConfigPath) : [];
        var config = new Configuration(bytes);
        return new Diagnosis(installation, CacheCleaner.Scan(installation, token), config.IsEnabled, gameRunning());
    }

    public RepairResult Repair(string installationPath, RepairOptions options, IProgress<string>? progress = null, CancellationToken token = default, IProgress<RepairProgress>? stages = null)
    {
        if (!options.CleanCache && !options.FixNui)
            throw new IOException("Selecciona al menos una reparación.");
        token.ThrowIfCancellationRequested();
        EnsureGameClosed();
        stages?.Report(new(RepairStage.Checking));
        progress?.Report("Comprobando instalación y configuración…");
        var installation = Installation.FromPath(installationPath);
        Safety.EnsureNoLinks(ConfigPath);
        var beforeExists = File.Exists(ConfigPath);
        var before = beforeExists ? File.ReadAllBytes(ConfigPath) : [];
        var config = new Configuration(before);
        var after = options.FixNui ? config.Enable() : before;
        var change = options.FixNui && !before.AsSpan().SequenceEqual(after);
        var expected = beforeExists ? Safety.Hash(before) : null;
        if (change && beforeExists)
        {
            if ((File.GetAttributes(ConfigPath) & FileAttributes.ReadOnly) != 0)
                throw new IOException("La configuración es de solo lectura. No se inició la reparación.");
            using var writeCheck = new FileStream(ConfigPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        // Validate all candidate paths and locks before either requested mutation starts.
        stages?.Report(new(RepairStage.Scanning));
        var plan = options.CleanCache ? CacheCleaner.Scan(installation, token) : null;
        if (plan is not null) CacheCleaner.Preflight(plan, EnsureGameClosed, token);
        string? journalDirectory = null;
        Journal? journal = null;
        if (change)
        {
            stages?.Report(new(RepairStage.Backup));
            Safety.EnsureNoLinks(StateDirectory);
            journalDirectory = Path.Combine(StateDirectory, "backups", DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ") + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(journalDirectory);
            if (beforeExists) File.WriteAllBytes(Path.Combine(journalDirectory, "original.cfg"), before);
            journal = new Journal
            {
                ConfigPath = ConfigPath, BeforeHash = expected, AfterHash = Safety.Hash(after),
                BeforeLines = config.SettingLines, AfterLines = new Configuration(after).SettingLines
            };
            WriteJournal(journalDirectory, journal);
            progress?.Report("Configuración anterior respaldada.");
        }

        CleanupResult? cleanup = null;
        if (plan is not null)
        {
            progress?.Report("Limpiando la caché del cliente…");
            cleanup = CacheCleaner.Clean(plan, EnsureGameClosed, progress, token, stages);
            if (!cleanup.Success)
                return new RepairResult(false, cleanup, false,
                    $"Limpieza incompleta: {cleanup.DeletedFiles:N0} de {cleanup.PlannedFiles:N0} archivos. {cleanup.Error} El ajuste NUI no se modificó.");
            progress?.Report($"Caché limpia: {cleanup.DeletedFiles:N0} archivos eliminados.");
        }
        if (options.FixNui) stages?.Report(new(RepairStage.Interfaces));
        if (change)
        {
            token.ThrowIfCancellationRequested();
            EnsureGameClosed();
            try
            {
                Safety.AtomicWrite(ConfigPath, after, expected);
                journal!.State = "applied";
                WriteJournal(journalDirectory!, journal);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                var applied = Safety.FileHash(ConfigPath) == journal!.AfterHash;
                return new RepairResult(false, cleanup, applied,
                    "La operación requiere revisión: " + exception.Message + (applied ? " El ajuste NUI quedó activado y su respaldo está disponible." : " El ajuste NUI no se modificó."));
            }
            progress?.Report("Interfaces NUI fijadas a 1920 × 1080.");
        }
        else if (options.FixNui) progress?.Report("El tamaño fijo de NUI ya estaba activado.");
        token.ThrowIfCancellationRequested();
        stages?.Report(new(RepairStage.Completed, 1));
        return new RepairResult(true, cleanup, change,
            "Reparación aplicada. Abre FiveM y prueba las interfaces y la carga del mapa dentro de Chile Prime.");
    }

    public bool CanRestore()
    {
        try
        {
            var latest = LatestJournal();
            return latest is not null && latest.Value.Journal.State is "prepared" or "applied";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException) { return false; }
    }

    public string Restore()
    {
        EnsureGameClosed();
        var latest = LatestJournal() ?? throw new IOException("Todavía no hay un ajuste anterior para restaurar.");
        var journal = latest.Journal;
        if (journal.State == "restored") throw new IOException("El ajuste anterior ya fue restaurado.");
        if (journal.Format != 1 || !Safety.FullPath(journal.ConfigPath).Equals(ConfigPath, StringComparison.OrdinalIgnoreCase))
            throw new IOException("El respaldo no corresponde a esta configuración.");
        Safety.EnsureNoLinks(ConfigPath);
        if (!File.Exists(ConfigPath)) throw new IOException("La configuración actual no existe. Se conservó el respaldo para revisión.");
        var current = File.ReadAllBytes(ConfigPath);
        var currentHash = Safety.Hash(current);
        var currentDocument = new Configuration(current);
        // FiveM can rewrite formatting and coalesce duplicate settings when it saves preferences.
        // Compare the setting's value so a normal game session does not disable restoration.
        if (!currentDocument.IsEnabled)
            throw new IOException("El ajuste NUI cambió después de la reparación. Se conservaron tus cambios y el respaldo.");
        byte[]? original = null;
        if (journal.BeforeHash is not null)
        {
            var backup = Path.Combine(latest.Directory, "original.cfg");
            Safety.EnsureBelow(backup, StateDirectory);
            Safety.EnsureNoLinks(backup);
            original = File.ReadAllBytes(backup);
            if (Safety.Hash(original) != journal.BeforeHash)
                throw new IOException("El respaldo fue modificado o está incompleto. La restauración se bloqueó.");
            if (!new Configuration(original).SettingLines.SequenceEqual(journal.BeforeLines))
                throw new IOException("El respaldo no coincide con los valores registrados.");
        }
        EnsureGameClosed();
        if (currentHash == journal.AfterHash && journal.BeforeHash is null)
        {
            if (Safety.FileHash(ConfigPath) != currentHash) throw new IOException("La configuración cambió. Vuelve a intentarlo.");
            File.Delete(ConfigPath);
        }
        else
        {
            var restored = currentHash == journal.AfterHash ? original! : currentDocument.RestoreSetting(journal.BeforeLines);
            Safety.AtomicWrite(ConfigPath, restored, currentHash);
        }
        journal.State = "restored";
        WriteJournal(latest.Directory, journal);
        return "Ajuste NUI anterior restaurado. La caché se volverá a generar al usar FiveM.";
    }

    private (string Directory, Journal Journal)? LatestJournal()
    {
        var backups = Path.Combine(StateDirectory, "backups");
        Safety.EnsureNoLinks(backups);
        if (!Directory.Exists(backups)) return null;
        foreach (var directory in Directory.EnumerateDirectories(backups).OrderByDescending(Path.GetFileName, StringComparer.Ordinal))
        {
            Safety.EnsureBelow(directory, StateDirectory);
            Safety.EnsureNoLinks(directory);
            var path = Path.Combine(directory, "manifest.json");
            Safety.EnsureNoLinks(path);
            if (!File.Exists(path)) continue;
            var journal = JsonSerializer.Deserialize<Journal>(File.ReadAllText(path)) ?? throw new IOException("No se pudo leer el respaldo.");
            if (!journal.ConfigPath.Equals(ConfigPath, StringComparison.OrdinalIgnoreCase)) continue;
            // A prepared journal can recover a crash between writing the configuration and recording success.
            if (journal.State == "prepared" && Safety.FileHash(ConfigPath) != journal.AfterHash) continue;
            return (directory, journal);
        }
        return null;
    }

    private static void WriteJournal(string directory, Journal journal)
    {
        var path = Path.Combine(directory, "manifest.json");
        Safety.AtomicWrite(path, JsonSerializer.SerializeToUtf8Bytes(journal, JsonOptions), Safety.FileHash(path));
    }
}
