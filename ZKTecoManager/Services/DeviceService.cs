using Microsoft.Extensions.DependencyInjection;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using ZKTecoManager.Data;
using ZKTecoManager.Data.Repositories.Interfaces;
using ZKTecoManager.Infrastructure.ZKTeco;
using ZKTecoManager.Models.Entities;

namespace ZKTecoManager.Services;

public class DeviceService : IDeviceService, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Dictionary<int, ZKTecoDevice> _pool = new();
    private readonly object _poolLock = new();

    public DeviceService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task<ConnectionTestResult> TestConnectionAsync(Device device, CancellationToken ct = default)
    {
        // Step 1: ICMP ping — ¿llega el host?
        bool pingOk;
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(device.IpAddress, 1000);
            pingOk = reply.Status == IPStatus.Success;
        }
        catch { pingOk = false; }

        if (!pingOk)
            return new ConnectionTestResult(false,
                $"El dispositivo no responde en {device.IpAddress}.\n\n" +
                "Qué verificar:\n" +
                $"• Abra CMD y ejecute:  ping {device.IpAddress}\n" +
                "• Confirme la IP desde el menú del reloj:\n" +
                "  Menú → Comm → Ethernet → IP Address\n" +
                "• Algunos relojes requieren reinicio después de cambiar la IP\n" +
                "• Verifique que el reloj y la PC estén en la misma red/subred");

        // Step 2: TCP port — ¿responde el puerto?
        bool portOpen;
        try
        {
            using var tcp = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(1500);
            await tcp.ConnectAsync(device.IpAddress, device.Port, cts.Token);
            portOpen = true;
        }
        catch { portOpen = false; }

        if (!portOpen)
            return new ConnectionTestResult(false,
                $"El reloj responde al ping en {device.IpAddress} " +
                $"pero el puerto {device.Port} está bloqueado.\n\n" +
                "Qué verificar:\n" +
                $"• Firewall de Windows: permita el puerto {device.Port} TCP\n" +
                "• Firewall del router/switch entre la PC y el reloj\n" +
                $"• Confirme el puerto en el reloj (por defecto 4370):\n" +
                "  Menú → Comm → Ethernet → Port");

        // Step 2: SDK handshake — try stored password first, then empty password
        return await Task.Run(() =>
        {
            var zk = GetOrCreate(device);

            // First attempt: stored CommPassword
            if (!zk.IsConnected)
                zk.Connect(timeout: 4000, password: device.CommPassword ?? string.Empty);

            if (zk.IsConnected)
                return new ConnectionTestResult(true, null);

            // Second attempt: if password was non-empty, retry with empty (device default)
            var storedPwd = device.CommPassword ?? string.Empty;
            if (storedPwd.Length > 0)
            {
                zk.Connect(timeout: 4000, password: string.Empty);
                if (zk.IsConnected)
                    return new ConnectionTestResult(true,
                        "⚠ Conectado con contraseña vacía. " +
                        "La contraseña guardada en BD no coincide con el dispositivo. " +
                        "Edite el dispositivo y deje CommPassword en blanco.");
            }

            var pwdHint = storedPwd.Length == 0
                ? "vacía"
                : $"{storedPwd.Length} caracteres";

            var localIp = GetLocalIp();

            return new ConnectionTestResult(false,
                $"Puerto {device.Port} responde en {device.IpAddress} " +
                $"pero el SDK rechazó la sesión.\n\n" +
                $"IP de esta PC: {localIp}\n" +
                $"Contraseña usada: {pwdHint}\n\n" +
                "Causas más comunes (en orden de probabilidad):\n\n" +
                "1. «Server IP» en el reloj no coincide con la IP de esta PC\n" +
                $"   Menú → Comm → PC Connection → Server IP\n" +
                $"   Cámbielo a 0.0.0.0 (cualquier PC) o a {localIp}\n\n" +
                "2. CommPassword incorrecto\n" +
                "   Menú → Comm → PC Connection Password\n" +
                "   Si dice 0 o vacío, deje el campo en blanco en la app\n\n" +
                "3. Otro software ZKTeco ocupa la conexión\n" +
                "   (ZKTime, ZKBioSecurity, ZKAccess)\n\n" +
                "4. Firmware incompatible con Pull SDK");
        }, ct);
    }

    public async Task<DeviceSyncResult> SyncDeviceInfoAsync(Device device, CancellationToken ct = default)
    {
        return await Task.Run(async () =>
        {
            var zk = GetOrCreate(device);
            if (!zk.IsConnected && !zk.Connect(timeout: 4000, password: device.CommPassword ?? string.Empty))
                return new DeviceSyncResult(false, null, null, null, "No se pudo conectar al dispositivo.");

            var serial = zk.GetParam("~SerialNumber");
            var model = zk.GetParam("~DeviceName");
            var time = zk.GetDeviceTime();

            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();

            var entity = await repo.GetByIdAsync(device.Id, ct);
            if (entity is not null)
            {
                if (serial is not null) entity.SerialNumber = serial;
                if (model is not null) entity.Model = model;
                entity.UpdatedAt = DateTime.UtcNow;
                await repo.UpdateAsync(entity, ct);
                await repo.SaveChangesAsync(ct);
            }

            return new DeviceSyncResult(true, serial, model, time, null);
        }, ct);
    }

    public async Task<LogDownloadResult> DownloadLogsAsync(Device device, CancellationToken ct = default)
    {
        return await Task.Run(async () =>
        {
            var zk = GetOrCreate(device);
            if (!zk.IsConnected && !zk.Connect(timeout: 4000, password: device.CommPassword ?? string.Empty))
                return new LogDownloadResult(false, 0, "No se pudo conectar al dispositivo.");

            List<AttendanceRawLog> rawLogs;
            try
            {
                rawLogs = zk.DownloadLogs();
            }
            catch (Exception ex)
            {
                return new LogDownloadResult(false, 0, ex.Message);
            }

            if (rawLogs.Count == 0)
                return new LogDownloadResult(true, 0, null);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Build PIN → EmployeeId map for this device
            var pinMap = db.DeviceUsers
                           .Where(du => du.DeviceId == device.Id)
                           .ToDictionary(du => du.PinOnDevice, du => du.EmployeeId);

            // Find latest existing log for deduplication
            var latestLog = db.AttendanceLogs
                              .Where(l => l.DeviceId == device.Id)
                              .OrderByDescending(l => l.PunchTime)
                              .Select(l => l.PunchTime)
                              .FirstOrDefault();

            var newLogs = rawLogs
                .Where(r => r.PunchTime > latestLog)
                .Select(r => new AttendanceLog
                {
                    DeviceId = device.Id,
                    EmployeeId = pinMap.TryGetValue(r.Pin, out var empId) ? empId : null,
                    PinOnDevice = r.Pin,
                    PunchTime = r.PunchTime,
                    PunchType = r.PunchType,
                    VerifyMethod = r.VerifyMethod,
                    WorkCode = r.WorkCode,
                    RawData = r.Raw,
                    IsProcessed = false,
                    CreatedAt = DateTime.UtcNow
                })
                .ToList();

            if (newLogs.Count > 0)
            {
                await db.AttendanceLogs.AddRangeAsync(newLogs, ct);

                // Update LastSync
                var deviceEntity = await db.Devices.FindAsync(new object[] { device.Id }, ct);
                if (deviceEntity is not null)
                    deviceEntity.LastSync = DateTime.UtcNow;

                await db.SaveChangesAsync(ct);
            }

            return new LogDownloadResult(true, newLogs.Count, null);
        }, ct);
    }

    public async Task<bool> SetUserOnDeviceAsync(Device device, string pin, string name, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var zk = GetOrCreate(device);
            if (!zk.IsConnected && !zk.Connect(timeout: 4000, password: device.CommPassword ?? string.Empty))
                return false;
            return zk.SetUser(pin, name);
        }, ct);
    }

    public async Task<bool> SetCardOnDeviceAsync(Device device, string pin, string cardNumber, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var zk = GetOrCreate(device);
            if (!zk.IsConnected && !zk.Connect(timeout: 4000, password: device.CommPassword ?? string.Empty))
                return false;
            return zk.SetCard(pin, cardNumber);
        }, ct);
    }

    public async Task<bool> DeleteUserFromDeviceAsync(Device device, string pin, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var zk = GetOrCreate(device);
            if (!zk.IsConnected && !zk.Connect(timeout: 4000, password: device.CommPassword ?? string.Empty))
                return false;
            return zk.DeleteUser(pin);
        }, ct);
    }

    public bool IsConnected(int deviceId)
    {
        lock (_poolLock)
            return _pool.TryGetValue(deviceId, out var zk) && zk.IsConnected;
    }

    public void Disconnect(int deviceId)
    {
        lock (_poolLock)
        {
            if (_pool.TryGetValue(deviceId, out var zk))
            {
                zk.Disconnect();
                _pool.Remove(deviceId);
            }
        }
    }

    public void DisconnectAll()
    {
        lock (_poolLock)
        {
            foreach (var zk in _pool.Values) zk.Dispose();
            _pool.Clear();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string GetLocalIp()
    {
        try
        {
            using var s = new System.Net.Sockets.Socket(
                System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Dgram, 0);
            s.Connect("8.8.8.8", 65530);
            return (s.LocalEndPoint as System.Net.IPEndPoint)?.Address.ToString() ?? "desconocida";
        }
        catch { return "desconocida"; }
    }

    private ZKTecoDevice GetOrCreate(Device device)
    {
        lock (_poolLock)
        {
            if (!_pool.TryGetValue(device.Id, out var zk))
            {
                zk = new ZKTecoDevice(device.IpAddress, device.Port);
                _pool[device.Id] = zk;
            }
            return zk;
        }
    }

    public void Dispose() => DisconnectAll();
}
