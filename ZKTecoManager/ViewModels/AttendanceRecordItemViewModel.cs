using System.Windows.Media;
using ZKTecoManager.Models.Entities;
using ZKTecoManager.Models.Enums;

namespace ZKTecoManager.ViewModels;

public class AttendanceRecordItemViewModel
{
    public AttendanceRecord Record { get; }

    public AttendanceRecordItemViewModel(AttendanceRecord record)
    {
        Record = record;
    }

    public int    Id             => Record.Id;
    public string DateDisplay    => Record.WorkDate.ToString("dd/MM/yyyy");
    public string DayOfWeek      => Record.WorkDate.ToDateTime(TimeOnly.MinValue).ToString("ddd");
    public string EmployeeCode   => Record.Employee.EmployeeCode;
    public string EmployeeName   => Record.Employee.FullName;
    public string? Department    => Record.Employee.Department?.Name;
    public string? ShiftName     => Record.Shift?.Name;
    public string CheckInDisplay => Record.CheckIn.HasValue
                                    ? Record.CheckIn.Value.ToLocalTime().ToString("HH:mm") : "—";
    public string CheckOutDisplay=> Record.CheckOut.HasValue
                                    ? Record.CheckOut.Value.ToLocalTime().ToString("HH:mm") : "—";
    public string HoursDisplay   => Record.HoursWorked.HasValue
                                    ? $"{Record.HoursWorked:F1} h" : "—";
    public string LateDisplay    => Record.LateMinutes > 0
                                    ? $"{Record.LateMinutes} min" : "—";
    public string OvertimeDisplay=> Record.OvertimeHours > 0
                                    ? $"{Record.OvertimeHours:F1} h" : "—";
    public string? Notes         => Record.Notes;

    public string StatusLabel => Record.Status switch
    {
        AttendanceStatus.Normal    => "Normal",
        AttendanceStatus.Late      => "Tardanza",
        AttendanceStatus.Absent    => "Falta",
        AttendanceStatus.Justified => "Justificado",
        AttendanceStatus.DayOff    => "Descanso",
        _                          => "Pendiente"
    };

    public Brush StatusBrush => Record.Status switch
    {
        AttendanceStatus.Normal    => new SolidColorBrush(Color.FromRgb(34,  197, 94)),
        AttendanceStatus.Late      => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
        AttendanceStatus.Absent    => new SolidColorBrush(Color.FromRgb(239, 68,  68)),
        AttendanceStatus.Justified => new SolidColorBrush(Color.FromRgb(59,  130, 246)),
        AttendanceStatus.DayOff    => new SolidColorBrush(Color.FromRgb(156, 163, 175)),
        _                          => new SolidColorBrush(Color.FromRgb(209, 213, 219))
    };

    public double RowOpacity => Record.Status == AttendanceStatus.Absent ? 0.7 : 1.0;
}
