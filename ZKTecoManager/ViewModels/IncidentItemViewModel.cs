using ZKTecoManager.Models.Entities;
using ZKTecoManager.Models.Enums;

namespace ZKTecoManager.ViewModels;

public class IncidentItemViewModel
{
    public Incident Incident { get; }

    public IncidentItemViewModel(Incident incident) => Incident = incident;

    public int     Id              => Incident.Id;
    public string  DateDisplay     => Incident.IncidentDate.ToString("dd/MM/yyyy");
    public string  EmployeeCode    => Incident.Employee.EmployeeCode;
    public string  EmployeeName    => Incident.Employee.FullName;
    public string? Department      => Incident.Employee.Department?.Name;
    public string  TypeLabel       => Incident.IncidentType switch
    {
        IncidentType.Permission       => "Permiso",
        IncidentType.Illness          => "Enfermedad",
        IncidentType.Vacation         => "Vacaciones",
        IncidentType.JustifiedLate    => "Tardanza justificada",
        IncidentType.JustifiedAbsence => "Falta justificada",
        IncidentType.CompensatoryDay  => "Día compensatorio",
        _                             => "Otro"
    };
    public string? Description     => Incident.Description;
    public string  ApprovedLabel   => Incident.ApprovedAt.HasValue
                                      ? $"✔ {Incident.ApprovedBy}" : "Pendiente";
    public bool    IsApproved      => Incident.ApprovedAt.HasValue;
}
