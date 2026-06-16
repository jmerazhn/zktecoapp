using ZKTecoManager.Models.Entities;

namespace ZKTecoManager.Services;

public record DeviceSyncResult(bool Success, string? SerialNumber, string? Model, DateTime? DeviceTime, string? Error);
public record LogDownloadResult(bool Success, int NewLogs, string? Error);

public interface IDeviceService
{
    Task<bool> TestConnectionAsync(Device device, CancellationToken ct = default);
    Task<DeviceSyncResult> SyncDeviceInfoAsync(Device device, CancellationToken ct = default);
    Task<LogDownloadResult> DownloadLogsAsync(Device device, CancellationToken ct = default);
    bool IsConnected(int deviceId);
    void Disconnect(int deviceId);
    void DisconnectAll();
}
