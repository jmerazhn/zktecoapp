using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZKTecoManager.Data;
using ZKTecoManager.Models.Entities;
using ZKTecoManager.Models.Enums;

namespace ZKTecoManager.Services;

public class AttendanceService : IAttendanceService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public AttendanceService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    // ── Process unprocessed logs into AttendanceRecord ────────────────────────

    public async Task<ProcessResult> ProcessLogsAsync(
        DateOnly from, DateOnly to, int? companyId = null, CancellationToken ct = default)
    {
        return await Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var fromDt = from.ToDateTime(TimeOnly.MinValue);
            var toDt   = to.ToDateTime(new TimeOnly(23, 59, 59));

            // Load unprocessed logs with employee info
            var logsQuery = db.AttendanceLogs
                .Where(l => !l.IsProcessed
                         && l.PunchTime >= fromDt
                         && l.PunchTime <= toDt
                         && l.EmployeeId != null)
                .Include(l => l.Employee)
                    .ThenInclude(e => e!.Department)
                .AsQueryable();

            if (companyId.HasValue)
                logsQuery = logsQuery.Where(l => l.Employee!.CompanyId == companyId.Value);

            var logs = await logsQuery.OrderBy(l => l.PunchTime).ToListAsync(ct);

            if (logs.Count == 0)
                return new ProcessResult(0, 0, 0, 0, null);

            // Load all employee shifts for affected employees
            var empIds = logs.Select(l => l.EmployeeId!.Value).Distinct().ToList();
            var allShifts = await db.EmployeeShifts
                .Include(es => es.Shift)
                .Where(es => empIds.Contains(es.EmployeeId))
                .AsNoTracking()
                .ToListAsync(ct);

            // ── Assign work date to each log ──────────────────────────────────
            var tagged = logs.Select(l =>
            {
                var shift = GetShiftForDate(allShifts, l.EmployeeId!.Value,
                    DateOnly.FromDateTime(l.PunchTime));
                var workDate = GetWorkDate(l.PunchTime, shift);
                return (Log: l, WorkDate: workDate, Shift: shift);
            }).ToList();

            // ── Group by employee + work date ─────────────────────────────────
            var groups = tagged.GroupBy(x => (x.Log.EmployeeId!.Value, x.WorkDate));

            int newCount = 0, updCount = 0, errCount = 0;

            foreach (var group in groups)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var (empId, workDate) = group.Key;
                    var punches = group.OrderBy(x => x.Log.PunchTime).ToList();
                    var shift   = punches.First().Shift;

                    var checkIn  = punches.First().Log.PunchTime;
                    var checkOut = punches.Count > 1
                        ? punches.Last().Log.PunchTime
                        : (DateTime?)null;

                    var (hoursWorked, lateMinutes, overtimeHours, status) =
                        CalculateMetrics(checkIn, checkOut, shift);

                    // Upsert
                    var existing = await db.AttendanceRecords
                        .FirstOrDefaultAsync(r => r.EmployeeId == empId && r.WorkDate == workDate, ct);

                    if (existing is null)
                    {
                        db.AttendanceRecords.Add(new AttendanceRecord
                        {
                            EmployeeId    = empId,
                            WorkDate      = workDate,
                            ShiftId       = shift?.Id,
                            CheckIn       = checkIn,
                            CheckOut      = checkOut,
                            HoursWorked   = hoursWorked,
                            LateMinutes   = lateMinutes,
                            OvertimeHours = overtimeHours,
                            Status        = status,
                            ProcessedAt   = DateTime.UtcNow,
                            CreatedAt     = DateTime.UtcNow,
                            UpdatedAt     = DateTime.UtcNow
                        });
                        newCount++;
                    }
                    else if (existing.Status == AttendanceStatus.Pending ||
                             existing.Status == AttendanceStatus.Normal  ||
                             existing.Status == AttendanceStatus.Late)
                    {
                        // Only overwrite auto-calculated statuses, not manual ones
                        existing.ShiftId       = shift?.Id;
                        existing.CheckIn       = checkIn;
                        existing.CheckOut      = checkOut;
                        existing.HoursWorked   = hoursWorked;
                        existing.LateMinutes   = lateMinutes;
                        existing.OvertimeHours = overtimeHours;
                        existing.Status        = status;
                        existing.ProcessedAt   = DateTime.UtcNow;
                        existing.UpdatedAt     = DateTime.UtcNow;
                        updCount++;
                    }

                    // Mark logs as processed
                    var logIds = punches.Select(p => p.Log.Id).ToList();
                    await db.AttendanceLogs
                        .Where(l => logIds.Contains(l.Id))
                        .ExecuteUpdateAsync(s => s.SetProperty(l => l.IsProcessed, true), ct);
                }
                catch (Exception ex)
                {
                    errCount++;
                    // Continue processing other groups
                    _ = ex;
                }
            }

            await db.SaveChangesAsync(ct);
            return new ProcessResult(logs.Count, newCount, updCount, errCount, null);
        }, ct);
    }

    // ── Query attendance records ──────────────────────────────────────────────

    public async Task<IReadOnlyList<AttendanceRecord>> GetRecordsAsync(
        AttendanceFilter filter, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var query = db.AttendanceRecords
            .Include(r => r.Employee).ThenInclude(e => e.Department)
            .Include(r => r.Shift)
            .Where(r => r.WorkDate >= filter.From && r.WorkDate <= filter.To)
            .AsQueryable();

        if (filter.CompanyId.HasValue)
            query = query.Where(r => r.Employee.CompanyId == filter.CompanyId.Value);

        if (filter.DepartmentId.HasValue)
            query = query.Where(r => r.Employee.DepartmentId == filter.DepartmentId.Value);

        if (filter.EmployeeId.HasValue)
            query = query.Where(r => r.EmployeeId == filter.EmployeeId.Value);

        return await query
            .OrderBy(r => r.WorkDate)
            .ThenBy(r => r.Employee.LastName)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task UpdateRecordAsync(
        int recordId, AttendanceStatus status, string? notes, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var record = await db.AttendanceRecords.FindAsync(new object[] { recordId }, ct);
        if (record is null) return;

        record.Status    = status;
        record.Notes     = notes;
        record.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    // ── Calculation helpers ───────────────────────────────────────────────────

    private static (decimal? hours, int lateMin, decimal overtime, AttendanceStatus status)
        CalculateMetrics(DateTime checkIn, DateTime? checkOut, WorkShift? shift)
    {
        if (checkOut is null)
            return (null, 0, 0, AttendanceStatus.Pending);

        var duration = checkOut.Value - checkIn;
        var hoursWorked = (decimal)Math.Round(duration.TotalHours, 2);

        if (shift is null)
            return (hoursWorked, 0, 0, AttendanceStatus.Normal);

        // Late minutes (respect tolerance)
        var shiftStart   = shift.StartTime.ToTimeSpan();
        var tolerance    = TimeSpan.FromMinutes(shift.ToleranceMinutes);
        var checkInTime  = checkIn.TimeOfDay;
        var lateSpan     = checkInTime - shiftStart - tolerance;
        var lateMinutes  = lateSpan > TimeSpan.Zero ? (int)lateSpan.TotalMinutes : 0;

        // Overtime beyond shift duration
        var shiftDuration  = GetShiftDuration(shift);
        var overtimeHours  = Math.Max(0m, hoursWorked - (decimal)shiftDuration.TotalHours);
        overtimeHours      = Math.Round(overtimeHours, 2);

        var status = lateMinutes > 0 ? AttendanceStatus.Late : AttendanceStatus.Normal;
        return (hoursWorked, lateMinutes, overtimeHours, status);
    }

    private static TimeSpan GetShiftDuration(WorkShift shift)
    {
        if (!shift.IsNightShift)
            return shift.EndTime.ToTimeSpan() - shift.StartTime.ToTimeSpan();

        // Night shift crosses midnight
        return TimeSpan.FromHours(24) - shift.StartTime.ToTimeSpan() + shift.EndTime.ToTimeSpan();
    }

    private static DateOnly GetWorkDate(DateTime punchTime, WorkShift? shift)
    {
        // Night shift: early-morning punches belong to the previous work day
        if (shift is { IsNightShift: true })
        {
            if (punchTime.TimeOfDay <= shift.EndTime.ToTimeSpan())
                return DateOnly.FromDateTime(punchTime.Date.AddDays(-1));
        }
        return DateOnly.FromDateTime(punchTime.Date);
    }

    private static WorkShift? GetShiftForDate(
        List<EmployeeShift> allShifts, int employeeId, DateOnly date)
    {
        return allShifts
            .Where(es => es.EmployeeId == employeeId
                      && es.EffectiveFrom <= date
                      && (es.EffectiveTo == null || es.EffectiveTo >= date))
            .OrderByDescending(es => es.EffectiveFrom)
            .FirstOrDefault()?.Shift;
    }
}
