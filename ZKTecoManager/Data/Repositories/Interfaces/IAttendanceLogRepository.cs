using ZKTecoManager.Models.Entities;

namespace ZKTecoManager.Data.Repositories.Interfaces;

public interface IAttendanceLogRepository : IRepository<AttendanceLog>
{
    Task<IReadOnlyList<AttendanceLog>> GetByDeviceAndDateRangeAsync(int deviceId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<IReadOnlyList<AttendanceLog>> GetUnprocessedAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AttendanceLog>> GetByEmployeeAsync(int employeeId, DateTime from, DateTime to, CancellationToken ct = default);
    Task BulkInsertAsync(IEnumerable<AttendanceLog> logs, CancellationToken ct = default);
    Task MarkAsProcessedAsync(IEnumerable<long> ids, CancellationToken ct = default);
}
