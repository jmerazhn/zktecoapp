using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows;
using ZKTecoManager.Models.Entities;
using ZKTecoManager.Models.Enums;

namespace ZKTecoManager.Views.Dialogs;

public record IncidentTypeItem(IncidentType Value, string Label);

public partial class IncidentFormDialogVM : ObservableObject
{
    public string                                DialogTitle    { get; }
    public ObservableCollection<Employee>        Employees      { get; }
    public ObservableCollection<IncidentTypeItem> IncidentTypes { get; }

    [ObservableProperty] private Employee?          _selectedEmployee;
    [ObservableProperty] private DateTime           _incidentDate = DateTime.Today;
    [ObservableProperty] private IncidentTypeItem?  _selectedIncidentType;
    [ObservableProperty] private string?            _description;

    public IncidentFormDialogVM(IEnumerable<Employee> employees, Incident? existing = null)
    {
        DialogTitle = existing is null ? "Nueva Incidencia" : "Editar Incidencia";

        Employees = new ObservableCollection<Employee>(employees);

        IncidentTypes = new ObservableCollection<IncidentTypeItem>
        {
            new(IncidentType.Permission,       "Permiso"),
            new(IncidentType.Illness,          "Enfermedad"),
            new(IncidentType.Vacation,         "Vacaciones"),
            new(IncidentType.JustifiedLate,    "Tardanza justificada"),
            new(IncidentType.JustifiedAbsence, "Falta justificada"),
            new(IncidentType.CompensatoryDay,  "Día compensatorio"),
            new(IncidentType.Other,            "Otro")
        };

        if (existing is not null)
        {
            SelectedEmployee      = Employees.FirstOrDefault(e => e.Id == existing.EmployeeId);
            IncidentDate          = existing.IncidentDate.ToDateTime(TimeOnly.MinValue);
            SelectedIncidentType  = IncidentTypes.FirstOrDefault(t => t.Value == existing.IncidentType);
            Description           = existing.Description;
        }
        else
        {
            SelectedIncidentType = IncidentTypes[0];
        }
    }

    public bool Validate() => SelectedEmployee is not null && SelectedIncidentType is not null;
}

public partial class IncidentFormDialog : Window
{
    private readonly IncidentFormDialogVM _vm;

    public Incident? Result { get; private set; }

    public IncidentFormDialog(IEnumerable<Employee> employees, Incident? existing = null)
    {
        _vm = new IncidentFormDialogVM(employees, existing);
        DataContext = _vm;
        InitializeComponent();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!_vm.Validate())
        {
            MessageBox.Show("Selecciona empleado y tipo de incidencia.", "Validación",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result = new Incident
        {
            EmployeeId   = _vm.SelectedEmployee!.Id,
            IncidentDate = DateOnly.FromDateTime(_vm.IncidentDate),
            IncidentType = _vm.SelectedIncidentType!.Value,
            Description  = _vm.Description?.Trim()
        };

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
