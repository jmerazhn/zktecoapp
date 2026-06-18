using System.IO;
using System.Net;
using System.Text;

namespace ZKTecoManager.Infrastructure.Adms;

// Minimal ADMS/Push protocol server (the protocol ZKTeco's own ZKTimeNet/ZKBioTime use
// to manage devices whose firmware doesn't expose remote user writes over Pull SDK or
// Standalone SDK — see project notes). The device connects OUT to us and polls; we never
// connect to the device for this path. Uses plain HttpListener — no ASP.NET Core/Hosting,
// matching the rest of the app's minimal-dependency style.
//
// Exact handshake/response format is a known unknown until tested against the real
// device — every request is dumped to adms-debug.log so it can be inspected and the
// response format adjusted without guessing blind.
public class AdmsServer : IDisposable
{
    private readonly AdmsCommandQueue _queue;
    private readonly string _logPath = Path.Combine(AppContext.BaseDirectory, "adms-debug.log");
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly object _logLock = new();

    public AdmsServer(AdmsCommandQueue queue) => _queue = queue;

    public void Start(int port)
    {
        if (_listener is not null) return;

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://+:{port}/iclock/");
        _cts = new CancellationTokenSource();

        try
        {
            _listener.Start();
        }
        catch (HttpListenerException ex)
        {
            Log($"No se pudo iniciar el listener en el puerto {port}: {ex.Message} " +
                "(¿falta el URL ACL? netsh http add urlacl url=http://+:" + port + "/ user=Everyone)");
            throw;
        }

        Log($"ADMS escuchando en puerto {port}.");
        _ = AcceptLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _listener?.Stop(); } catch { /* best effort */ }
        _listener = null;
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (_listener is { IsListening: true } && !ct.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                if (ct.IsCancellationRequested) break;
                continue;
            }

            _ = HandleAsync(ctx);
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        string body;
        using (var reader = new System.IO.StreamReader(ctx.Request.InputStream, Encoding.UTF8))
            body = await reader.ReadToEndAsync().ConfigureAwait(false);

        var path = ctx.Request.Url?.AbsolutePath ?? "";
        var query = ctx.Request.Url?.Query ?? "";
        var sn = GetQueryParam(query, "SN") ?? "";

        Log($"{ctx.Request.HttpMethod} {path}{query}\n{body}");

        string response;
        try
        {
            response = (ctx.Request.HttpMethod, path) switch
            {
                ("GET", "/iclock/cdata") => HandleHandshake(sn),
                ("POST", "/iclock/cdata") => HandleAttLogPush(sn, body),
                ("GET", "/iclock/getrequest") => HandleGetRequest(sn),
                ("POST", "/iclock/devicecmd") => HandleDeviceCmdResult(body),
                _ => "OK"
            };
        }
        catch (Exception ex)
        {
            Log($"Error manejando {path}: {ex}");
            response = "OK";
        }

        Log($"-> {response}");

        var bytes = Encoding.UTF8.GetBytes(response);
        ctx.Response.ContentType = "text/plain";
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        ctx.Response.OutputStream.Close();
    }

    // Device registration/handshake. Format per common ADMS server implementations —
    // adjust based on what adms-debug.log shows the device actually doing in response.
    private static string HandleHandshake(string sn) =>
        "GET OPTION FROM: " + sn + "\r\n" +
        "Stamp=9999\r\n" +
        "OpStamp=9999\r\n" +
        "ErrorDelay=60\r\n" +
        "Delay=30\r\n" +
        "TransTimes=00:00;14:05\r\n" +
        "TransInterval=1\r\n" +
        "TransFlag=1111000000\r\n" +
        "Realtime=1\r\n" +
        "Encrypt=0\r\n";

    // Phase A stub: just acknowledge. Real ATTLOG ingestion into AttendanceLog comes
    // once the protocol round-trip is confirmed working end to end.
    private string HandleAttLogPush(string sn, string body) => "OK";

    private string HandleGetRequest(string sn)
    {
        var pending = _queue.DequeuePending(sn);
        if (pending.Count == 0) return "OK";

        var sb = new StringBuilder();
        foreach (var (id, commandText) in pending)
            sb.Append("C:").Append(id).Append(':').Append(commandText).Append("\r\n");
        return sb.ToString();
    }

    private string HandleDeviceCmdResult(string body)
    {
        foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var fields = ParseFormEncoded(line);
            if (fields.TryGetValue("ID", out var id))
                _queue.Resolve(id, fields.GetValueOrDefault("Return", "?"));
        }
        return "OK";
    }

    private static Dictionary<string, string> ParseFormEncoded(string line)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in line.Split('&'))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2)
                dict[Uri.UnescapeDataString(kv[0])] = Uri.UnescapeDataString(kv[1]);
        }
        return dict;
    }

    private static string? GetQueryParam(string query, string name)
    {
        if (string.IsNullOrEmpty(query)) return null;
        foreach (var part in query.TrimStart('?').Split('&'))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && string.Equals(kv[0], name, StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(kv[1]);
        }
        return null;
    }

    private void Log(string message)
    {
        lock (_logLock)
        {
            try
            {
                File.AppendAllText(_logPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\n\n");
            }
            catch { /* best effort */ }
        }
    }

    public void Dispose() => Stop();
}
