using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Windows;
using ZKTecoManager.Data;
using ZKTecoManager.Models.Entities;
using ZKTecoManager.Models.Enums;
using ZKTecoManager.Services;
using ZKTecoManager.ViewModels.Base;
using ZKTecoManager.Views.Dialogs;

namespace ZKTecoManager.ViewModels;

public partial class AttendanceViewModel : ViewModelBase
{
    private readonly IAttendanceService _attendanceService;
    private readonly IServiceScopeFactory _scopeFactory;

    // ── Filters ───────────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<Company>    _companies    = new();
    [ObservableProperty] private ObservableCollection<Department> _departments  = new();
    [ObservableProperty] private ObservableCollection<Employee>   _employeeList = new();

    [ObservableProperty] private Company?    _selectedCompany;
    [ObservableProperty] private Department? _selectedDepartment;
    [ObservableProperty] private Employee?   _selectedEmployeeFilter;

    [ObservableProperty] private DateTime _dateFrom = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime _dateTo   = DateTime.Today;

    // ── Grid data ─────────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<AttendanceRecordItemViewModel> _records = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditRecordCommand))]
    private AttendanceRecordItemViewModel? _selectedRecord;

    // ── Summary stats ─────────────────────────────────────────────────────────
    [ObservableProperty] private int     _totalPresent;
    [ObservableProperty] private int     _totalAbsent;
    [ObservableProperty] private int     _totalLate;
    [ObservableProperty] private decimal _totalOvertime;

    public AttendanceViewModel(IAttendanceService attendanceService, IServiceScopeFactory scopeFactory)
    {
        _attendanceService = attendanceService;
        _scopeFactory      = scopeFactory;
    }

    partial void OnSelectedCompanyChanged(Company? value)    => _ = LoadFiltersAsync(value);
    partial void OnSelectedDepartmentChanged(Department? value) => _ = LoadAsync();

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
        });

        await LoadAsync();
    }

    private async Task LoadFiltersAsync(Company? company)
    {
        Departments.Clear();
        SelectedDepartment = null;
        EmployeeList.Clear();
        SelectedEmployeeFilter = null;

        if (company is null) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var depts = await db.Departments
            .Where(d => d.CompanyId == company.Id && d.IsActive)
            .OrderBy(d => d.Name).AsNoTracking().ToListAsync();

        var emps = await db.Employees
            .Where(e => e.CompanyId == company.Id && e.IsActive)
            .OrderBy(e => e.LastName).AsNoTracking().ToListAsync();

        Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (var d in depts) Departments.Add(d);
            foreach (var e in emps)  EmployeeList.Add(e);
        });

        await LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct = default)
    {
        await RunBusyAsync(async () =>
        {
            var filter = new AttendanceFilter(
                CompanyId:    SelectedCompany?.Id,
                DepartmentId: SelectedDepartment?.Id,
                EmployeeId:   SelectedEmployeeFilter?.Id,
                From:         DateOnly.FromDateTime(DateFrom),
                To:           DateOnly.FromDateTime(DateTo));

            var data = await _attendanceService.GetRecordsAsync(filter, ct);

            Application.Current.Dispatcher.Invoke(() =>
            {
                Records.Clear();
                foreach (var r in data)
                    Records.Add(new AttendanceRecordItemViewModel(r));

                // Compute summary
                TotalPresent  = Records.Count(r => r.Record.Status is AttendanceStatus.Normal or AttendanceStatus.Late);
                TotalAbsent   = Records.Count(r => r.Record.Status == AttendanceStatus.Absent);
                TotalLate     = Records.Count(r => r.Record.Status == AttendanceStatus.Late);
                TotalOvertime = Records.Sum(r  => r.Record.OvertimeHours);
            });
        }, "Cargando registros...");
    }

    [RelayCommand]
    private async Task ProcessLogsAsync()
    {
        var dialog = new ProcessAttendanceDialog(
            Companies.ToList(),
            DateFrom, DateTo,
            SelectedCompany);

        if (dialog.ShowDialog() != true) return;

        var (from, to, companyId) = dialog.Result;

        await RunBusyAsync(async () =>
        {
            StatusMessage = "Procesando registros de asistencia...";
            var result = await _attendanceService.ProcessLogsAsync(
                DateOnly.FromDateTime(from),
                DateOnly.FromDateTime(to),
                companyId);

            var msg = result.Errors > 0
                ? $"Completado con {result.Errors} error(es).\n"
                : string.Empty;

            msg += $"Logs procesados: {result.Processed}\n" +
                   $"Registros nuevos: {result.NewRecords}\n" +
                   $"Registros actualizados: {result.Updated}";

            MessageBox.Show(msg, "Procesamiento completado",
                MessageBoxButton.OK,
                result.Errors > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

        }, "Procesando...");

        await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditRecordAsync()
    {
        var item = SelectedRecord!;
        var dialog = new EditAttendanceDialog(item.Record);
        if (dialog.ShowDialog() != true) return;

        var (status, notes) = dialog.Result;
        await _attendanceService.UpdateRecordAsync(item.Id, status, notes);
        await LoadAsync();
    }

    private bool HasSelection() => SelectedRecord is not null;
}
