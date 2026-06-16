using CommunityToolkit.Mvvm.ComponentModel;
using ZKTecoManager.Models.Entities;

namespace ZKTecoManager.ViewModels;

public partial class EmployeeItemViewModel : ObservableObject
{
    public Employee Employee { get; }

    [ObservableProperty] private string _currentShift = "Sin turno";
    [ObservableProperty] private int _enrolledDevices;

    public EmployeeItemViewModel(Employee employee)
    {
        Employee = employee;
    }

    public int Id               => Employee.Id;
    public string Code          => Employee.EmployeeCode;
    public string FullName      => Employee.FullName;
    public string FirstName     => Employee.FirstName;
    public string LastName      => Employee.LastName;
    public string? Department   => Employee.Department?.Name;
    public string? Position     => Employee.Position;
    public string? CardNumber   => Employee.CardNumber;
    public bool IsActive        => Employee.IsActive;
    public string ActiveLabel   => IsActive ? "Activo" : "Inactivo";
    public string CardDisplay   => string.IsNullOrEmpty(Employee.CardNumber) ? "—" : Employee.CardNumber;
    public string HireDate      => Employee.HireDate.HasValue
                                   ? Employee.HireDate.Value.ToString("dd/MM/yyyy") : "—";
    public string DevicesDisplay => EnrolledDevices == 0 ? "—" : $"{EnrolledDevices} reloj(es)";
}
