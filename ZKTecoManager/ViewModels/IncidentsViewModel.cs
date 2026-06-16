using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Windows;
using ZKTecoManager.Data;
using ZKTecoManager.Models.Entities;
using ZKTecoManager.Models.Enums;
using ZKTecoManager.ViewModels.Base;
using ZKTecoManager.Views.Dialogs;

namespace ZKTecoManager.ViewModels;

public partial class IncidentsViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;

    [ObservableProperty] private ObservableCollection<Company>           _companies  = new();
    [ObservableProperty] private ObservableCollection<IncidentItemViewModel> _incidents = new();

    [ObservableProperty] private Company?  _selectedCompany;
    [ObservableProperty] private DateTime  _dateFrom = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime  _dateTo   = DateTime.Today;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApproveCommand))]
    private IncidentItemViewModel? _selectedIncident;

    partial void OnSelectedCompanyChanged(Company? value) => _ = LoadAsync();

    public IncidentsViewModel(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

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
        await LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct = default)
    {
        await RunBusyAsync(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var from = DateOnly.FromDateTime(DateFrom);
            var to   = DateOnly.FromDateTime(DateTo);

            var query = db.Incidents
                .Include(i => i.Employee).ThenInclude(e => e.Department)
                .Where(i => i.IncidentDate >= from && i.IncidentDate <= to)
                .AsQueryable();

            if (SelectedCompany is not null)
                query = query.Where(i => i.Employee.CompanyId == SelectedCompany.Id);

            var list = await query
                .OrderByDescending(i => i.IncidentDate)
                .ThenBy(i => i.Employee.LastName)
                .AsNoTracking().ToListAsync(ct);

            Application.Current.Dispatcher.Invoke(() =>
            {
                Incidents.Clear();
                foreach (var i in list) Incidents.Add(new IncidentItemViewModel(i));
            });
        }, "Cargando incidencias...");
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var query = db.Employees.Where(e => e.IsActive);
        if (SelectedCompany is not null) query = query.Where(e => e.CompanyId == SelectedCompany.Id);
        var employees = await query.OrderBy(e => e.LastName).ToListAsync();

        var dialog = new IncidentFormDialog(employees);
        if (dialog.ShowDialog() != true) return;

        var inc = dialog.Result!;
        inc.CreatedAt = DateTime.UtcNow;
        db.Incidents.Add(inc);
        await db.SaveChangesAsync();
        await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var query = db.Employees.Where(e => e.IsActive);
        if (SelectedCompany is not null) query = query.Where(e => e.CompanyId == SelectedCompany.Id);
        var employees = await query.OrderBy(e => e.LastName).ToListAsync();

        var dialog = new IncidentFormDialog(employees, SelectedIncident!.Incident);
        if (dialog.ShowDialog() != true) return;

        var entity = await db.Incidents.FindAsync(SelectedIncident.Id);
        if (entity is null) return;

        var updated = dialog.Result!;
        entity.EmployeeId   = updated.EmployeeId;
        entity.IncidentDate = updated.IncidentDate;
        entity.IncidentType = updated.IncidentType;
        entity.Description  = updated.Description;
        await db.SaveChangesAsync();
        await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteAsync()
    {
        var res = MessageBox.Show(
            $"¿Eliminar la incidencia de '{SelectedIncident!.EmployeeName}' del {SelectedIncident.DateDisplay}?",
            "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (res != MessageBoxResult.Yes) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = await db.Incidents.FindAsync(SelectedIncident.Id);
        if (entity is null) return;
        db.Incidents.Remove(entity);
        await db.SaveChangesAsync();
        await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(CanApprove))]
    private async Task ApproveAsync()
    {
        var approvedBy = Microsoft.VisualBasic.Interaction.InputBox(
            "Nombre de quien aprueba la incidencia:", "Aprobar incidencia", "");
        if (string.IsNullOrWhiteSpace(approvedBy)) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = await db.Incidents.FindAsync(SelectedIncident!.Id);
        if (entity is null) return;
        entity.ApprovedBy = approvedBy.Trim();
        entity.ApprovedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await LoadAsync();
    }

    private bool HasSelection()  => SelectedIncident is not null;
    private bool CanApprove()    => SelectedIncident is not null && !SelectedIncident.IsApproved;
}
