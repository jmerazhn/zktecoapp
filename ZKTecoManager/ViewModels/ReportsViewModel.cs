using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows;
using ZKTecoManager.Data;
using ZKTecoManager.Models.Entities;
using ZKTecoManager.Services;
using ZKTecoManager.ViewModels.Base;

namespace ZKTecoManager.ViewModels;

public partial class ReportsViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IReportService       _reportService;

    [ObservableProperty] private ObservableCollection<Company>    _companies   = new();
    [ObservableProperty] private ObservableCollection<Department> _departments = new();
    [ObservableProperty] private ObservableCollection<Employee>   _employees   = new();

    [ObservableProperty] private Company?    _selectedCompany;
    [ObservableProperty] private Department? _selectedDepartment;
    [ObservableProperty] private Employee?   _selectedEmployee;

    [ObservableProperty] private string  _reportTypeName  = "Detalle de Asistencia";
    [ObservableProperty] private bool    _isDetailType    = true;
    [ObservableProperty] private bool    _isSummaryType   = false;
    [ObservableProperty] private DateTime _dateFrom = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime _dateTo   = DateTime.Today;

    partial void OnSelectedCompanyChanged(Company? value)    => _ = LoadDepartmentsAsync(value?.Id);
    partial void OnSelectedDepartmentChanged(Department? value) => _ = LoadEmployeesAsync(value?.Id);
    partial void OnIsDetailTypeChanged(bool value)           { if (value) ReportTypeName = "Detalle de Asistencia"; }
    partial void OnIsSummaryTypeChanged(bool value)          { if (value) ReportTypeName = "Resumen Mensual"; }

    public ReportsViewModel(IServiceScopeFactory scopeFactory, IReportService reportService)
    {
        _scopeFactory  = scopeFactory;
        _reportService = reportService;
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        await RunBusyAsync(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var companies = await db.Companies.Where(c => c.IsActive)
                .OrderBy(c => c.Name).AsNoTracking().ToListAsync();
            Application.Current.Dispatcher.Invoke(() =>
            {
                Companies.Clear();
                foreach (var c in companies) Companies.Add(c);
                if (Companies.Count == 1) SelectedCompany = Companies[0];
            });
        });
    }

    private async Task LoadDepartmentsAsync(int? companyId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var query = db.Departments.AsQueryable();
        if (companyId.HasValue) query = query.Where(d => d.CompanyId == companyId);
        var list = await query.OrderBy(d => d.Name).AsNoTracking().ToListAsync();
        Application.Current.Dispatcher.Invoke(() =>
        {
            SelectedDepartment = null;
            Departments.Clear();
            foreach (var d in list) Departments.Add(d);
        });
    }

    private async Task LoadEmployeesAsync(int? departmentId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var query = db.Employees.Where(e => e.IsActive);
        if (departmentId.HasValue)
            query = query.Where(e => e.DepartmentId == departmentId);
        else if (SelectedCompany is not null)
            query = query.Where(e => e.CompanyId == SelectedCompany.Id);
        var list = await query.OrderBy(e => e.LastName).AsNoTracking().ToListAsync();
        Application.Current.Dispatcher.Invoke(() =>
        {
            SelectedEmployee = null;
            Employees.Clear();
            foreach (var e in list) Employees.Add(e);
        });
    }

    [RelayCommand]
    private async Task ExportExcelAsync(CancellationToken ct = default)
    {
        var filePath = PickSavePath("Excel|*.xlsx", ".xlsx");
        if (filePath is null) return;

        await RunBusyAsync(async () =>
        {
            var opts = BuildOptions();
            var msg = await _reportService.ExportExcelAsync(opts, filePath, ct);
            StatusMessage = msg;
        }, "Generando Excel...");
    }

    [RelayCommand]
    private async Task ExportPdfAsync(CancellationToken ct = default)
    {
        var filePath = PickSavePath("PDF|*.pdf", ".pdf");
        if (filePath is null) return;

        await RunBusyAsync(async () =>
        {
            var opts = BuildOptions();
            var msg = await _reportService.ExportPdfAsync(opts, filePath, ct);
            StatusMessage = msg;
        }, "Generando PDF...");
    }

    private ReportOptions BuildOptions() => new(
        Type:         IsDetailType ? ReportType.AttendanceDetail : ReportType.MonthlySummary,
        CompanyId:    SelectedCompany?.Id,
        DepartmentId: SelectedDepartment?.Id,
        EmployeeId:   SelectedEmployee?.Id,
        From:         DateOnly.FromDateTime(DateFrom),
        To:           DateOnly.FromDateTime(DateTo));

    private static string? PickSavePath(string filter, string ext)
    {
        var dlg = new SaveFileDialog
        {
            Filter           = filter,
            DefaultExt       = ext,
            FileName         = $"Reporte_{DateTime.Today:yyyyMMdd}{ext}"
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}
