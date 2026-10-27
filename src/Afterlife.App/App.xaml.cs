using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Afterlife.App;

public partial class App : Application
{
    public static string LogDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AFTERLIFE", "logs");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandled;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log(args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) => { Log(args.Exception); args.SetObserved(); };

        // P24: nxm:// links (Mod Manager Download) start AFTERLIFE with the link as argument.
        var link = e.Args.FirstOrDefault(a => a.StartsWith("nxm://", StringComparison.OrdinalIgnoreCase));
        _instance = new Services.SingleInstance();
        if (!_instance.IsFirst)
        {
            if (Services.SingleInstance.TrySend(link ?? "activate")) { Shutdown(); return; }
        }

        var window = new MainWindow(link);
        MainWindow = window;
        window.Show();
        if (_instance.IsFirst) _instance.Listen(msg => Dispatcher.BeginInvoke(() => window.OnExternalMessage(msg)));
    }

    private Services.SingleInstance? _instance;

    protected override void OnExit(ExitEventArgs e)
    {
        _instance?.Dispose();
        base.OnExit(e);
    }

    private void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var path = Log(e.Exception);
        MessageBox.Show(
            "Something went wrong in the window. Your game files are protected: unfinished changes are recovered on the next start.\n\n"
            + e.Exception.Message + (path is null ? "" : $"\n\nDetails: {path}"),
            "AFTERLIFE", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }

    private static string? Log(Exception? ex)
    {
        if (ex is null) return null;
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var path = Path.Combine(LogDirectory, $"error-{DateTime.Now:yyyyMMdd}.log");
            File.AppendAllText(path, $"[{DateTime.Now:O}] {ex}\n\n");
            return path;
        }
        catch { return null; }
    }
}
