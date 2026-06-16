using Microsoft.EntityFrameworkCore;
using ZKTecoManager.Data.Repositories.Interfaces;
using ZKTecoManager.Models.Entities;

namespace ZKTecoManager.Data.Repositories;

public class DeviceRepository : Repository<Device>, IDeviceRepository
{
    public DeviceRepository(AppDbContext db) : base(db) { }

    public async Task<IReadOnlyList<Device>> GetByCompanyAsync(int companyId, CancellationToken ct = default)
        => await _set.AsNoTracking()
                     .Where(d => d.CompanyId == companyId && d.IsActive)
                     .OrderBy(d => d.Name)
                     .ToListAsync(ct);

    public async Task<Device?> GetByIpAsync(string ipAddress, int port, CancellationToken ct = default)
        => await _set.AsNoTracking()
                     .FirstOrDefaultAsync(d => d.IpAddress == ipAddress && d.Port == port, ct);

    public async Task UpdateLastSyncAsync(int deviceId, DateTime syncTime, CancellationToken ct = default)
    {
        await _set.Where(d => d.Id == deviceId)
                  .ExecuteUpdateAsync(s => s.SetProperty(d => d.LastSync, syncTime), ct);
    }
}
