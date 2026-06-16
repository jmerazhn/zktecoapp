using System.Text;
using ZKTecoManager.Models.Entities;
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

    public string IpAddress { get; }
    public int Port { get; }
    public bool IsConnected => _handle != IntPtr.Zero;

    public ZKTecoDevice(string ipAddress, int port)
    {
        IpAddress = ipAddress;
        Port = port;
    }

    // ── Connection ────────────────────────────────────────────────────────────

    public bool Connect(int timeout = 4000, string password = "")
    {
        lock (_lock)
        {
            if (_handle != IntPtr.Zero) return true;
            var connStr = $"protocol=TCP,ipaddress={IpAddress},port={Port},timeout={timeout},passwd={password}";
            _handle = ZKTecoSdk.Connect(connStr);
            return _handle != IntPtr.Zero;
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
            byte[] buf = new byte[512];
            int result = ZKTecoSdk.GetDeviceParam(_handle, ref buf[0], buf.Length, paramName);
            if (result < 0) return null;
            return Encoding.ASCII.GetString(buf).TrimEnd('\0');
        }
    }

    public DateTime? GetDeviceTime()
    {
        lock (_lock)
        {
            EnsureConnected();
            int year = 0, month = 0, day = 0, hour = 0, minute = 0, second = 0;
            int r = ZKTecoSdk.GetDeviceTime(_handle, ref year, ref month, ref day, ref hour, ref minute, ref second);
            if (r < 0) return null;
            return new DateTime(year, month, day, hour, minute, second);
        }
    }

    public bool SyncTime()
    {
        lock (_lock)
        {
            EnsureConnected();
            var now = DateTime.Now;
            return ZKTecoSdk.SetDeviceTime2(_handle, now.Year, now.Month, now.Day,
                now.Hour, now.Minute, now.Second) >= 0;
        }
    }

    // ── Attendance logs ───────────────────────────────────────────────────────

    /// <summary>Descarga todos los logs de asistencia del reloj.</summary>
    public List<AttendanceRawLog> DownloadLogs()
    {
        lock (_lock)
        {
            EnsureConnected();
            byte[] buf = new byte[1024 * 1024 * 4]; // 4 MB
            int size = 0;
            int result = ZKTecoSdk.ReadGeneralLogData(_handle, ref buf[0], buf.Length, ref size);
            if (result < 0 || size == 0) return new();

            var raw = Encoding.ASCII.GetString(buf, 0, size);
            return ParseLogs(raw);
        }
    }

    public bool ClearLogs()
    {
        lock (_lock)
        {
            EnsureConnected();
            return ZKTecoSdk.ClearGLog(_handle) >= 0;
        }
    }

    // ── Users ─────────────────────────────────────────────────────────────────

    public bool SetUser(string pin, string name, string password = "", int privilege = 0)
    {
        lock (_lock)
        {
            EnsureConnected();
            return ZKTecoSdk.SSR_SetUserInfo(_handle, pin, name, password, privilege, true) >= 0;
        }
    }

    public bool DeleteUser(string pin)
    {
        lock (_lock)
        {
            EnsureConnected();
            return ZKTecoSdk.DeleteUser(_handle, 1, pin, 0) >= 0;
        }
    }

    public bool SetCard(string pin, string cardNumber)
    {
        lock (_lock)
        {
            EnsureConnected();
            int errCount = 0;
            ZKTecoSdk.BeginBatchWrite(_handle);
            ZKTecoSdk.BatchWriteCard(_handle, pin, cardNumber, 1);
            return ZKTecoSdk.EndBatchWrite(_handle, ref errCount) >= 0 && errCount == 0;
        }
    }

    // ── Parsing ───────────────────────────────────────────────────────────────

    private static List<AttendanceRawLog> ParseLogs(string raw)
    {
        var logs = new List<AttendanceRawLog>();
        // Pull SDK format: PIN\tDateTime\tVerifyMethod\tPunchType\tWorkCode\r\n
        foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            var parts = trimmed.Split('\t');
            if (parts.Length < 4) continue;

            if (!DateTime.TryParse(parts[1], out var dt)) continue;
            if (!byte.TryParse(parts[2], out var vm)) continue;
            if (!byte.TryParse(parts[3], out var pt)) continue;

            logs.Add(new AttendanceRawLog
            {
                Pin = parts[0],
                PunchTime = dt,
                VerifyMethod = (VerifyMethod)vm,
                PunchType = (PunchType)pt,
                WorkCode = parts.Length > 4 ? parts[4] : null,
                Raw = trimmed
            });
        }
        return logs;
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
