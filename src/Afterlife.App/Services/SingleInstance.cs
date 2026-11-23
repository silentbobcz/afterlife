using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace Afterlife.App.Services;

/// <summary>
/// One AFTERLIFE window per Windows user. A second start (for example from an nxm link) hands its link to the
/// Running window over a named pipe and exits.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    // string.GetHashCode is randomized per process; use a stable hash so both instances agree on the name.
    private static string Name => "AFTERLIFE-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserDomainName + "\\" + Environment.UserName)))[..12];
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _cts = new();
    public bool IsFirst { get; }

    public SingleInstance()
    {
        _mutex = new Mutex(true, Name + "-mutex", out var created);
        IsFirst = created;
    }

    public static bool TrySend(string message)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", Name, PipeDirection.Out);
            client.Connect(1500);
            using var w = new StreamWriter(client, Encoding.UTF8);
            w.WriteLine(message);
            w.Flush();
            return true;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException) { return false; }
    }

    public void Listen(Action<string> onMessage)
    {
        var token = _cts.Token;
        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(Name, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await server.WaitForConnectionAsync(token);
                    using var r = new StreamReader(server, Encoding.UTF8);
                    var line = await r.ReadLineAsync(token);
                    if (!string.IsNullOrWhiteSpace(line)) onMessage(line.Trim());
                }
                catch (OperationCanceledException) { break; }
                catch (IOException) { await Task.Delay(200, CancellationToken.None); }
            }
        }, token);
    }

    public void Dispose()
    {
        _cts.Cancel();
        if (IsFirst) { try { _mutex.ReleaseMutex(); } catch (ApplicationException) { } }
        _mutex.Dispose();
    }
}
