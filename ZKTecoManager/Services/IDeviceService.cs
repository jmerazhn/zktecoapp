using ZKTecoManager.Models.Entities;

namespace ZKTecoManager.Services;

public record DeviceSyncResult(bool Success, string? SerialNumber, string? Model, DateTime? DeviceTime, string? Error);
public record LogDownloadResult(bool Success, int NewLogs, string? Error);
public record ConnectionTestResult(bool Connected, string? Error);

public interface IDeviceService
{
    Task<ConnectionTestResult> TestConnectionAsync(Device device, CancellationToken ct = default);
    Task<DeviceSyncResult> SyncDeviceInfoAsync(Device device, CancellationToken ct = default);
    Task<LogDownloadResult> DownloadLogsAsync(Device device, CancellationToken ct = default);
    Task<(bool Success, int ErrorCode)> SetUserOnDeviceAsync(Device device, string pin, string name, CancellationToken ct = default);
    Task<(bool Success, int ErrorCode)> SetCardOnDeviceAsync(Device device, string pin, string cardNumber, CancellationToken ct = default);
    Task<(bool Success, int ErrorCode)> DeleteUserFromDeviceAsync(Device device, string pin, CancellationToken ct = default);
    bool IsConnected(int deviceId);
    void Disconnect(int deviceId);
    void DisconnectAll();
}
