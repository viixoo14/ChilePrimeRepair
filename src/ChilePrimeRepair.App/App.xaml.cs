using System.Security.Principal;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ChilePrimeRepair.Core;

namespace ChilePrimeRepair.App;

public partial class App : Application
{
    private Mutex? instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var smoke = Array.IndexOf(e.Args, "--smoke-test");
        if (smoke >= 0)
        {
            if (smoke + 1 >= e.Args.Length) { Shutdown(2); return; }
            var output = Path.GetFullPath(e.Args[smoke + 1]);
            Directory.CreateDirectory(output);
            var fixture = Path.Combine(output, "fixture-" + Guid.NewGuid().ToString("N"));
            var installation = Path.Combine(fixture, "FiveM.app");
            Directory.CreateDirectory(Path.Combine(installation, "citizen"));
            File.WriteAllText(Path.Combine(installation, "CitizenFX.ini"), "[Game]\r\nIVPath=C:\\MockGTA\r\n");
            var cache = Path.Combine(installation, "data", "cache", "temporary.dat");
            Directory.CreateDirectory(Path.GetDirectoryName(cache)!); File.WriteAllText(cache, "cached resource");
            var config = Path.Combine(fixture, "CitizenFX", "fivem.cfg");
            Directory.CreateDirectory(Path.GetDirectoryName(config)!);
            File.WriteAllText(config, "bind keyboard F2 \"inventory\"\r\nseta \"nui_useFixedSize\" \"false\"\r\n");
            var service = new RepairService(config, Path.Combine(fixture, "state"), () => false);
            var window = new MainWindow(false, service,
                () => Installation.DetectCandidates([Path.Combine(fixture, "stale"), installation]), () => false);
            Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    window.SmokeTestWindowControls();
                    await window.SmokeTest(installation, config, cache, output);
                    File.WriteAllText(cache, "cached resource");
                    var gameOpen = true;
                    var waitService = new RepairService(config, Path.Combine(fixture, "state"), () => gameOpen);
                    var waiting = new MainWindow(false, waitService, () => installation, () => gameOpen);
                    await waiting.SmokeTestWaiting(config, cache, () => gameOpen = false, output);
                    waitService.Restore();
                    File.WriteAllText(cache, "cached resource");
                    string? detected = null;
                    var missing = new MainWindow(false, service, () => detected, () => false);
                    await missing.SmokeTestMissing(config, cache, output);
                    await missing.SmokeTestRetry(config, cache, () => detected = installation);
                    service.Restore();
                    File.WriteAllText(cache, "cached resource");
                    var invalid = new MainWindow(false, service, () => Path.Combine(fixture, "missing"), () => false);
                    await invalid.SmokeTestMissing(config, cache, output);
                    var closeDuringWait = new MainWindow(false, service, () => installation, () => true);
                    await closeDuringWait.SmokeTestCloseWhileWaiting(config, cache);
                    var closeDuringNotice = new MainWindow(false, service, () => installation, () => false);
                    await closeDuringNotice.SmokeTestNoticeBeforeWrites(config, cache);
                    File.WriteAllText(Path.Combine(output, "ui-smoke-result.txt"), "PASS: dark integrated window controls minimize, maximize, restore and request normal closure; closing during a game wait cancels and preserves files; the change notice appears before writes and closing during it preserves files; embedded Montserrat regular/medium/semibold/bold with matching font weights, minimal welcome, automatic discovery skipping a stale hint, one-click repair without a folder picker, duplicate-start protection, completed progress, restored setting, preserved preferences, waiting for an open game without writes, automatic resume, missing or invalid detection without writes, retry discovers a newly available client and completes both repairs, scrollable small window.\nNo real FiveM files were used.");
                    Shutdown();
                }
                catch (Exception exception)
                {
                    File.WriteAllText(Path.Combine(output, "ui-smoke-error.txt"), exception.ToString());
                    Shutdown(1);
                }
            });
            return;
        }
        var preview = Array.IndexOf(e.Args, "--preview");
        if (preview >= 0)
        {
            if (preview + 1 >= e.Args.Length) { Shutdown(2); return; }
            var window = new MainWindow(true);
            var stateIndex = Array.IndexOf(e.Args, "--state");
            if (stateIndex >= 0 && stateIndex + 1 < e.Args.Length) window.ShowPreview(e.Args[stateIndex + 1]);
            var scaleIndex = Array.IndexOf(e.Args, "--scale");
            var scale = scaleIndex >= 0 && scaleIndex + 1 < e.Args.Length
                ? double.Parse(e.Args[scaleIndex + 1], System.Globalization.CultureInfo.InvariantCulture) : 1;
            SavePreview(window, e.Args[preview + 1], scale);
            Shutdown();
            return;
        }
        var identity = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        instance = new Mutex(true, "Local\\ChilePrimeRepair-" + identity, out var created);
        if (!created)
        {
            MessageBox.Show("Chile Prime Repair ya está abierto.", "Chile Prime Repair", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show("No se pudo completar la operación. " + args.Exception.Message, "Chile Prime Repair", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        new MainWindow().Show();
    }
    internal static void SavePreview(MainWindow window, string path, double scale = 1, double width = 580, double height = 520)
    {
        var content = (FrameworkElement)window.Content;
        // Match layout rounding and glyph hinting to the preview DPI as well as the bitmap size.
        Visual root = content;
        while (VisualTreeHelper.GetParent(root) is Visual parent) root = parent;
        VisualTreeHelper.SetRootDpi(root, new DpiScale(scale, scale));
        content.Width = width; content.Height = height;
        content.Measure(new Size(width, height));
        content.Arrange(new Rect(0, 0, width, height));
        content.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)(width * scale), (int)(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.GetFullPath(path)); encoder.Save(stream);
    }
    protected override void OnExit(ExitEventArgs e) { instance?.Dispose(); base.OnExit(e); }
}
