namespace ZKTecoManager.Models.Entities;

public class EmployeeShift
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public int ShiftId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }

    public Employee Employee { get; set; } = null!;
    public WorkShift Shift { get; set; } = null!;
}
