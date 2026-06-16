using ZKTecoManager.Models.Enums;

namespace ZKTecoManager.Services;

public enum ReportType { AttendanceDetail, MonthlySummary }

public record ReportOptions(
    ReportType Type,
    int? CompanyId,
    int? DepartmentId,
    int? EmployeeId,
    DateOnly From,
    DateOnly To);

public interface IReportService
{
    Task<string> ExportExcelAsync(ReportOptions opts, string filePath, CancellationToken ct = default);
    Task<string> ExportPdfAsync(ReportOptions opts, string filePath, CancellationToken ct = default);
}
