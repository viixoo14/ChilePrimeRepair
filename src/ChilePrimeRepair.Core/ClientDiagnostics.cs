using System.Globalization;
using System.Text;

namespace ChilePrimeRepair.Core;

public sealed record GraphicsDevice(string Name, ulong DedicatedBytes, ulong SharedBytes);
public sealed record DeviceSnapshot(ulong? TotalRam, ulong? AvailableRam, long? FreeDisk, GraphicsDevice[] Graphics, bool GraphicsKnown, int? GpuPreference = null)
{
    public static DeviceSnapshot Unknown => new(null, null, null, [], false);
}
public sealed record LogSignals(int FilesRead, int Skipped, bool MemoryPressure, bool NuiGpuFailure, bool DownloadFailure, bool CollisionFailure);
public sealed record DiagnosticPlan(string? ConfigHash, DeviceSnapshot Device, LogSignals Logs, string Channel,
    Dictionary<string, string> Settings, string[] Findings, string[] Recommendations)
{
    public bool ReviewGraphics => Device.Graphics.Length > 1;
    public string Notice => "Se limpiará la caché de FiveM y se fijarán las interfaces a 1920 × 1080. " +
        (Settings.ContainsKey("str_maxVehicleTextureRes") ? "Se limitarán las texturas de vehículos a 1024. " : "") +
        (Settings.ContainsKey("nui_useInProcessGpu") ? "Se probará el modo de compatibilidad NUI; puede reducir su fluidez. " : "") +
        "La caché se volverá a descargar. Puedes cerrar para cancelar.";

    public string Report(int? deletedFiles = null)
    {
        var lines = new List<string> { "CHILE PRIME REPAIR · DIAGNÓSTICO LOCAL", "Fecha: " + DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm zzz"), "", "Equipo" };
        lines.Add("RAM total: " + Size(Device.TotalRam) + "; disponible con el juego cerrado: " + Size(Device.AvailableRam));
        lines.Add("Espacio disponible en la unidad de FiveM: " + (Device.FreeDisk is { } disk ? Size((ulong)Math.Max(0, disk)) : "no disponible"));
        lines.Add(Device.GraphicsKnown ? "Adaptadores gráficos detectados: " + Device.Graphics.Length : "No se pudo consultar la información gráfica.");
        foreach (var gpu in Device.Graphics)
            lines.Add("• " + Clean(gpu.Name) + ": memoria dedicada " + Size(gpu.DedicatedBytes) + ", memoria compartida máxima " + Size(gpu.SharedBytes));
        lines.Add("Estos datos no miden el consumo de FiveM durante una partida ni confirman qué GPU está utilizando.");
        lines.AddRange(["", "Diagnóstico", "Canal de FiveM: " + Channel, $"Registros recientes analizados: {Logs.FilesRead}; omitidos o ilegibles: {Logs.Skipped}."]);
        lines.AddRange(Findings.Select(f => "• " + f));
        if (deletedFiles is not null)
        {
            lines.AddRange(["", "Cambios aplicados", $"• Caché: {deletedFiles} archivos eliminados.", "• Interfaces NUI: tamaño fijo a 1920 × 1080."]);
            foreach (var (key, value) in Settings) lines.Add("• " + key + " = " + value + " (prueba de reparación según los registros).");
        }
        lines.AddRange(["", "Recomendaciones", .. Recommendations.Select(r => "• " + r), "", "Si vuelves a caer del mapa, anota el lugar y si afecta a otros jugadores. Una coincidencia justificaría revisar los recursos y colisiones del servidor; este asistente no modifica esos recursos.", "Una reparación completada no demuestra una mejora de FPS ni confirma que el problema dentro del servidor esté resuelto.", "El informe no incluye los registros originales, direcciones de red, rutas del usuario ni identificadores de hardware. No se envía automáticamente."]);
        return string.Join(Environment.NewLine, lines);
    }
    private static string Size(ulong? bytes) => bytes is null ? "no disponible" : (bytes.Value / 1073741824d).ToString("0.0", CultureInfo.InvariantCulture) + " GiB";
    private static string Clean(string text) => new(text.Where(c => !char.IsControl(c)).Take(128).ToArray());
}

public static class ClientDiagnostics
{
    public static DiagnosticPlan Analyze(string installation, string configPath, DeviceSnapshot device, CancellationToken token = default)
    {
        var app = Installation.FromPath(installation).AppDirectory;
        Safety.EnsureNoLinks(configPath);
        if (File.Exists(configPath) && new FileInfo(configPath).Length > 2 * 1024 * 1024)
            throw new IOException("La configuración es demasiado grande para analizarla con seguridad.");
        var bytes = File.Exists(configPath) ? File.ReadAllBytes(configPath) : [];
        var logs = ReadLogs(app, token);
        var settings = new Dictionary<string, string>(StringComparer.Ordinal);
        var findings = new List<string>();
        var recommendations = new List<string>();
        if (logs.MemoryPressure) findings.Add("Los registros contienen indicios de falta de memoria; no identifican por sí solos si falta RAM o VRAM.");
        if (logs.NuiGpuFailure) findings.Add("Se detectaron fallos del proceso gráfico de CEF/NUI.");
        if (logs.DownloadFailure) recommendations.Add("Hay fallos de descarga: comprueba la conexión y vuelve a probar. Si persisten, soporte debe revisar la descarga del recurso y el servidor.");
        if (logs.CollisionFailure) recommendations.Add("Hay errores de carga de colisiones o mapas. Comprueba si ocurren en el mismo lugar a varios jugadores y solicita revisión del recurso del servidor.");
        if (logs.FilesRead == 0) findings.Add("No hay registros recientes legibles. No se pudo determinar la causa del problema.");
        else if (!logs.MemoryPressure && !logs.NuiGpuFailure && !logs.DownloadFailure && !logs.CollisionFailure)
            findings.Add("No se encontraron señales reconocidas en la parte revisada de los registros. El diagnóstico de la causa es inconcluso.");
        if (device.AvailableRam is < 2UL * 1024 * 1024 * 1024) recommendations.Add("Queda poca RAM disponible antes de abrir el juego. Cierra aplicaciones que no necesites y vuelve a medir durante la partida.");
        if (device.FreeDisk is < 5L * 1024 * 1024 * 1024) recommendations.Add("Queda poco espacio en la unidad de FiveM. Libera espacio para permitir nuevas descargas de caché; el espacio necesario depende del servidor.");
        if (device.Graphics.Length > 1)
            recommendations.Add(device.GpuPreference == 1 ? "Hay varios adaptadores y una preferencia de ahorro de energía para el proceso del juego. Revisa Gráficos de Windows y selecciona Alto rendimiento para ese ejecutable." :
                "Hay varios adaptadores. Revisa en Gráficos de Windows la preferencia de FiveM y sus procesos de GTA; el adaptador activo debe comprobarse durante una partida.");
        string? Value(string key)
        {
            try { return new Configuration(bytes, key).Value; }
            catch (IOException) { recommendations.Add("El ajuste " + key + " tiene formato inesperado. Se conservará para revisión."); return null; }
        }
        var texture = Value("str_maxVehicleTextureRes");
        if (logs.MemoryPressure && int.TryParse(texture, out var resolution) && resolution > 1024)
        {
            settings["str_maxVehicleTextureRes"] = "1024";
            findings.Add("El límite de texturas de vehículos supera 1024 y coincide con errores de memoria. Se probará el valor estándar de 1024; puede reducir la calidad visual.");
        }
        var nui = Value("nui_useInProcessGpu");
        if (logs.NuiGpuFailure && !logs.MemoryPressure && (nui is null || nui.Equals("false", StringComparison.OrdinalIgnoreCase) || nui == "0"))
        {
            // A malformed key must not be silently replaced after Value refused it.
            try
            {
                _ = new Configuration(bytes, "nui_useInProcessGpu");
                settings["nui_useInProcessGpu"] = "true";
                recommendations.Add("Se probará compatibilidad NUI. Reinicia FiveM y compara las interfaces; si empeoran o pierden fluidez, deshaz los ajustes.");
            }
            catch (IOException) { }
        }
        var budget = Value("vid_budgetScale");
        findings.Add("Presupuesto extendido de texturas: " + (budget is null ? "valor predeterminado o no disponible" : int.TryParse(budget, out var n) ? n.ToString(CultureInfo.InvariantCulture) : "valor no numérico; requiere revisión") + ".");
        if (logs.MemoryPressure || logs.CollisionFailure)
            recommendations.Add("Prueba un presupuesto extendido de texturas junto con una calidad de texturas y distancia extendida acordes a tu GPU. Observa la memoria durante una partida y cambia una opción cada vez. El modelo de GPU y su VRAM no permiten calcular un valor óptimo universal; el asistente conserva este ajuste.");
        var channel = ReadChannel(app);
        if (channel is "beta" or "canary") recommendations.Add("Estás usando un canal experimental. En los ajustes de FiveM, cambia al canal Release/Production y reinicia si buscas estabilidad.");
        if (channel == "no disponible") recommendations.Add("No se pudo determinar el canal de actualización. Revísalo en los ajustes de FiveM.");
        if (recommendations.Count == 0) recommendations.Add("Abre FiveM, prueba Chile Prime y compara el comportamiento. Si persiste, comparte este informe con soporte y describe el síntoma y el lugar.");
        return new(bytes.Length == 0 && !File.Exists(configPath) ? null : Safety.Hash(bytes), device, logs, channel, settings, findings.ToArray(), recommendations.ToArray());
    }

    public static LogSignals Classify(IEnumerable<string> lines, int filesRead = 1, int skipped = 0)
    {
        bool memory = false, nui = false, download = false, collision = false;
        foreach (var raw in lines)
        {
            var line = raw[..Math.Min(raw.Length, 4096)].ToLowerInvariant();
            if (line.Contains("no errors") || line.Contains("0 errors") || line.Contains("error=false")) continue;
            memory |= line.Contains("out of memory") || line.Contains("out-of-memory") || line.Contains("err_mem_") || line.Contains("memory allocation failed");
            nui |= (line.Contains("gpu process") || line.Contains("gpu-process")) &&
                (line.Contains("cef") || line.Contains("chromium") || line.Contains("nui")) &&
                (line.Contains("launch failed") || line.Contains("exited unexpectedly") || line.Contains("crashed"));
            download |= (line.Contains("curl error") || line.Contains("download failed") || line.Contains("failed to download"));
            collision |= (line.Contains("collision") || line.Contains(".ybn")) &&
                (line.Contains("failed to load") || line.Contains("could not load") || line.Contains("error loading"));
        }
        return new(filesRead, skipped, memory, nui, download, collision);
    }

    private static LogSignals ReadLogs(string app, CancellationToken token)
    {
        var rows = new List<string>(); int read = 0, skipped = 0;
        var logs = Path.Combine(app, "logs");
        try
        {
            Safety.EnsureNoLinks(logs);
            if (!Directory.Exists(logs)) return Classify([], 0);
            // Bounded enumeration and reading. Never traverse resource folders or follow links.
            foreach (var file in Directory.EnumerateFiles(logs, "CitizenFX_log*.log").Take(128)
                .Select(p => new FileInfo(p)).Where(f => f.LastWriteTimeUtc >= DateTime.UtcNow.AddDays(-7))
                .OrderByDescending(f => f.LastWriteTimeUtc).Take(3))
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    Safety.EnsureBelow(file.FullName, app); Safety.EnsureNoLinks(file.FullName);
                    using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    var offset = Math.Max(0, stream.Length - 256 * 1024);
                    stream.Seek(offset, SeekOrigin.Begin);
                    var buffer = new byte[(int)Math.Min(256 * 1024, stream.Length - offset)];
                    var count = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
                    using var reader = new StreamReader(new MemoryStream(buffer, 0, count), Encoding.UTF8, true);
                    if (offset > 0) _ = reader.ReadLine();
                    while (reader.ReadLine() is { } line && rows.Count < 12000) { token.ThrowIfCancellationRequested(); rows.Add(line); }
                    read++;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { skipped++; }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { skipped++; }
        return Classify(rows, read, skipped);
    }

    private static string ReadChannel(string app)
    {
        try
        {
            var path = Path.Combine(app, "CitizenFX.ini"); Safety.EnsureNoLinks(path);
            if (new FileInfo(path).Length > 64 * 1024) return "no disponible";
            string section = ""; var values = new List<string>();
            foreach (var raw in File.ReadLines(path))
            {
                var line = raw.Trim();
                if (line.StartsWith('[')) { section = line.Trim('[', ']'); continue; }
                if (!section.Equals("Game", StringComparison.OrdinalIgnoreCase)) continue;
                var parts = line.Split('=', 2);
                if (parts.Length == 2 && parts[0].Trim().Equals("UpdateChannel", StringComparison.OrdinalIgnoreCase)) values.Add(parts[1].Split(';', '#')[0].Trim().ToLowerInvariant());
            }
            return values.Count == 0 ? "production" : values.Distinct().Count() == 1 && values[0] is "production" or "beta" or "canary" ? values[0] : "no disponible";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return "no disponible"; }
    }
}
