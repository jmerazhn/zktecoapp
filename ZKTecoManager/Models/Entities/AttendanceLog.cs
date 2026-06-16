using ZKTecoManager.Models.Enums;

namespace ZKTecoManager.Models.Entities;

public class AttendanceLog
{
    public long Id { get; set; }
    public int DeviceId { get; set; }
    public int? EmployeeId { get; set; }
    public string PinOnDevice { get; set; } = string.Empty;
    public DateTime PunchTime { get; set; }
    public PunchType PunchType { get; set; }
    public VerifyMethod VerifyMethod { get; set; }
    public string? WorkCode { get; set; }
    public string? RawData { get; set; }
    public bool IsProcessed { get; set; } = false;
    public DateTime CreatedAt { get; set; }

    public Device Device { get; set; } = null!;
    public Employee? Employee { get; set; }
}
