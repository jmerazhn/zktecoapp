namespace ZKTecoManager.Models.Entities;

public class DeviceUser
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public int EmployeeId { get; set; }
    public string PinOnDevice { get; set; } = string.Empty;
    public string? CardNumber { get; set; }
    public DateTime? SyncedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public Device Device { get; set; } = null!;
    public Employee Employee { get; set; } = null!;
}
