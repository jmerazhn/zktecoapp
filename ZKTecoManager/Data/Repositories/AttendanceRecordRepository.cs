using Microsoft.EntityFrameworkCore;
using ZKTecoManager.Data.Repositories.Interfaces;
using ZKTecoManager.Models.Entities;

namespace ZKTecoManager.Data.Repositories;

public class AttendanceRecordRepository : Repository<AttendanceRecord>, IAttendanceRecordRepository
{
    public AttendanceRecordRepository(AppDbContext db) : base(db) { }

    public async Task<IReadOnlyList<AttendanceRecord>> GetByEmployeeAndMonthAsync(
        int employeeId, int year, int month, CancellationToken ct = default)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        return await _set.AsNoTracking()
                         .Include(r => r.Shift)
                         .Where(r => r.EmployeeId == employeeId && r.WorkDate >= from && r.WorkDate <= to)
                         .OrderBy(r => r.WorkDate)
                         .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AttendanceRecord>> GetByDateRangeAsync(
        int companyId, DateOnly from, DateOnly to, CancellationToken ct = default)
        => await _set.AsNoTracking()
                     .Include(r => r.Employee)
                     .Include(r => r.Shift)
                     .Where(r => r.Employee.CompanyId == companyId && r.WorkDate >= from && r.WorkDate <= to)
                     .OrderBy(r => r.WorkDate).ThenBy(r => r.Employee.LastName)
                     .ToListAsync(ct);

    public async Task<AttendanceRecord?> GetByEmployeeAndDateAsync(
        int employeeId, DateOnly date, CancellationToken ct = default)
        => await _set.FirstOrDefaultAsync(r => r.EmployeeId == employeeId && r.WorkDate == date, ct);

    public async Task UpsertAsync(AttendanceRecord record, CancellationToken ct = default)
    {
        var existing = await GetByEmployeeAndDateAsync(record.EmployeeId, record.WorkDate, ct);
        if (existing is null)
        {
            await _set.AddAsync(record, ct);
        }
        else
        {
            existing.CheckIn = record.CheckIn;
            existing.CheckOut = record.CheckOut;
            existing.HoursWorked = record.HoursWorked;
            existing.OvertimeHours = record.OvertimeHours;
            existing.LateMinutes = record.LateMinutes;
            existing.Status = record.Status;
            existing.ShiftId = record.ShiftId;
            existing.ProcessedAt = record.ProcessedAt;
            existing.UpdatedAt = DateTime.UtcNow;
            _set.Update(existing);
        }
        await _db.SaveChangesAsync(ct);
    }
}
