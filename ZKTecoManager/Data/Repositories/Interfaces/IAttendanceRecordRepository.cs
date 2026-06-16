using ZKTecoManager.Models.Entities;
using ZKTecoManager.Models.Enums;

namespace ZKTecoManager.Data.Repositories.Interfaces;

public interface IAttendanceRecordRepository : IRepository<AttendanceRecord>
{
    Task<IReadOnlyList<AttendanceRecord>> GetByEmployeeAndMonthAsync(int employeeId, int year, int month, CancellationToken ct = default);
    Task<IReadOnlyList<AttendanceRecord>> GetByDateRangeAsync(int companyId, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<AttendanceRecord?> GetByEmployeeAndDateAsync(int employeeId, DateOnly date, CancellationToken ct = default);
    Task UpsertAsync(AttendanceRecord record, CancellationToken ct = default);
}
