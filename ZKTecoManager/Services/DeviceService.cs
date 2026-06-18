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

        ConnectionTestResult? sdkResult = null;

        var sdkThread = new System.Threading.Thread(() =>
        {
            try
            {
                using var zk  = new ZKTecoDevice(device.IpAddress, device.Port);
                var storedPwd = device.CommPassword ?? string.Empty;
                var attempts  = new[] { storedPwd, "", "0" }.Distinct().ToArray();

                string? connectedWith = null;
                foreach (var pwd in attempts)
                {
                    if (zk.IsConnected) break;
                    zk.Connect(timeout: 4000, password: pwd);
                    if (zk.IsConnected) { connectedWith = pwd; break; }
                }

                if (zk.IsConnected)
                {
                    if (connectedWith != storedPwd)
                    {
                        var hint = connectedWith == "" ? "vacía" : $"\"{connectedWith}\"";
                        sdkResult = new ConnectionTestResult(true,
                            $"⚠ Conectado con contraseña {hint}.\n" +
                            "Actualice CommPassword en el formulario del dispositivo.");
                    }
                    else
                    {
                        sdkResult = new ConnectionTestResult(true, null);
                    }
                }
                else
                {
                    var sdkError = zk.LastError;
                    string detail = sdkError switch
                    {
                        -2   => "Error -2 = el reloj no respondió al comando de conexión " +
                                "(no es error de contraseña — ese es el código -14).\n" +
                                "Causas típicas: el firmware no soporta este protocolo del SDK, " +
                                "o algo en la red (router/firewall entre subredes) descarta la " +
                                "respuesta del reloj.\n" +
                                "Pruebe conectar desde un equipo en la misma subred que el reloj " +
                                "(192.168.4.x) para descartar un problema de ruteo.",
                        -14  => "Error -14 = contraseña incorrecta.\n" +
                                "Verifique: Menú → Comm → PC Connection Password",
                        -107 => "Error -107 = sesión rechazada por el dispositivo.\n" +
                                "Verifique: Menú → Comm → PC Connection → Server IP → 0.0.0.0",
                        _    => "Verifique: Menú → Comm → PC Connection → Server IP → 0.0.0.0"
                    };
                    sdkResult = new ConnectionTestResult(false,
                        $"SDK rechazó la sesión — PullLastError = {sdkError}\n" +
                        $"IP dispositivo: {device.IpAddress}:{device.Port}\n" +
                        $"IP de esta PC:  {GetLocalIp()}\n\n" + detail);
                }
            }
            catch (Exception ex)
            {
                sdkResult = new ConnectionTestResult(false, ex.Message);
            }
        });

        sdkThread.SetApartmentState(System.Threading.ApartmentState.STA);
        sdkThread.IsBackground = true;
        sdkThread.Start();

        bool finished = await Task.Run(() => sdkThread.Join(15_000), ct);

        if (!finished || sdkResult is null)
            return new ConnectionTestResult(false,
                $"Timeout (15 s): el SDK no respondió.\n" +
                $"IP dispositivo: {device.IpAddress}:{device.Port}");

        return sdkResult;
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

    public async Task<(bool Success, int ErrorCode)> SetUserOnDeviceAsync(Device device, string pin, string name, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var zk = GetOrCreate(device);
            if (!zk.IsConnected && !zk.Connect(timeout: 4000, password: device.CommPassword ?? string.Empty))
                return (false, zk.LastError);
            return (zk.SetUser(pin, name), zk.LastReturnCode);
        }, ct);
    }

    public async Task<(bool Success, int ErrorCode)> SetCardOnDeviceAsync(Device device, string pin, string cardNumber, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var zk = GetOrCreate(device);
            if (!zk.IsConnected && !zk.Connect(timeout: 4000, password: device.CommPassword ?? string.Empty))
                return (false, zk.LastError);
            return (zk.SetCard(pin, cardNumber), zk.LastReturnCode);
        }, ct);
    }

    public async Task<(bool Success, int ErrorCode)> DeleteUserFromDeviceAsync(Device device, string pin, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var zk = GetOrCreate(device);
            if (!zk.IsConnected && !zk.Connect(timeout: 4000, password: device.CommPassword ?? string.Empty))
                return (false, zk.LastError);
            return (zk.DeleteUser(pin), zk.LastReturnCode);
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
