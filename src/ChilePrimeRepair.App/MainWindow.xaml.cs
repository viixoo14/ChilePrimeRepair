using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using ChilePrimeRepair.Core;

namespace ChilePrimeRepair.App;

public partial class MainWindow : Window
{
    private const string RepairNotice = "Se limpiará la caché de FiveM y se fijarán las interfaces a 1920 × 1080. La caché se volverá a descargar. Puedes cerrar para cancelar.";
    private string currentNotice = RepairNotice;
    private DiagnosticPlan? diagnostic;
    private string? report;
    private readonly bool fixtureMode;
    private readonly RepairService service;
    private readonly bool preview;
    private readonly Func<CancellationToken, string?> detectInstallation;
    private readonly Func<bool> gameRunning;
    private bool busy;
    private bool succeeded;
    private bool closeWhenFinished;
    private CancellationTokenSource? cancellation;
    private string? preferredInstallation;
    private int generation;

    public MainWindow(bool preview = false, RepairService? serviceOverride = null,
        Func<string?>? detectInstallationOverride = null,
        Func<bool>? gameRunningOverride = null)
    {
        this.preview = preview;
        fixtureMode = serviceOverride is not null;
        gameRunning = gameRunningOverride ?? IsGameRunning;
        detectInstallation = detectInstallationOverride is null ? Installation.Detect : _ => detectInstallationOverride();
        service = serviceOverride ?? new RepairService(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CitizenFX", "fivem.cfg"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChilePrimeRepair"),
            gameRunning);
        InitializeComponent();
        SourceInitialized += (_, _) => WindowFrame.RemoveLightBorder(this);
        StateChanged += (_, _) => UpdateWindowControls();
        if (preview) return;
        Loaded += (_, _) =>
        {
            Height = Math.Max(MinHeight, Math.Min(Height, SystemParameters.WorkArea.Height - 32));
            Width = Math.Max(MinWidth, Math.Min(Width, SystemParameters.WorkArea.Width - 32));
        };
        Closing += OnClosing;
    }

    private static bool IsGameRunning()
    {
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    var name = process.ProcessName;
                    if (name.StartsWith("FiveM", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("GTA5", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("GTA5_Enhanced", StringComparison.OrdinalIgnoreCase)) return true;
                }
                catch (InvalidOperationException) { /* Process exited during enumeration. */ }
                catch (Win32Exception) { /* Unrelated protected Windows process. */ }
            }
        }
        return false;
    }

    private async void StartClick(object sender, RoutedEventArgs e) => await StartRepair();

    private void MinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        UpdateWindowControls();
    }
    private void CloseClick(object sender, RoutedEventArgs e) => Close();

    private void UpdateWindowControls()
    {
        var maximized = WindowState == WindowState.Maximized;
        MaximizeIcon.Visibility = maximized ? Visibility.Collapsed : Visibility.Visible;
        RestoreIcon.Visibility = maximized ? Visibility.Visible : Visibility.Collapsed;
        MaximizeButton.ToolTip = maximized ? "Restaurar" : "Maximizar";
    }

    private async Task StartRepair()
    {
        await Run(async token =>
        {
            SetProgress("Buscando FiveM…", 0, true);
            var installation = preferredInstallation ?? await Task.Run(() => detectInstallation(token), token);
            token.ThrowIfCancellationRequested();
            if (installation is null)
            {
                ShowOutcome(false, "No encontramos FiveM", "Ábrelo una vez, ciérralo y pulsa Reintentar. Si aún no lo tienes, instala FiveM primero.");
                return;
            }
            // A discovery hint is never trusted until the installation is validated.
            try { preferredInstallation = Installation.FromPath(installation).AppDirectory; }
            catch { preferredInstallation = null; throw; }
            await WaitForGame(token);
            SetProgress("Analizando tu equipo y los registros…", 0, true);
            diagnostic = await Task.Run(() => ClientDiagnostics.Analyze(preferredInstallation!, service.ConfigPath,
                fixtureMode ? DeviceSnapshot.Unknown : WindowsDeviceProbe.Read(preferredInstallation!), token), token);
            currentNotice = diagnostic.Notice;
            await AnnounceRepairChanges(token);
            var currentGeneration = generation;
            var progress = new Progress<RepairProgress>(value =>
            {
                if (busy && currentGeneration == generation) DisplayProgress(value);
            });
            var result = await Task.Run(
                () => service.Repair(preferredInstallation!, new(true, true, diagnostic.Settings, diagnostic.ConfigHash, true), token: token, stages: progress), token);
            if (result.Success)
            {
                SetProgress("Reparación completada", 100, false);
                ShowOutcome(true, "Reparación completada", diagnostic.Settings.Count == 0 ?
                    "Caché e interfaces revisadas. Abre FiveM y prueba Chile Prime; consulta el diagnóstico si el problema persiste." :
                    "Aplicamos ajustes según los errores encontrados. Reinicia FiveM, prueba Chile Prime y consulta las recomendaciones.");
            }
            else ShowOutcome(false, "No se pudo completar", result.Message);
            ShowDiagnosis(result.Success ? result.Cache?.DeletedFiles ?? 0 : null);
        });
    }

    private async Task AnnounceRepairChanges(CancellationToken token)
    {
        SetProgress("Preparando la reparación…", 0, true);
        ProgressHint.Text = currentNotice;
        ProgressHint.Visibility = Visibility.Visible;
        // Keep the notice visible before the first file mutation; closing still cancels.
        await Task.Delay(TimeSpan.FromSeconds(6), token);
        token.ThrowIfCancellationRequested();
    }

    private async Task WaitForGame(CancellationToken token)
    {
        while (gameRunning())
        {
            SetProgress("Cierra FiveM para continuar", 0, true);
            ProgressHint.Text = "Cierra también GTA V. Continuaremos automáticamente.";
            ProgressHint.Visibility = Visibility.Visible;
            await Task.Delay(500, token);
        }
        ProgressHint.Visibility = Visibility.Collapsed;
        token.ThrowIfCancellationRequested();
    }

    private async Task Run(Func<CancellationToken, Task> operation)
    {
        if (busy || preview) return;
        busy = true;
        succeeded = false;
        generation++;
        cancellation = new CancellationTokenSource();
        WelcomeView.Visibility = OutcomeView.Visibility = Visibility.Collapsed;
        ProgressView.Visibility = Visibility.Visible;
        RestoreButton.Visibility = Visibility.Collapsed;
        DiagnosisDetails.Visibility = GraphicsButton.Visibility = Visibility.Collapsed;
        DiagnosisDetails.IsExpanded = false;
        diagnostic = null; report = null;
        Progress.Value = 0;
        StartButton.IsEnabled = FinishButton.IsEnabled = RestoreButton.IsEnabled = false;
        try { await operation(cancellation.Token); }
        catch (OperationCanceledException)
        {
            ShowOutcome(false, "Operación interrumpida", "La reparación no se completó. Puedes volver a intentarlo.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            System.Text.Json.JsonException or ArgumentException or NotSupportedException)
        {
            ShowOutcome(false, "No se pudo completar", exception.Message);
        }
        finally
        {
            busy = false;
            cancellation.Dispose();
            cancellation = null;
            StartButton.IsEnabled = FinishButton.IsEnabled = RestoreButton.IsEnabled = true;
            if (closeWhenFinished) Close();
        }
    }

    private void DisplayProgress(RepairProgress value)
    {
        var (text, percent) = value.Stage switch
        {
            RepairStage.Checking => ("Comprobando tu cliente…", 0d),
            RepairStage.Scanning => ("Analizando la caché…", 10d),
            RepairStage.Backup => ("Respaldando tus preferencias…", 15d),
            RepairStage.Cleaning => ("Limpiando la caché…", 20 + 70 * Math.Clamp(value.Fraction ?? 0, 0, 1)),
            RepairStage.Interfaces => ("Ajustando las interfaces…", 95d),
            _ => ("Reparación completada", 100d)
        };
        SetProgress(text, Math.Max(Progress.Value, percent), value.Fraction is null);
    }

    private void SetProgress(string text, double percent, bool indeterminate)
    {
        StageLabel.Text = text;
        Progress.Value = percent;
        PercentLabel.Text = $"{Math.Floor(percent):0}%";
        ActivitySpinner.Visibility = indeterminate ? Visibility.Visible : Visibility.Hidden;
        ProgressHint.Visibility = Visibility.Collapsed;
    }

    private void ShowOutcome(bool success, string title, string message)
    {
        succeeded = success;
        WelcomeView.Visibility = ProgressView.Visibility = Visibility.Collapsed;
        OutcomeView.Visibility = Visibility.Visible;
        ResultTitle.Text = title;
        ResultMessage.Text = message;
        ResultMark.Text = success ? "✓" : "!";
        ResultMark.Foreground = (Brush)FindResource(success ? "Success" : "Warning");
        ResultIcon.Background = new SolidColorBrush(success ? Color.FromRgb(20, 42, 28) : Color.FromRgb(47, 35, 16));
        FinishButton.Content = success ? "Finalizar" : "Reintentar";
        RestoreButton.Visibility = !preview && service.CanRestore() ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void FinishClick(object sender, RoutedEventArgs e)
    {
        if (busy || preview) return;
        if (succeeded) Close();
        else await StartRepair();
    }

    private async void RestoreClick(object sender, RoutedEventArgs e)
    {
        await Run(async token =>
        {
            SetProgress("Restaurando tus interfaces…", 0, true);
            await WaitForGame(token);
            await Task.Run(service.Restore, token);
            SetProgress("Ajuste anterior restaurado", 100, false);
            ShowOutcome(true, "Ajustes anteriores restaurados", "Recuperamos las preferencias que modificó el asistente. La caché se regenerará al usar FiveM.");
        });
    }

    private void ShowDiagnosis(int? deletedFiles)
    {
        if (diagnostic is null) return;
        report = diagnostic.Report(deletedFiles);
        DiagnosisText.Text = report;
        DiagnosisDetails.Visibility = Visibility.Visible;
        GraphicsButton.Visibility = diagnostic.ReviewGraphics ? Visibility.Visible : Visibility.Collapsed;
        try
        {
            var path = Path.Combine(service.StateDirectory, "reports", DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmss") + "-" + Guid.NewGuid().ToString("N") + ".txt");
            Safety.EnsureBelow(path, service.StateDirectory);
            Safety.AtomicWrite(path, System.Text.Encoding.UTF8.GetBytes(report), null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { DiagnosisText.Text += "\nNo se pudo guardar el informe. Puedes copiarlo desde esta pantalla."; }
    }

    private void CopyDiagnosisClick(object sender, RoutedEventArgs e)
    {
        if (report is null) return;
        try { Clipboard.SetText(report); CopyDiagnosisButton.Content = "Informe copiado"; }
        catch (System.Runtime.InteropServices.COMException) { CopyDiagnosisButton.Content = "No se pudo copiar; vuelve a intentar"; }
    }
    private void GraphicsClick(object sender, RoutedEventArgs e)
    {
        if (preview || fixtureMode) return;
        try { Process.Start(new ProcessStartInfo("ms-settings:display-advancedgraphics") { UseShellExecute = true }); }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        { ResultMessage.Text = "Abre Configuración de Windows → Sistema → Pantalla → Gráficos y revisa la preferencia del juego."; }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!busy) return;
        e.Cancel = true;
        closeWhenFinished = true;
        cancellation?.Cancel();
        StageLabel.Text = "Terminando la operación…";
    }

    internal void ShowPreview(string state)
    {
        switch (state)
        {
            case "progress":
                WelcomeView.Visibility = Visibility.Collapsed; ProgressView.Visibility = Visibility.Visible;
                SetProgress("Limpiando la caché…", 67, false);
                break;
            case "waiting":
                WelcomeView.Visibility = Visibility.Collapsed; ProgressView.Visibility = Visibility.Visible;
                SetProgress("Cierra FiveM para continuar", 0, true);
                ProgressHint.Text = "Cierra también GTA V. Continuaremos automáticamente.";
                ProgressHint.Visibility = Visibility.Visible;
                break;
            case "success":
                ShowOutcome(true, "Reparación completada", "Ya puedes abrir FiveM y volver a Chile Prime.");
                break;
        }
    }

    internal async Task SmokeTest(string installation, string config, string cache, string output)
    {
        foreach (var weight in new[] { FontWeights.Normal, FontWeights.Medium, FontWeights.SemiBold, FontWeights.Bold })
        {
            var typeface = new Typeface((FontFamily)FindResource("UiFont"), FontStyles.Normal, weight, FontStretches.Normal);
            if (!typeface.TryGetGlyphTypeface(out var glyph) || !glyph.FamilyNames.Values.Contains("Montserrat") ||
                !glyph.FontUri.ToString().Contains("/assets/fonts/montserrat/", StringComparison.OrdinalIgnoreCase) ||
                glyph.Weight != weight)
                throw new InvalidOperationException("The packaged Montserrat typeface could not be loaded: " + weight);
        }
        if (WelcomeView.Visibility != Visibility.Visible || ProgressView.Visibility != Visibility.Collapsed ||
            OutcomeView.Visibility != Visibility.Collapsed || (string)StartButton.Content != "Comenzar")
            throw new InvalidOperationException("The assistant did not start on its minimal welcome screen.");
        App.SavePreview(this, Path.Combine(output, "welcome.png"));
        StartButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        if (ProgressView.Visibility != Visibility.Visible || StartButton.IsEnabled)
            throw new InvalidOperationException("Starting did not display progress and prevent a duplicate start.");
        await WaitUntilIdle();
        if (File.Exists(cache) || !new Configuration(File.ReadAllBytes(config)).IsEnabled ||
            !succeeded || Progress.Value != 100 || RestoreButton.Visibility != Visibility.Visible)
            throw new InvalidOperationException("A single click did not apply both repairs and expose recovery.");
        if (DiagnosisDetails.Visibility != Visibility.Visible || report is null || !report.Contains("diagnóstico", StringComparison.OrdinalIgnoreCase) ||
            !report.Contains("inconcluso", StringComparison.OrdinalIgnoreCase) && !report.Contains("No hay registros"))
            throw new InvalidOperationException("The result did not expose a truthful local diagnosis.");
        App.SavePreview(this, Path.Combine(output, "completed.png"));
        RestoreButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        await WaitUntilIdle();
        if (new Configuration(File.ReadAllBytes(config)).IsEnabled ||
            !File.ReadAllText(config).Contains("inventory") || RestoreButton.Visibility != Visibility.Collapsed)
            throw new InvalidOperationException("The recovery action did not preserve the original preference.");
        App.SavePreview(this, Path.Combine(output, "restored.png"));
        var scroll = AssistantScroll;
        App.SavePreview(this, Path.Combine(output, "small-window.png"), 1.5, 400, 280);
        if (scroll.ScrollableHeight <= 0) throw new InvalidOperationException("Small window is not scrollable.");
        scroll.ScrollToEnd(); scroll.UpdateLayout();
        if (scroll.VerticalOffset <= 0) throw new InvalidOperationException("Actions cannot be reached on a small screen.");
    }

    internal async Task SmokeTestWaiting(string config, string cache, Action closeGame, string output)
    {
        StartButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        var timer = Stopwatch.StartNew();
        while (!StageLabel.Text.StartsWith("Cierra FiveM"))
        {
            if (!busy || timer.Elapsed > TimeSpan.FromSeconds(10)) throw new InvalidOperationException("The assistant did not wait for the game.");
            await Task.Delay(20);
        }
        if (!File.Exists(cache) || new Configuration(File.ReadAllBytes(config)).IsEnabled)
            throw new InvalidOperationException("Files were changed while the game was running.");
        App.SavePreview(this, Path.Combine(output, "waiting.png"));
        closeGame();
        await WaitUntilIdle();
        if (!succeeded || File.Exists(cache) || !new Configuration(File.ReadAllBytes(config)).IsEnabled)
            throw new InvalidOperationException("The assistant did not resume automatically after the game closed.");
    }

    internal async Task SmokeTestAdvancedDiagnosis(string installation, string config, string cache, string output)
    {
        var logs = Path.Combine(installation, "logs"); Directory.CreateDirectory(logs);
        var log = Path.Combine(logs, "CitizenFX_log_fixture.log");
        File.AppendAllText(config, "seta str_maxVehicleTextureRes 4096\nseta vid_budgetScale 8\n");
        File.WriteAllText(log, "ERROR: out of memory\nERROR: failed to download private-resource\n");
        File.WriteAllText(cache, "fixture cache");
        await StartRepair();
        if (!succeeded || new Configuration(File.ReadAllBytes(config), "str_maxVehicleTextureRes").Value != "1024" ||
            new Configuration(File.ReadAllBytes(config), "vid_budgetScale").Value != "8" ||
            report is null || !report.Contains("fallos de descarga") || report.Contains("private-resource"))
            throw new InvalidOperationException("Evidence-based memory repair or private report failed.");
        DiagnosisDetails.IsExpanded = true;
        App.SavePreview(this, Path.Combine(output, "diagnosis.png"), 1, 580, 950);
        service.Restore();
        File.WriteAllText(log, "[CEF] GPU process launch failed\n");
        await StartRepair();
        if (!succeeded || !new Configuration(File.ReadAllBytes(config), "nui_useInProcessGpu").IsEnabled ||
            !currentNotice.Contains("reducir su fluidez"))
            throw new InvalidOperationException("Conditional NUI compatibility trial failed.");
        service.Restore();
        if (new Configuration(File.ReadAllBytes(config), "nui_useInProcessGpu").IsEnabled ||
            new Configuration(File.ReadAllBytes(config), "str_maxVehicleTextureRes").Value != "4096")
            throw new InvalidOperationException("Advanced repair restoration failed.");
    }

    internal async Task SmokeTestMissing(string config, string cache, string output)
    {
        StartButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        await WaitUntilIdle();
        if (succeeded || !File.Exists(cache) || new Configuration(File.ReadAllBytes(config)).IsEnabled ||
            (string)FinishButton.Content != "Reintentar")
            throw new InvalidOperationException("Missing or invalid automatic detection caused writes or removed retry.");
        App.SavePreview(this, Path.Combine(output, "not-found.png"));
    }

    private async Task WaitUntilIdle()
    {
        var timer = Stopwatch.StartNew();
        while (busy)
        {
            if (timer.Elapsed > TimeSpan.FromSeconds(30)) throw new TimeoutException("UI operation did not finish.");
            await Task.Delay(20);
        }
    }

    internal async Task SmokeTestRetry(string config, string cache, Action makeClientAvailable)
    {
        makeClientAvailable();
        FinishButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        await WaitUntilIdle();
        if (!succeeded || File.Exists(cache) || !new Configuration(File.ReadAllBytes(config)).IsEnabled)
            throw new InvalidOperationException("Retry did not automatically discover and repair the newly available client.");
    }

    internal void SmokeTestWindowControls()
    {
        MinimizeButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        if (WindowState != WindowState.Minimized) throw new InvalidOperationException("The minimize control did not minimize the window.");
        WindowState = WindowState.Normal;
        MaximizeButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        if (WindowState != WindowState.Maximized || RestoreIcon.Visibility != Visibility.Visible)
            throw new InvalidOperationException("The maximize control did not maximize the window.");
        MaximizeButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        if (WindowState != WindowState.Normal || MaximizeIcon.Visibility != Visibility.Visible)
            throw new InvalidOperationException("The restore control did not restore the window.");
        // Create our own hidden native window to exercise SourceInitialized and DWM frame setup.
        _ = new WindowInteropHelper(this).EnsureHandle();
        var closingRequested = false;
        CancelEventHandler preventClosing = (_, args) => { closingRequested = true; args.Cancel = true; };
        Closing += preventClosing;
        try { CloseButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); }
        finally { Closing -= preventClosing; }
        if (!closingRequested) throw new InvalidOperationException("The close control did not request normal window closure.");
    }

    internal async Task SmokeTestCloseWhileWaiting(string config, string cache)
    {
        var closed = false;
        Closed += (_, _) => closed = true;
        StartButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        var timer = Stopwatch.StartNew();
        while (!StageLabel.Text.StartsWith("Cierra FiveM"))
        {
            if (!busy || timer.Elapsed > TimeSpan.FromSeconds(10)) throw new InvalidOperationException("The close test did not reach its waiting stage.");
            await Task.Delay(20);
        }
        CloseButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        await WaitUntilIdle();
        if (!closed || !File.Exists(cache) || new Configuration(File.ReadAllBytes(config)).IsEnabled)
            throw new InvalidOperationException("Closing during a wait did not cancel safely and preserve files.");
    }

    internal async Task SmokeTestNoticeBeforeWrites(string config, string cache)
    {
        var closed = false;
        Closed += (_, _) => closed = true;
        StartButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        var timer = Stopwatch.StartNew();
        while (StageLabel.Text != "Preparando la reparación…")
        {
            if (!busy || timer.Elapsed > TimeSpan.FromSeconds(10)) throw new InvalidOperationException("The change notice was not displayed.");
            await Task.Delay(20);
        }
        if (ProgressHint.Visibility != Visibility.Visible || ProgressHint.Text != RepairNotice ||
            !File.Exists(cache) || new Configuration(File.ReadAllBytes(config)).IsEnabled)
            throw new InvalidOperationException("The change notice was hidden or files were changed before it.");
        CloseButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        await WaitUntilIdle();
        if (!closed || !File.Exists(cache) || new Configuration(File.ReadAllBytes(config)).IsEnabled)
            throw new InvalidOperationException("Closing during the change notice did not preserve files.");
    }
}
