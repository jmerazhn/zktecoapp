namespace ZKTecoManager.Models.Enums;

public enum AttendanceStatus : byte
{
    Pending = 0,
    Normal = 1,
    Late = 2,
    Absent = 3,
    Justified = 4,
    DayOff = 5
}
