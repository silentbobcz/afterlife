using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using Afterlife.Infrastructure;

namespace Afterlife.App.Services;

/// <summary>
/// One AFTERLIFE window per Windows user. A second start (for example from an nxm link) hands its link to the
/// running window and exits; it never opens a window of its own. The hand-over goes over a named pipe and, when the
/// pipe cannot reach the window (for example because it runs as administrator), through files in <see cref="LinkInbox"/>.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    // string.GetHashCode is randomized per process; use a stable hash so both instances agree on the name.
    private static string Name => "AFTERLIFE-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserDomainName + "\\" + Environment.UserName)))[..12];
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(5);
    /// <summary>A link left in the inbox longer than this was meant for a window that is gone; it is not downloaded later.</summary>
    private static readonly TimeSpan InboxMaxAge = TimeSpan.FromMinutes(10);
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _cts = new();
    private FileSystemWatcher? _watcher;
    private System.Threading.Timer? _inboxTimer;
    public bool IsFirst { get; }

    public SingleInstance()
    {
        try
        {
            _mutex = new Mutex(true, Name + "-mutex", out var created);
            IsFirst = created;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or WaitHandleCannotBeOpenedException)
        {
            // the mutex exists but belongs to an AFTERLIFE that runs as administrator
            _mutex = new Mutex();
            IsFirst = false;
        }
    }

    /// <summary>
    /// Hands the message to the running window: over the pipe for a few seconds, then through the inbox, waiting for
    /// the window to take the file. False when nothing took it within <paramref name="timeout"/>.
    /// </summary>
    public static bool Deliver(string message, TimeSpan timeout)
    {
        var clock = Stopwatch.StartNew();
        var pipeUntil = TimeSpan.FromTicks(timeout.Ticks * 2 / 5);
        // the running window may still be starting; a refused pipe (administrator) will not change, so skip ahead
        while (clock.Elapsed < pipeUntil)
        {
            var sent = TrySend(message, out var refused);
            if (sent) return true;
            if (refused) break;
            Thread.Sleep(250);
        }
        // from here on only the inbox: using both could hand the same link over twice
        var file = new LinkInbox(LinkInbox.DefaultFolder).Drop(message);
        if (file is null) return false;
        while (clock.Elapsed < timeout)
        {
            if (!File.Exists(file)) return true;
            Thread.Sleep(200);
        }
        try { File.Delete(file); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return !File.Exists(file);
    }

    private static bool TrySend(string message, out bool refused)
    {
        refused = false;
        try
        {
            using var client = new NamedPipeClientStream(".", Name, PipeDirection.Out);
            client.Connect(1000);
            using var w = new StreamWriter(client, Encoding.UTF8);
            w.WriteLine(message);
            w.Flush();
            return true;
        }
        catch (UnauthorizedAccessException) { refused = true; return false; }
        catch (Exception ex) when (ex is IOException or TimeoutException) { return false; }
    }

    /// <summary>
    /// Receives messages from later starts: over the pipe (several clients at once, each read limited to a few seconds)
    /// and from the inbox (watched, polled, and read once now for files written before this start). Only accepted
    /// messages reach <paramref name="onMessage"/>; errors go to <paramref name="onError"/> and listening goes on.
    /// </summary>
    public void Listen(Action<string> onMessage, Action<Exception> onError)
    {
        var token = _cts.Token;
        void Pass(string? text)
        {
            if (LinkInbox.IsAccepted(text)) onMessage(text!.Trim());
        }

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                NamedPipeServerStream? server = null;
                try
                {
                    server = new NamedPipeServerStream(Name, PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await server.WaitForConnectionAsync(token);
                    // read this client on its own, so the next one can connect right away
                    var connected = server;
                    server = null;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await using (connected)
                            {
                                using var limit = CancellationTokenSource.CreateLinkedTokenSource(token);
                                limit.CancelAfter(ReadTimeout);
                                using var r = new StreamReader(connected, Encoding.UTF8);
                                Pass(await r.ReadLineAsync(limit.Token));
                            }
                        }
                        catch (OperationCanceledException) { } // a client that never wrote a line
                        catch (Exception ex) { onError(ex); }
                    }, CancellationToken.None);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    onError(ex);
                    try { await Task.Delay(500, token); } catch (OperationCanceledException) { break; }
                }
                finally
                {
                    if (server is not null) await server.DisposeAsync();
                }
            }
        }, CancellationToken.None);

        var inbox = new LinkInbox(LinkInbox.DefaultFolder);
        void DrainInbox()
        {
            if (token.IsCancellationRequested) return;
            try { foreach (var m in inbox.Drain(InboxMaxAge)) Pass(m); }
            catch (Exception ex) { onError(ex); }
        }

        try
        {
            Directory.CreateDirectory(inbox.Folder);
            _watcher = new FileSystemWatcher(inbox.Folder) { IncludeSubdirectories = false };
            _watcher.Created += (_, _) => DrainInbox();
            _watcher.Renamed += (_, _) => DrainInbox();
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex) { onError(ex); }
        // the watcher can miss events (buffer overflow, network profile), so the folder is also looked at every few seconds
        _inboxTimer = new System.Threading.Timer(_ => DrainInbox(), null, TimeSpan.Zero, TimeSpan.FromSeconds(3));
    }

    public void Dispose()
    {
        _cts.Cancel();
        _watcher?.Dispose();
        _inboxTimer?.Dispose();
        if (IsFirst) { try { _mutex.ReleaseMutex(); } catch (ApplicationException) { } }
        _mutex.Dispose();
    }
}
