namespace ZKTecoManager.Models.Entities;

public class Device
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; } = 4370;
    public string? CommPassword { get; set; }
    public string? SerialNumber { get; set; }
    public string? Model { get; set; }
    public string? Location { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastSync { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Company Company { get; set; } = null!;
    public ICollection<DeviceUser> DeviceUsers { get; set; } = new List<DeviceUser>();
    public ICollection<AttendanceLog> AttendanceLogs { get; set; } = new List<AttendanceLog>();
}
