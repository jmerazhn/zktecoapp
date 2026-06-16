using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Windows;
using ZKTecoManager.Data;
using ZKTecoManager.Models.Entities;
using ZKTecoManager.Services;
using ZKTecoManager.ViewModels.Base;
using ZKTecoManager.Views.Dialogs;

namespace ZKTecoManager.ViewModels;

public partial class EmployeesViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEmployeeService _employeeService;

    // ── Filters ───────────────────────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<Company> _companies = new();
    [ObservableProperty] private ObservableCollection<Department> _departments = new();
    [ObservableProperty] private ObservableCollection<EmployeeItemViewModel> _employees = new();

    [ObservableProperty] private Company? _selectedCompany;
    [ObservableProperty] private Department? _selectedDepartment;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _showInactive;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleActiveCommand))]
    [NotifyCanExecuteChangedFor(nameof(AssignShiftCommand))]
    [NotifyCanExecuteChangedFor(nameof(ManageDevicesCommand))]
    private EmployeeItemViewModel? _selectedEmployee;

    public EmployeesViewModel(IServiceScopeFactory scopeFactory, IEmployeeService employeeService)
    {
        _scopeFactory = scopeFactory;
        _employeeService = employeeService;
    }

    // ── Reactive filter handlers ──────────────────────────────────────────────

    partial void OnSelectedCompanyChanged(Company? value) =>
        _ = LoadDepartmentsAsync(value);

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task InitializeAsync()
    {
        await RunBusyAsync(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var companies = await db.Companies
                .Where(c => c.IsActive).OrderBy(c => c.Name).AsNoTracking().ToListAsync();

            Application.Current.Dispatcher.Invoke(() =>
            {
                Companies.Clear();
                foreach (var c in companies) Companies.Add(c);
                if (Companies.Count == 1) SelectedCompany = Companies[0];
            });
        }, "Cargando...");

        await LoadAsync();
    }

    private async Task LoadDepartmentsAsync(Company? company)
    {
        Departments.Clear();
        SelectedDepartment = null;
        if (company is null) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var depts = await db.Departments
            .Where(d => d.CompanyId == company.Id && d.IsActive)
            .OrderBy(d => d.Name).AsNoTracking().ToListAsync();

        Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (var d in depts) Departments.Add(d);
        });

        await LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct = default)
    {
        await RunBusyAsync(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var query = db.Employees
                .Include(e => e.Department)
                .AsQueryable();

            if (SelectedCompany is not null)
                query = query.Where(e => e.CompanyId == SelectedCompany.Id);

            if (SelectedDepartment is not null)
                query = query.Where(e => e.DepartmentId == SelectedDepartment.Id);

            if (!ShowInactive)
                query = query.Where(e => e.IsActive);

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var s = SearchText.Trim().ToLower();
                query = query.Where(e =>
                    e.FirstName.ToLower().Contains(s) ||
                    e.LastName.ToLower().Contains(s) ||
                    e.EmployeeCode.ToLower().Contains(s) ||
                    (e.CardNumber != null && e.CardNumber.ToLower().Contains(s)));
            }

            var employees = await query
                .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
                .AsNoTracking().ToListAsync(ct);

            // Load current shifts and device counts in one query each
            var empIds = employees.Select(e => e.Id).ToList();

            var today = DateOnly.FromDateTime(DateTime.Today);
            var activeShifts = await db.EmployeeShifts
                .Include(es => es.Shift)
                .Where(es => empIds.Contains(es.EmployeeId)
                          && es.EffectiveFrom <= today
                          && (es.EffectiveTo == null || es.EffectiveTo >= today))
                .AsNoTracking()
                .ToListAsync(ct);

            var deviceCounts = await db.DeviceUsers
                .Where(du => empIds.Contains(du.EmployeeId))
                .GroupBy(du => du.EmployeeId)
                .Select(g => new { EmployeeId = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            Application.Current.Dispatcher.Invoke(() =>
            {
                Employees.Clear();
                foreach (var emp in employees)
                {
                    var vm = new EmployeeItemViewModel(emp);
                    var shift = activeShifts
                        .Where(es => es.EmployeeId == emp.Id)
                        .OrderByDescending(es => es.EffectiveFrom)
                        .FirstOrDefault();
                    vm.CurrentShift = shift?.Shift.Name ?? "Sin turno";
                    vm.EnrolledDevices = deviceCounts.FirstOrDefault(d => d.EmployeeId == emp.Id)?.Count ?? 0;
                    Employees.Add(vm);
                }
            });
        }, "Cargando empleados...");
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var companies = await db.Companies.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
        var departments = await db.Departments.Where(d => d.IsActive).OrderBy(d => d.Name).ToListAsync();

        var dialog = new EmployeeFormDialog(companies, departments);
        if (dialog.ShowDialog() != true) return;

        var emp = dialog.Result!;
        emp.CreatedAt = DateTime.UtcNow;
        emp.UpdatedAt = DateTime.UtcNow;

        db.Employees.Add(emp);
        await db.SaveChangesAsync();
        await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var companies = await db.Companies.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
        var departments = await db.Departments.Where(d => d.IsActive).OrderBy(d => d.Name).ToListAsync();

        var dialog = new EmployeeFormDialog(companies, departments, SelectedEmployee!.Employee);
        if (dialog.ShowDialog() != true) return;

        var updated = dialog.Result!;
        var entity = await db.Employees.FindAsync(updated.Id);
        if (entity is null) return;

        entity.EmployeeCode = updated.EmployeeCode;
        entity.FirstName    = updated.FirstName;
        entity.LastName     = updated.LastName;
        entity.Position     = updated.Position;
        entity.HireDate     = updated.HireDate;
        entity.CardNumber   = updated.CardNumber;
        entity.CompanyId    = updated.CompanyId;
        entity.DepartmentId = updated.DepartmentId;
        entity.UpdatedAt    = DateTime.UtcNow;

        await db.SaveChangesAsync();
        await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ToggleActiveAsync()
    {
        var item = SelectedEmployee!;
        var action = item.IsActive ? "desactivar" : "activar";
        var result = MessageBox.Show(
            $"¿Deseas {action} al empleado '{item.FullName}'?",
            "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var entity = await db.Employees.FindAsync(item.Id);
        if (entity is null) return;

        entity.IsActive = !entity.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task AssignShiftAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var emp = SelectedEmployee!;
        var shifts = await db.WorkShifts
            .Where(s => s.CompanyId == emp.Employee.CompanyId && s.IsActive)
            .OrderBy(s => s.Name).ToListAsync();

        if (shifts.Count == 0)
        {
            MessageBox.Show("No hay turnos configurados para esta empresa.",
                "Sin turnos", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new AssignShiftDialog(emp.Employee, shifts, emp.CurrentShift);
        if (dialog.ShowDialog() != true) return;

        var (shiftId, effectiveFrom) = dialog.Result;

        // Close previous open shift
        var openShift = await db.EmployeeShifts
            .Where(es => es.EmployeeId == emp.Id && es.EffectiveTo == null)
            .FirstOrDefaultAsync();

        if (openShift is not null)
            openShift.EffectiveTo = effectiveFrom.AddDays(-1);

        db.EmployeeShifts.Add(new EmployeeShift
        {
            EmployeeId = emp.Id,
            ShiftId = shiftId,
            EffectiveFrom = effectiveFrom
        });

        await db.SaveChangesAsync();
        await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ManageDevicesAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var emp = SelectedEmployee!;

        var devices = await db.Devices
            .Where(d => d.CompanyId == emp.Employee.CompanyId && d.IsActive)
            .OrderBy(d => d.Name).ToListAsync();

        if (devices.Count == 0)
        {
            MessageBox.Show("No hay dispositivos configurados para esta empresa.",
                "Sin dispositivos", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var enrollments = await db.DeviceUsers
            .Where(du => du.EmployeeId == emp.Id)
            .ToListAsync();

        var dialog = new DeviceEnrollmentDialog(emp.Employee, devices, enrollments, _employeeService);
        dialog.ShowDialog();

        await LoadAsync();
    }

    private bool HasSelection() => SelectedEmployee is not null;
}
