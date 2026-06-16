using ZKTecoManager.Models.Enums;

namespace ZKTecoManager.Models.Entities;

public class AttendanceRecord
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public DateOnly WorkDate { get; set; }
    public int? ShiftId { get; set; }
    public DateTime? CheckIn { get; set; }
    public DateTime? CheckOut { get; set; }
    public decimal? HoursWorked { get; set; }
    public decimal OvertimeHours { get; set; } = 0;
    public int LateMinutes { get; set; } = 0;
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Pending;
    public string? Notes { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Employee Employee { get; set; } = null!;
    public WorkShift? Shift { get; set; }
}
