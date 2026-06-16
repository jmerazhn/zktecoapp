using ZKTecoManager.Models.Entities;

namespace ZKTecoManager.Services;

public record ProcessResult(int Processed, int NewRecords, int Updated, int Errors, string? ErrorDetail);

public record AttendanceFilter(
    int? CompanyId,
    int? DepartmentId,
    int? EmployeeId,
    DateOnly From,
    DateOnly To);

public interface IAttendanceService
{
    Task<ProcessResult> ProcessLogsAsync(
        DateOnly from, DateOnly to, int? companyId = null, CancellationToken ct = default);

    Task<IReadOnlyList<AttendanceRecord>> GetRecordsAsync(
        AttendanceFilter filter, CancellationToken ct = default);

    Task UpdateRecordAsync(
        int recordId, Models.Enums.AttendanceStatus status, string? notes, CancellationToken ct = default);
}
