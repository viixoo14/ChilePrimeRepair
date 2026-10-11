using System.Text;
using System.Diagnostics;
using ChilePrimeRepair.Core;

var testRoot = Path.GetFullPath(args.FirstOrDefault() ?? Path.Combine(Path.GetTempPath(), "ChilePrimeRepair-tests"));
Directory.CreateDirectory(testRoot);
var passed = 0;
var failed = 0;
void Test(string name, Action test)
{
    try { test(); Console.WriteLine("PASS " + name); passed++; }
    catch (Exception exception) { Console.WriteLine("FAIL " + name + ": " + exception); failed++; }
}
Fixture NewFixture() => new(testRoot);
void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}; got {actual}"); }
void True(bool value) { if (!value) throw new Exception("Expected true"); }
void Throws(Action action)
{
    try { action(); } catch (IOException) { return; }
    throw new Exception("Expected IOException");
}

Test("UTF-8 BOM, CRLF, Unicode and other preferences survive", () =>
{
    var text = "// configuración\r\nbind keyboard F2 \"inventario\"\r\nseta \"nui_useFixedSize\" \"false\" // tamaño\r\nseta \"nui_useInProcessGpu\" \"false\"\r\n";
    var enc = new UTF8Encoding(true);
    byte[] before = [.. enc.GetPreamble(), .. enc.GetBytes(text)];
    var after = new Configuration(before).Enable();
    Equal(text.Replace("\"false\" // tamaño", "\"true\" // tamaño"), enc.GetString(after[3..]));
    True(after.AsSpan().StartsWith(enc.GetPreamble()));
});
Test("UTF-16 and UTF-32 encoding survives", () =>
{
    foreach (var enc in new Encoding[] { new UnicodeEncoding(false, true), new UnicodeEncoding(true, true), new UTF32Encoding(false, true) })
    {
        byte[] before = [.. enc.GetPreamble(), .. enc.GetBytes("seta nui_useFixedSize false\n// niño\n")];
        var after = new Configuration(before).Enable();
        True(after.AsSpan().StartsWith(enc.GetPreamble()));
        Equal("seta nui_useFixedSize \"true\"\n// niño\n", enc.GetString(after[enc.GetPreamble().Length..]));
    }
});
Test("Windows-1252 preferences survive", () =>
{
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    var enc = Encoding.GetEncoding(1252);
    var before = enc.GetBytes("// configuración\r\nseta nui_useFixedSize false\r\n");
    Equal("// configuración\r\nseta nui_useFixedSize \"true\"\r\n", enc.GetString(new Configuration(before).Enable()));
});
Test("Comments are not settings; missing setting is appended", () =>
{
    var config = new Configuration(Encoding.UTF8.GetBytes("// seta nui_useFixedSize false\nbind keyboard F2 \"inventory\""));
    var text = Encoding.UTF8.GetString(config.Enable());
    Equal("// seta nui_useFixedSize false\nbind keyboard F2 \"inventory\"\nseta \"nui_useFixedSize\" \"true\"\n", text);
});
Test("Duplicate settings are enabled consistently", () =>
{
    var after = new Configuration(Encoding.UTF8.GetBytes("seta nui_useFixedSize false\nset nui_useFixedSize 0\n")).Enable();
    True(new Configuration(after).IsEnabled);
    Equal(2, new Configuration(after).SettingLines.Length);
});
Test("Malformed and multiple-command setting lines refuse modification", () =>
{
    Throws(() => new Configuration(Encoding.UTF8.GetBytes("seta nui_useFixedSize\n")));
    Throws(() => new Configuration(Encoding.UTF8.GetBytes("seta nui_useFixedSize false; seta other true\n")));
});
Test("Repair cleans only allowed caches, preserves game and persistent NUI data", () =>
{
    var f = NewFixture();
    var before = File.ReadAllBytes(f.Config);
    var result = f.Service().Repair(f.App, new(true, true));
    True(result.Success); Equal(3, result.Cache!.DeletedFiles);
    True(File.Exists(f.Game)); True(File.Exists(f.PersistentNui)); True(File.Exists(f.ProtectedNestedGame));
    True(File.Exists(f.Other)); True(File.Exists(Path.Combine(f.App, "CitizenFX.ini")));
    True(new Configuration(File.ReadAllBytes(f.Config)).IsEnabled);
    True(f.Service().CanRestore());
    f.Service().Restore();
    True(File.ReadAllBytes(f.Config).AsSpan().SequenceEqual(before));
    True(!f.Service().CanRestore());
});
Test("Invalid installation rejects all writes", () =>
{
    var f = NewFixture();
    var before = File.ReadAllBytes(f.Config);
    Throws(() => f.Service().Repair(f.Root, new(true, true)));
    True(File.Exists(f.CacheFiles[0])); True(!Directory.Exists(f.State));
    True(File.ReadAllBytes(f.Config).AsSpan().SequenceEqual(before));
});
Test("An open game blocks cleaning and changing configuration", () =>
{
    var f = NewFixture();
    Throws(() => f.Service(() => true).Repair(f.App, new(true, true)));
    True(File.Exists(f.CacheFiles[0])); True(!Directory.Exists(f.State));
    True(!new Configuration(File.ReadAllBytes(f.Config)).IsEnabled);
});
Test("Read-only cache stops all mutations at preflight", () =>
{
    var f = NewFixture();
    File.SetAttributes(f.CacheFiles[1], FileAttributes.ReadOnly);
    try
    {
        Throws(() => f.Service().Repair(f.App, new(true, true)));
        True(f.CacheFiles.All(File.Exists)); True(!Directory.Exists(f.State));
        True(!new Configuration(File.ReadAllBytes(f.Config)).IsEnabled);
    }
    finally { File.SetAttributes(f.CacheFiles[1], FileAttributes.Normal); }
});
Test("Locked cache stops all mutations at preflight", () =>
{
    var f = NewFixture();
    using var locked = new FileStream(f.CacheFiles[1], FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    Throws(() => f.Service().Repair(f.App, new(true, true)));
    True(f.CacheFiles.All(File.Exists)); True(!Directory.Exists(f.State));
});
Test("Junction inside cache blocks cleanup and preserves outside files", () =>
{
    if (!OperatingSystem.IsWindows()) throw new Exception("This safety test requires Windows");
    var f = NewFixture();
    var outside = Path.Combine(f.Root, "outside"); Directory.CreateDirectory(outside);
    var sentinel = Path.Combine(outside, "keep.txt"); File.WriteAllText(sentinel, "keep");
    var link = Path.Combine(f.App, "data", "cache", "linked");
    var info = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
    info.ArgumentList.Add("/c"); info.ArgumentList.Add("mklink"); info.ArgumentList.Add("/J"); info.ArgumentList.Add(link); info.ArgumentList.Add(outside);
    using var process = Process.Start(info)!; var message = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd(); process.WaitForExit();
    if (process.ExitCode != 0) throw new Exception("Could not create junction fixture: " + message);
    Throws(() => f.Service().Repair(f.App, new(true, true)));
    True(File.Exists(sentinel)); True(f.CacheFiles.All(File.Exists));
    // Fixtures are kept under the test root, so no recursive deletion traverses this junction.
});
Test("Junction at installation root is refused", () =>
{
    var f = NewFixture(); var link = Path.Combine(f.Root, "redirected");
    var info = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var arg in new[] { "/c", "mklink", "/J", link, f.App }) info.ArgumentList.Add(arg);
    using var process = Process.Start(info)!; process.WaitForExit();
    if (process.ExitCode != 0) throw new Exception("Could not create junction fixture");
    Throws(() => Installation.FromPath(link));
});
Test("Restoring merges NUI only and keeps later keybind edits", () =>
{
    var f = NewFixture(); f.Service().Repair(f.App, new(false, true));
    File.AppendAllText(f.Config, "bind keyboard F7 \"new-command\"\r\n");
    f.Service().Restore();
    var text = File.ReadAllText(f.Config);
    True(text.Contains("F7 \"new-command\"")); True(text.Contains("nui_useFixedSize\" \"false\""));
});
Test("A later manual NUI edit is not overwritten by restore", () =>
{
    var f = NewFixture(); f.Service().Repair(f.App, new(false, true));
    var manuallyEdited = File.ReadAllText(f.Config).Replace("\"true\"", "\"false\""); File.WriteAllText(f.Config, manuallyEdited);
    Throws(() => f.Service().Restore()); Equal(manuallyEdited, File.ReadAllText(f.Config));
});
Test("Missing configuration is created, then removed on exact restore", () =>
{
    var f = NewFixture(); File.Delete(f.Config);
    True(f.Service().Repair(f.App, new(false, true)).Success); True(File.Exists(f.Config));
    f.Service().Restore(); True(!File.Exists(f.Config));
});
Test("Missing original config preserves new preferences on restore", () =>
{
    var f = NewFixture(); File.Delete(f.Config); f.Service().Repair(f.App, new(false, true));
    File.AppendAllText(f.Config, "bind keyboard F8 \"new-key\"\r\n");
    f.Service().Restore(); True(File.ReadAllText(f.Config).Contains("new-key"));
    True(!File.ReadAllText(f.Config).Contains("nui_useFixedSize"));
});
Test("Exact restore preserves a file with no final newline", () =>
{
    var f = NewFixture(); File.WriteAllText(f.Config, "bind keyboard F2 \"inventory\"");
    var before = File.ReadAllBytes(f.Config); f.Service().Repair(f.App, new(false, true)); f.Service().Restore();
    True(before.AsSpan().SequenceEqual(File.ReadAllBytes(f.Config)));
});
Test("Already enabled setting is a byte-for-byte no-op", () =>
{
    var f = NewFixture(); File.WriteAllText(f.Config, "seta nui_useFixedSize 1\n");
    var before = File.ReadAllBytes(f.Config); var result = f.Service().Repair(f.App, new(false, true));
    True(result.Success); True(!result.ConfigurationChanged); True(!f.Service().CanRestore());
    True(before.AsSpan().SequenceEqual(File.ReadAllBytes(f.Config)));
});
Test("Repeating repair retains the original restore point", () =>
{
    var f = NewFixture(); var before = File.ReadAllBytes(f.Config);
    f.Service().Repair(f.App, new(true, true)); f.Service().Repair(f.App, new(true, true));
    f.Service().Restore(); True(before.AsSpan().SequenceEqual(File.ReadAllBytes(f.Config)));
});
Test("Cache-only repair does not change NUI", () =>
{
    var f = NewFixture(); var before = File.ReadAllBytes(f.Config);
    True(f.Service().Repair(f.App, new(true, false)).Success);
    True(before.AsSpan().SequenceEqual(File.ReadAllBytes(f.Config))); True(!f.Service().CanRestore());
});
Test("NUI-only repair does not delete cache", () =>
{
    var f = NewFixture(); True(f.Service().Repair(f.App, new(false, true)).Success); True(f.CacheFiles.All(File.Exists));
});
Test("Changed backup fails integrity validation", () =>
{
    var f = NewFixture(); f.Service().Repair(f.App, new(false, true));
    var original = Directory.GetFiles(f.State, "original.cfg", SearchOption.AllDirectories).Single(); File.AppendAllText(original, "tampered");
    var before = File.ReadAllBytes(f.Config); Throws(() => f.Service().Restore());
    True(before.AsSpan().SequenceEqual(File.ReadAllBytes(f.Config)));
});
Test("Atomic write refuses concurrent configuration changes and removes temp", () =>
{
    var f = NewFixture(); var oldHash = Safety.FileHash(f.Config); File.WriteAllText(f.Config, "newer");
    Throws(() => Safety.AtomicWrite(f.Config, Encoding.UTF8.GetBytes("older"), oldHash));
    Equal("newer", File.ReadAllText(f.Config)); Equal(0, Directory.GetFiles(Path.GetDirectoryName(f.Config)!, "*.tmp").Length);
});
Test("Cancellation before repair leaves all data intact", () =>
{
    var f = NewFixture(); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
    try { f.Service().Repair(f.App, new(true, true), token: cancellation.Token); throw new Exception("Expected cancellation"); }
    catch (OperationCanceledException) { }
    True(f.CacheFiles.All(File.Exists)); True(!Directory.Exists(f.State));
});
Test("Game launched during cleaning gives partial result and leaves NUI unchanged", () =>
{
    var f = NewFixture(); var cleaning = false; var checks = 0;
    var progress = new InlineProgress(message => { if (message == "Limpiando la caché del cliente…") cleaning = true; });
    var result = f.Service(() => cleaning && ++checks >= 2).Repair(f.App, new(true, true), progress);
    True(!result.Success); Equal(1, result.Cache!.DeletedFiles); True(!result.ConfigurationChanged);
    True(!new Configuration(File.ReadAllBytes(f.Config)).IsEnabled);
});
Test("Config changed while cleaning is not overwritten", () =>
{
    var f = NewFixture();
    var progress = new InlineProgress(message => { if (message == "Limpiando la caché del cliente…") File.AppendAllText(f.Config, "// later edit\r\n"); });
    var result = f.Service().Repair(f.App, new(true, true), progress);
    True(!result.Success); True(result.Cache!.Success); True(File.ReadAllText(f.Config).Contains("later edit"));
    True(!new Configuration(File.ReadAllBytes(f.Config)).IsEnabled);
});
Test("Path-prefix sibling is outside the allowed root", () =>
{
    var f = NewFixture(); Throws(() => Safety.EnsureBelow(f.App + "-outside\\file", f.App));
});
Test("No actions selected is rejected", () =>
{
    var f = NewFixture(); Throws(() => f.Service().Repair(f.App, new(false, false))); True(f.CacheFiles.All(File.Exists));
});
Test("Read-only configuration prevents cache deletion", () =>
{
    var f = NewFixture(); File.SetAttributes(f.Config, FileAttributes.ReadOnly);
    try { Throws(() => f.Service().Repair(f.App, new(true, true))); True(f.CacheFiles.All(File.Exists)); }
    finally { File.SetAttributes(f.Config, FileAttributes.Normal); }
});
Test("A forged cleanup plan cannot erase another folder", () =>
{
    var f = NewFixture(); var root = Path.Combine(f.Root, "outside"); Directory.CreateDirectory(root);
    var sentinel = Path.Combine(root, "keep.txt"); File.WriteAllText(sentinel, "keep");
    var plan = new CachePlan(f.App, new[] { new CacheFile(sentinel, root, 4) }, Array.Empty<string>(), new[] { root });
    var result = CacheCleaner.Clean(plan, () => { }, null, default);
    True(!result.Success); True(File.Exists(sentinel)); Equal("keep", File.ReadAllText(sentinel));
});
Test("Restore tolerates normal FiveM reformatting of the enabled value", () =>
{
    var f = NewFixture(); f.Service().Repair(f.App, new(false, true));
    File.WriteAllText(f.Config, "seta nui_useFixedSize 1\nbind keyboard F9 \"new-key\"\n");
    f.Service().Restore();
    True(!new Configuration(File.ReadAllBytes(f.Config)).IsEnabled);
    True(File.ReadAllText(f.Config).Contains("new-key"));
});
Test("Measured progress reaches completion after successful repair", () =>
{
    var f = NewFixture(); var events = new List<RepairProgress>();
    var result = f.Service().Repair(f.App, new(true, true), stages: new InlineRepairProgress(events.Add));
    True(result.Success);
    var fractions = events.Where(e => e.Stage == RepairStage.Cleaning).Select(e => e.Fraction!.Value).ToArray();
    Equal(0d, fractions.First()); Equal(1d, fractions.Last());
    True(fractions.All(v => v >= 0 && v <= 1));
    True(fractions.Zip(fractions.Skip(1)).All(pair => pair.First <= pair.Second));
    Equal(RepairStage.Completed, events.Last().Stage);
});
Test("Interrupted repair never announces completed progress", () =>
{
    var f = NewFixture(); var events = new List<RepairProgress>(); var cleaning = false; var checks = 0;
    var log = new InlineProgress(message => { if (message == "Limpiando la caché del cliente…") cleaning = true; });
    var result = f.Service(() => cleaning && ++checks >= 2).Repair(f.App, new(true, true), log,
        stages: new InlineRepairProgress(events.Add));
    True(!result.Success); True(events.All(e => e.Stage != RepairStage.Completed));
    True(events.All(e => e.Stage != RepairStage.Interfaces));
});
Test("Empty cache reports finite progress and completes normally", () =>
{
    var f = NewFixture(); f.Service().Repair(f.App, new(true, true));
    var events = new List<RepairProgress>();
    var result = f.Service().Repair(f.App, new(true, true), stages: new InlineRepairProgress(events.Add));
    True(result.Success); Equal(0, result.Cache!.DeletedFiles);
    True(events.Where(e => e.Fraction.HasValue).All(e => double.IsFinite(e.Fraction!.Value)));
    Equal(RepairStage.Completed, events.Last().Stage);
});
Test("Automatic discovery skips stale paths and finds a custom installation", () =>
{
    var f = NewFixture();
    Equal(f.App, Installation.DetectCandidates([null, "", Path.Combine(f.Root, "not-installed"), Path.Combine(f.Root, "FiveM", "FiveM.exe")]));
    True(f.CacheFiles.All(File.Exists)); True(!Directory.Exists(f.State));
});
Test("Discovery prioritizes the first valid client and does not inspect later sources", () =>
{
    var f = NewFixture(); var second = NewFixture();
    IEnumerable<string> Sources() { yield return f.App; throw new Exception("Discovery inspected an unnecessary source."); }
    Equal(f.App, Installation.DetectCandidates(Sources()));
    Equal(second.App, Installation.DetectCandidates([second.App, f.App]));
});
Test("Missing or invalid discovery hints return no installation without writing", () =>
{
    var f = NewFixture();
    Equal<string?>(null, Installation.DetectCandidates([f.Root, "\0", Path.Combine(f.Root, "other.exe")]));
    True(f.CacheFiles.All(File.Exists)); True(!Directory.Exists(f.State));
});
Test("Cancelled discovery stops before traversing candidates", () =>
{
    using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
    try { Installation.DetectCandidates(["C:\\FiveM"], cancellation.Token); throw new Exception("Expected cancellation"); }
    catch (OperationCanceledException) { }
});
Test("Registered launcher commands support spaces, quoted arguments and icons", () =>
{
    var executable = Path.Combine(testRoot, "Client With Spaces", "FiveM.exe");
    Equal(executable, Installation.LauncherFromCommand('"' + executable + "\" \"%1\""));
    Equal(executable, Installation.LauncherFromCommand('"' + executable + "\",0"));
    Equal(executable, Installation.LauncherFromCommand(executable + " -uninstall app"));
    Equal<string?>(null, Installation.LauncherFromCommand("\"C:\\Windows\\cmd.exe\" /c \"" + executable + "\""));
    Equal<string?>(null, Installation.LauncherFromCommand("C:\\Windows\\cmd.exe /c " + executable));
    Equal<string?>(null, Installation.LauncherFromCommand("FiveM.exe \"%1\""));
});
Test("Common folder discovery finds a nested client without touching its cache", () =>
{
    if (!OperatingSystem.IsWindows()) return;
    var f = NewFixture();
    Equal(f.App, Installation.DetectCandidates(WindowsInstallationDiscovery.CommonFolderCandidates([f.Root])));
    True(f.CacheFiles.All(File.Exists));
});
Test("A real Windows shortcut locates a custom client without changing the shortcut", () =>
{
    if (!OperatingSystem.IsWindows()) return;
    var f = NewFixture(); var folder = Path.Combine(f.Root, "Shortcuts"); Directory.CreateDirectory(folder);
    var shortcut = Path.Combine(folder, "FiveM.lnk");
    object? link = null;
    try
    {
        link = new WindowsShellLink();
        ((IWindowsShellLink)link).SetPath(Path.Combine(f.Root, "FiveM", "FiveM.exe"));
        ((System.Runtime.InteropServices.ComTypes.IPersistFile)link).Save(shortcut, true);
    }
    finally
    {
        if (link is not null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);
    }
    var before = File.ReadAllBytes(shortcut);
    Equal(f.App, Installation.DetectCandidates(WindowsInstallationDiscovery.ShortcutTargets([folder])));
    True(before.AsSpan().SequenceEqual(File.ReadAllBytes(shortcut))); True(f.CacheFiles.All(File.Exists));
});
Console.WriteLine($"\n{passed} passed; {failed} failed. Fixtures: {testRoot}");
return failed == 0 ? 0 : 1;

