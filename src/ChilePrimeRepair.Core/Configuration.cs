using System.Text;
using System.Text.RegularExpressions;

namespace ChilePrimeRepair.Core;

public sealed class Configuration
{
    private sealed record Line(string Content, string Ending);
    public static readonly string[] SupportedKeys = ["nui_useFixedSize", "nui_useInProcessGpu", "str_maxVehicleTextureRes", "vid_budgetScale"];
    private readonly Regex Setting;
    private readonly string key;
    private readonly Encoding encoding;
    private readonly byte[] preamble;
    private readonly List<Line> lines;
    private readonly string newline;
    public byte[] OriginalBytes { get; }
    public string[] SettingLines => lines.Where(l => IsSetting(l.Content)).Select(l => l.Content).ToArray();
    public bool IsEnabled => SettingLines.Length > 0 && SettingLines.All(l =>
    {
        var value = Setting.Match(l).Groups["value"].Value.Trim('"');
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
    });

    public Configuration(byte[] bytes, string settingKey = "nui_useFixedSize")
    {
        if (!SupportedKeys.Contains(settingKey, StringComparer.Ordinal)) throw new IOException("Ajuste no autorizado.");
        key = settingKey;
        Setting = new Regex("^\\s*(?:seta|set)\\s+(?:\"" + Regex.Escape(key) + "\"|" + Regex.Escape(key) + ")\\s+(?<value>\"[^\"]*\"|[^\\s;\"]+)(?<tail>.*)$", RegexOptions.CultureInvariant);
        OriginalBytes = bytes;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        if (bytes.AsSpan().StartsWith(new byte[] { 0xff, 0xfe, 0, 0 })) encoding = new UTF32Encoding(false, true, true);
        else if (bytes.AsSpan().StartsWith(new byte[] { 0, 0, 0xfe, 0xff })) encoding = new UTF32Encoding(true, true, true);
        else if (bytes.AsSpan().StartsWith(new byte[] { 0xff, 0xfe })) encoding = new UnicodeEncoding(false, true, true);
        else if (bytes.AsSpan().StartsWith(new byte[] { 0xfe, 0xff })) encoding = new UnicodeEncoding(true, true, true);
        else if (bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf })) encoding = new UTF8Encoding(true, true);
        else
        {
            encoding = new UTF8Encoding(false, true);
            try { _ = encoding.GetString(bytes); }
            catch (DecoderFallbackException) { encoding = Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback); }
        }
        preamble = encoding.GetPreamble();
        var text = encoding.GetString(bytes, preamble.Length, bytes.Length - preamble.Length);
        lines = [];
        foreach (Match match in Regex.Matches(text, "([^\\r\\n]*)(\\r\\n|\\n|\\r|$)"))
        {
            if (match.Length == 0) continue;
            lines.Add(new Line(match.Groups[1].Value, match.Groups[2].Value));
        }
        newline = lines.FirstOrDefault(l => l.Ending.Length > 0)?.Ending ?? "\r\n";
        foreach (var line in lines.Where(l => IsSetting(l.Content)))
            if (Setting.Match(line.Content).Groups["tail"].Value is { } tail &&
                (tail.Contains(';') || tail.Trim().Length > 0 && !tail.TrimStart().StartsWith("//") && !tail.TrimStart().StartsWith('#')))
                throw new IOException("El ajuste NUI comparte línea con otros comandos. Se requiere revisarlo antes de modificarlo.");
        // Refuse malformed lines for this key instead of silently appending another value.
        foreach (var line in lines)
            if (Regex.IsMatch(line.Content, "^\\s*(?:seta|set)\\s+\"?" + Regex.Escape(key) + "\"?(?:\\s|$)") && !IsSetting(line.Content))
                throw new IOException("El ajuste NUI tiene un formato inesperado. La configuración se conservó.");
    }

    private bool IsSetting(string content) => Setting.IsMatch(content);
    public string? Value => SettingLines.LastOrDefault() is { } line ? Setting.Match(line).Groups["value"].Value.Trim('"') : null;
    public bool HasValue(string expected) => SettingLines.Length > 0 && SettingLines.All(l =>
    {
        var value = Setting.Match(l).Groups["value"].Value.Trim('"');
        return value.Equals(expected, StringComparison.OrdinalIgnoreCase) ||
            (expected == "true" && value == "1") || (expected == "false" && value == "0");
    });

    public byte[] Enable() => SetValue("true");

    public byte[] SetValue(string desired)
    {
        if (key.StartsWith("nui_", StringComparison.Ordinal) ? desired is not ("true" or "false") :
            !int.TryParse(desired, out var numeric) || numeric < 0 || numeric > 16384)
            throw new IOException("Valor de ajuste no autorizado.");
        if (HasValue(desired)) return OriginalBytes;
        var modified = lines.ToList();
        var found = false;
        for (var i = 0; i < modified.Count; i++)
        {
            var match = Setting.Match(modified[i].Content);
            if (!match.Success) continue;
            found = true;
            var value = match.Groups["value"];
            var content = modified[i].Content[..value.Index] + '"' + desired + '"' + modified[i].Content[(value.Index + value.Length)..];
            modified[i] = modified[i] with { Content = content };
        }
        if (!found)
        {
            if (modified.Count > 0 && modified[^1].Ending.Length == 0)
                modified[^1] = modified[^1] with { Ending = newline };
            modified.Add(new Line("seta \"" + key + "\" \"" + desired + "\"", newline));
        }
        return Encode(modified);
    }

    public byte[] RestoreSetting(string[] originals)
    {
        var modified = new List<Line>();
        var inserted = false;
        foreach (var line in lines)
        {
            if (!IsSetting(line.Content)) { modified.Add(line); continue; }
            if (!inserted)
            {
                modified.AddRange(originals.Select(content => new Line(content, line.Ending.Length > 0 ? line.Ending : newline)));
                inserted = true;
            }
        }
        return Encode(modified);
    }

    private byte[] Encode(IEnumerable<Line> contents)
    {
        var text = string.Concat(contents.Select(l => l.Content + l.Ending));
        return [.. preamble, .. encoding.GetBytes(text)];
    }
}
