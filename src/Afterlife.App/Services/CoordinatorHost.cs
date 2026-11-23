using Afterlife.Infrastructure;

namespace Afterlife.App.Services;

/// <summary>
/// Owns the single InstallCoordinator. Every call runs on a background thread, one at a time,
/// So the window never freezes and the SQLite connection is never used concurrently.
/// </summary>
public sealed class CoordinatorHost : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private InstallCoordinator? _coordinator;

    public bool IsOpen => _coordinator is not null;
    /// <summary>Paths of the open library (they do not change while it is open).</summary>
    public LibraryPaths? Library => _coordinator?.Library;

    public async Task OpenAsync(string libraryRoot)
    {
        await _gate.WaitAsync();
        try
        {
            var c = await Task.Run(() => InstallCoordinator.Open(libraryRoot));
            _coordinator?.Dispose();
            _coordinator = c;
        }
        finally { _gate.Release(); }
    }

    public async Task InitializeAsync(string gameRoot, string libraryRoot)
    {
        await _gate.WaitAsync();
        try
        {
            var c = await Task.Run(() => InstallCoordinator.Initialize(gameRoot, libraryRoot));
            _coordinator?.Dispose();
            _coordinator = c;
        }
        finally { _gate.Release(); }
    }

    public async Task<T> RunAsync<T>(Func<InstallCoordinator, T> work)
    {
        await _gate.WaitAsync();
        try
        {
            var c = _coordinator ?? throw new InvalidOperationException("no game install is open");
            return await Task.Run(() => work(c));
        }
        finally { _gate.Release(); }
    }

    public Task RunAsync(Action<InstallCoordinator> work) => RunAsync(c => { work(c); return true; });

    /// <summary>
    /// Runs <paramref name="check"/> on the open library, closes it, runs <paramref name="move"/> while nothing
    /// holds its files, and opens the library where it is afterwards (the new folder, or the old one when the move
    /// failed). Nothing else runs in between.
    /// </summary>
    public async Task MoveLibraryAsync(string target, Action<InstallCoordinator> check, Action move)
    {
        await _gate.WaitAsync();
        try
        {
            var c = _coordinator ?? throw new InvalidOperationException("no game install is open");
            var source = c.Library.Root;
            await Task.Run(() => check(c));
            c.Dispose();
            _coordinator = null;
            try { await Task.Run(move); }
            finally
            {
                var at = AppSettings.ResolveLibrary(null);
                if (!AppSettings.IsInitialized(at)) at = AppSettings.IsInitialized(source) ? source : target;
                _coordinator = await Task.Run(() => InstallCoordinator.Open(at));
            }
        }
        finally { _gate.Release(); }
    }

    public void Dispose()
    {
        _coordinator?.Dispose();
        _gate.Dispose();
    }
}
