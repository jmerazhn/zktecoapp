using ZKTecoManager.Models.Enums;

namespace ZKTecoManager.Models.Entities;

public class Incident
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public DateOnly IncidentDate { get; set; }
    public IncidentType IncidentType { get; set; }
    public string? Description { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public Employee Employee { get; set; } = null!;
}
