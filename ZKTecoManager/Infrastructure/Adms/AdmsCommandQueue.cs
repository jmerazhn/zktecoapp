using System.Collections.Concurrent;

namespace ZKTecoManager.Infrastructure.Adms;

// In-memory only: enrolling only makes sense while the app and the device are both
// online right now (same UX as the existing Pull SDK enroll flow), so there's no need
// to persist pending commands across app/device restarts. Keyed by device serial
// number, since that's the identifier the device sends on every ADMS request.
public class AdmsCommandQueue
{
    private record PendingCommand(string Id, string CommandText, TaskCompletionSource<string> ResultTcs);

    private readonly ConcurrentDictionary<string, ConcurrentQueue<PendingCommand>> _bySerial = new();
    private readonly ConcurrentDictionary<string, PendingCommand> _byCommandId = new();
    private long _nextId;

    public async Task<(bool Success, string Detail)> EnqueueAndWaitAsync(
        string serialNumber, string commandText, TimeSpan timeout, CancellationToken ct = default)
    {
        var id = Interlocked.Increment(ref _nextId).ToString();
        var pending = new PendingCommand(id, commandText, new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously));

        _byCommandId[id] = pending;
        _bySerial.GetOrAdd(serialNumber, _ => new ConcurrentQueue<PendingCommand>()).Enqueue(pending);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);
            using var _ = cts.Token.Register(() => pending.ResultTcs.TrySetResult("TIMEOUT"));

            var result = await pending.ResultTcs.Task.ConfigureAwait(false);
            return result == "TIMEOUT"
                ? (false, $"El dispositivo no respondió en {timeout.TotalSeconds:0}s (¿está encendido y configurado con ADMS hacia esta PC?).")
                : (true, result);
        }
        finally
        {
            _byCommandId.TryRemove(id, out _);
        }
    }

    // Called by AdmsServer when the device polls GET /iclock/getrequest.
    public IReadOnlyList<(string Id, string CommandText)> DequeuePending(string serialNumber)
    {
        if (!_bySerial.TryGetValue(serialNumber, out var queue)) return Array.Empty<(string, string)>();

        var items = new List<(string, string)>();
        while (queue.TryDequeue(out var cmd))
            items.Add((cmd.Id, cmd.CommandText));
        return items;
    }

    // Called by AdmsServer when the device POSTs the result to /iclock/devicecmd.
    public void Resolve(string commandId, string resultText)
    {
        if (_byCommandId.TryGetValue(commandId, out var pending))
            pending.ResultTcs.TrySetResult(resultText);
    }
}
