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
        if (AppEdition.Offline) Afterlife.Infrastructure.NexusHttp.Disable(); // no HTTP client is ever created
        // a refreshed login survives a restart; a login the user removed meanwhile stays removed
        else Afterlife.Infrastructure.NexusOAuth.Refreshed = c => { if (Services.NexusKeyStore.Load() is not null) Services.NexusKeyStore.Save(c); };
        DispatcherUnhandledException += OnUnhandled;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log(args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) => { Log(args.Exception); args.SetObserved(); };

        // nxm:// links (Mod Manager Download) start AFTERLIFE with the link as argument.
        var link = e.Args.FirstOrDefault(a => a.StartsWith("nxm://", StringComparison.OrdinalIgnoreCase));
        _instance = new Services.SingleInstance();
        if (!_instance.IsFirst)
        {
            // another AFTERLIFE runs: hand the link over and never open a second window
            if (!Services.SingleInstance.Deliver(link ?? Afterlife.Infrastructure.LinkInbox.Activate, TimeSpan.FromSeconds(10)))
                MessageBox.Show("AFTERLIFE is already running, possibly as administrator, and did not take the link. "
                    + "Close it, or start it without administrator rights, and try again.",
                    "AFTERLIFE", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        // listen before the window is built: a link clicked while it starts must not find nobody there
        _instance.Listen(msg => Dispatcher.BeginInvoke(() => Deliver(msg)), ex => Log(ex));
        var window = new MainWindow(link);
        MainWindow = window;
        window.Show();
        foreach (var msg in _early) window.OnExternalMessage(msg);
        _early.Clear();
    }

    private Services.SingleInstance? _instance;
    private readonly List<string> _early = [];

    private void Deliver(string message)
    {
        if (MainWindow is MainWindow window) window.OnExternalMessage(message);
        else _early.Add(message);
    }

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
            File.AppendAllText(path, $"[{DateTime.Now:O}] {Afterlife.Infrastructure.LogPrivacy.Scrub(ex.ToString())}\n\n");
            return path;
        }
        catch { return null; }
    }
}