sealed class InlineProgress(Action<string> action) : IProgress<string> { public void Report(string value) => action(value); }
sealed class InlineRepairProgress(Action<RepairProgress> action) : IProgress<RepairProgress> { public void Report(RepairProgress value) => action(value); }
sealed class Fixture
{
    public string Root { get; }
    public string App { get; }
    public string Config { get; }
    public string State { get; }
    public string[] CacheFiles { get; }
    public string Game { get; }
    public string PersistentNui { get; }
    public string ProtectedNestedGame { get; }
    public string Other { get; }
    public Fixture(string parent)
    {
        Root = Path.Combine(parent, Guid.NewGuid().ToString("N")); App = Path.Combine(Root, "FiveM", "FiveM.app");
        Directory.CreateDirectory(Path.Combine(App, "citizen")); File.WriteAllText(Path.Combine(App, "CitizenFX.ini"), "[Game]\r\nIVPath=C:\\GTA5\r\n");
        File.WriteAllText(Path.Combine(Root, "FiveM", "FiveM.exe"), "fixture");
        Config = Path.Combine(Root, "Roaming", "CitizenFX", "fivem.cfg"); Directory.CreateDirectory(Path.GetDirectoryName(Config)!);
        File.WriteAllText(Config, "bind keyboard F2 \"inventory\"\r\nseta \"nui_useFixedSize\" \"false\"\r\nseta \"nui_useInProcessGpu\" \"false\"\r\n", new UTF8Encoding(false));
        State = Path.Combine(Root, "RepairState");
        CacheFiles = new[] { "cache", "server-cache", "server-cache-priv" }.Select(name => Path.Combine(App, "data", name, "nested", "temporary.dat")).ToArray();
        Game = Path.Combine(App, "data", "game-storage", "game.rpf"); PersistentNui = Path.Combine(App, "data", "nui-storage", "preferences.dat");
        ProtectedNestedGame = Path.Combine(App, "data", "cache", "game", "game.rpf"); Other = Path.Combine(App, "data", "other", "keep.dat");
        foreach (var file in CacheFiles.Concat(new[] { Game, PersistentNui, ProtectedNestedGame, Other })) { Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, "keep or clean as specified"); }
    }
    public RepairService Service(Func<bool>? running = null) => new(Config, State, running ?? (() => false));
}
