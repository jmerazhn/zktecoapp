using Microsoft.EntityFrameworkCore;
using ZKTecoManager.Data.Repositories.Interfaces;
using ZKTecoManager.Models.Entities;

namespace ZKTecoManager.Data.Repositories;

public class AttendanceLogRepository : Repository<AttendanceLog>, IAttendanceLogRepository
{
    public AttendanceLogRepository(AppDbContext db) : base(db) { }

    public async Task<IReadOnlyList<AttendanceLog>> GetByDeviceAndDateRangeAsync(
        int deviceId, DateTime from, DateTime to, CancellationToken ct = default)
        => await _set.AsNoTracking()
                     .Where(l => l.DeviceId == deviceId && l.PunchTime >= from && l.PunchTime <= to)
                     .Include(l => l.Employee)
                     .OrderBy(l => l.PunchTime)
                     .ToListAsync(ct);

    public async Task<IReadOnlyList<AttendanceLog>> GetUnprocessedAsync(CancellationToken ct = default)
        => await _set.AsNoTracking()
                     .Where(l => !l.IsProcessed)
                     .Include(l => l.Employee)
                     .OrderBy(l => l.PunchTime)
                     .ToListAsync(ct);

    public async Task<IReadOnlyList<AttendanceLog>> GetByEmployeeAsync(
        int employeeId, DateTime from, DateTime to, CancellationToken ct = default)
        => await _set.AsNoTracking()
                     .Where(l => l.EmployeeId == employeeId && l.PunchTime >= from && l.PunchTime <= to)
                     .OrderBy(l => l.PunchTime)
                     .ToListAsync(ct);

    public async Task BulkInsertAsync(IEnumerable<AttendanceLog> logs, CancellationToken ct = default)
    {
        await _set.AddRangeAsync(logs, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkAsProcessedAsync(IEnumerable<long> ids, CancellationToken ct = default)
    {
        var idList = ids.ToList();
        await _set.Where(l => idList.Contains(l.Id))
                  .ExecuteUpdateAsync(s => s.SetProperty(l => l.IsProcessed, true), ct);
    }

    public override async Task<AttendanceLog?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _set.FindAsync(new object[] { (long)id }, ct);
}
