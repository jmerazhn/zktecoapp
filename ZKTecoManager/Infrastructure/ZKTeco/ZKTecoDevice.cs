using System.Text;
using ZKTecoManager.Models.Enums;

namespace ZKTecoManager.Infrastructure.ZKTeco;

public class AttendanceRawLog
{
    public string Pin { get; init; } = string.Empty;
    public DateTime PunchTime { get; init; }
    public PunchType PunchType { get; init; }
    public VerifyMethod VerifyMethod { get; init; }
    public string? WorkCode { get; init; }
    public string Raw { get; init; } = string.Empty;
}

public class ZKTecoDevice : IDisposable
{
    private IntPtr _handle = IntPtr.Zero;
    private readonly object _lock = new();
    private bool _disposed;

    public string IpAddress  { get; }
    public int    Port       { get; }
    public int    LastError  { get; private set; }
    public bool   IsConnected => _handle != IntPtr.Zero;

    public ZKTecoDevice(string ipAddress, int port)
    {
        IpAddress = ipAddress;
        Port      = port;
    }

    // ── Connection ────────────────────────────────────────────────────────────

    public bool Connect(int timeout = 4000, string password = "")
    {
        lock (_lock)
        {
            if (_handle != IntPtr.Zero) return true;

            var connStr = $"protocol=TCP,ipaddress={IpAddress},port={Port}," +
                          $"timeout={timeout},passwd={password}";

            _handle = ZKTecoSdk.Connect(connStr);

            if (_handle == IntPtr.Zero)
            {
                LastError = ZKTecoSdk.PullLastError();
                return false;
            }

            LastError = 0;
            return true;
        }
    }

    public void Disconnect()
    {
        lock (_lock)
        {
            if (_handle == IntPtr.Zero) return;
            ZKTecoSdk.Disconnect(_handle);
            _handle = IntPtr.Zero;
        }
    }

    // ── Device info ───────────────────────────────────────────────────────────

    public string? GetParam(string paramName)
    {
        lock (_lock)
        {
            EnsureConnected();
            var buf = new byte[1024];
            int ret = ZKTecoSdk.GetDeviceParam(_handle, ref buf[0], buf.Length, paramName);
            if (ret < 0) return null;
            int end = Array.IndexOf(buf, (byte)0);
            var raw = Encoding.UTF8.GetString(buf, 0, end < 0 ? buf.Length : end);

            // El SDK devuelve "paramName=valor" (igual que GetDeviceData), no solo el valor.
            var prefix = paramName + "=";
            return raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? raw[prefix.Length..] : raw;
        }
    }

    public DateTime? GetDeviceTime()
    {
        var raw = GetParam("DateTime");
        if (raw is null) return null;
        return DateTime.TryParse(raw, out var dt) ? dt : null;
    }

    public bool SyncTime()
    {
        lock (_lock)
        {
            EnsureConnected();
            var now = DateTime.Now;
            int ret = ZKTecoSdk.SetDeviceParam(_handle,
                $"DateTime={now:yyyy-MM-dd HH:mm:ss}");
            return ret >= 0;
        }
    }

    // ── Attendance logs ───────────────────────────────────────────────────────

    public List<AttendanceRawLog> DownloadLogs()
    {
        lock (_lock)
        {
            EnsureConnected();
            var buf = new byte[32 * 1024 * 1024];
            int ret = ZKTecoSdk.GetDeviceData(_handle, ref buf[0], buf.Length,
                "attlog", "Pin,Time,Status,Verify,WorkCode", "", "");

            if (ret < 0) return new();

            var raw = Encoding.UTF8.GetString(buf, 0, ret);
            return ParseLogs(raw);
        }
    }

    public bool ClearLogs()
    {
        lock (_lock)
        {
            EnsureConnected();
            return ZKTecoSdk.DeleteDeviceData(_handle, "attlog", "", "") >= 0;
        }
    }

    // ── Users ─────────────────────────────────────────────────────────────────

    public int LastReturnCode { get; private set; }

    public bool SetUser(string pin, string name, string password = "", int privilege = 0)
    {
        lock (_lock)
        {
            EnsureConnected();
            string data = $"Pin={pin}\tName={name}\tPri={privilege}\t" +
                          $"Passwd={password}\tCard=\tGrp=1\tTZ=1\tVerify=0";
            int ret = ZKTecoSdk.SetDeviceData(_handle, "user", data, "");
            LastReturnCode = ret;
            if (ret != 0) LastError = ZKTecoSdk.PullLastError();
            return ret == 0;
        }
    }

    public bool DeleteUser(string pin)
    {
        lock (_lock)
        {
            EnsureConnected();
            int ret = ZKTecoSdk.DeleteDeviceData(_handle, "user", $"Pin={pin}", "");
            LastReturnCode = ret;
            if (ret < 0) LastError = ZKTecoSdk.PullLastError();
            return ret >= 0;
        }
    }

    public bool SetCard(string pin, string cardNumber)
    {
        lock (_lock)
        {
            EnsureConnected();
            string data = $"Pin={pin}\tCard={cardNumber}";
            int ret = ZKTecoSdk.SetDeviceData(_handle, "user", data, "");
            LastReturnCode = ret;
            if (ret != 0) LastError = ZKTecoSdk.PullLastError();
            return ret == 0;
        }
    }

    // ── Parsing ───────────────────────────────────────────────────────────────

    private static List<AttendanceRawLog> ParseLogs(string raw)
    {
        var logs = new List<AttendanceRawLog>();

        foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // GetDeviceData returns key=value\tkey=value per line
            var fields = ParseFields(trimmed);

            if (!fields.TryGetValue("Pin", out var pin) ||
                !fields.TryGetValue("Time", out var timeStr) ||
                !DateTime.TryParse(timeStr, out var dt))
                continue;

            fields.TryGetValue("Status",   out var statusStr);
            fields.TryGetValue("Verify",   out var verifyStr);
            fields.TryGetValue("WorkCode", out var workCode);

            byte.TryParse(statusStr, out var statusByte);
            byte.TryParse(verifyStr, out var verifyByte);

            logs.Add(new AttendanceRawLog
            {
                Pin          = pin,
                PunchTime    = dt,
                PunchType    = (PunchType)statusByte,
                VerifyMethod = (VerifyMethod)verifyByte,
                WorkCode     = workCode,
                Raw          = trimmed
            });
        }

        return logs;
    }

    private static Dictionary<string, string> ParseFields(string line)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in line.Split('\t'))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2)
                dict[kv[0].Trim()] = kv[1].Trim();
        }
        return dict;
    }

    private void EnsureConnected()
    {
        if (_handle == IntPtr.Zero)
            throw new InvalidOperationException("Device is not connected.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        Disconnect();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
