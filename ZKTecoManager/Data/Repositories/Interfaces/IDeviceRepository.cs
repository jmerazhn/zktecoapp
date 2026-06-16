using ZKTecoManager.Models.Entities;

namespace ZKTecoManager.Data.Repositories.Interfaces;

public interface IDeviceRepository : IRepository<Device>
{
    Task<IReadOnlyList<Device>> GetByCompanyAsync(int companyId, CancellationToken ct = default);
    Task<Device?> GetByIpAsync(string ipAddress, int port, CancellationToken ct = default);
    Task UpdateLastSyncAsync(int deviceId, DateTime syncTime, CancellationToken ct = default);
}
