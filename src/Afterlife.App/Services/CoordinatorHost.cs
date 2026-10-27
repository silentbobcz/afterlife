using Afterlife.Infrastructure;

namespace Afterlife.App.Services;

/// <summary>
/// Owns the single InstallCoordinator. Every call runs on a background thread, one at a time,
/// so the window never freezes and the SQLite connection is never used concurrently.
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

    public void Dispose()
    {
        _coordinator?.Dispose();
        _gate.Dispose();
    }
}
