namespace Afterlife.Core;

/// <summary>
/// Counts the actions that are running so that overlapping actions keep the window busy until the last one ends.
/// <see cref="Enter"/> returns a scope for <c>using</c>; <see cref="Changed"/> is raised only when the count goes
/// from 0 to 1 or from 1 to 0. Disposing a scope twice counts once.
/// </summary>
public sealed class BusyCounter
{
    private int _count;

    /// <summary>Raised when <see cref="IsBusy"/> flips; read <see cref="IsBusy"/> for the new state.</summary>
    public event Action? Changed;

    public bool IsBusy => Volatile.Read(ref _count) > 0;

    public int Count => Volatile.Read(ref _count);

    public IDisposable Enter()
    {
        if (Interlocked.Increment(ref _count) == 1) Changed?.Invoke();
        return new Scope(this);
    }

    private void Exit()
    {
        if (Interlocked.Decrement(ref _count) == 0) Changed?.Invoke();
    }

    private sealed class Scope(BusyCounter owner) : IDisposable
    {
        private BusyCounter? _owner = owner;

        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Exit();
    }
}
