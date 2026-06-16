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

public partial class DevicesViewModel : ViewModelBase
{
    private readonly IDeviceService _deviceService;
    private readonly IServiceScopeFactory _scopeFactory;

    [ObservableProperty]
    private ObservableCollection<DeviceItemViewModel> _devices = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(TestConnectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(DownloadLogsCommand))]
    [NotifyCanExecuteChangedFor(nameof(SyncInfoCommand))]
    private DeviceItemViewModel? _selectedDevice;

    public DevicesViewModel(IDeviceService deviceService, IServiceScopeFactory scopeFactory)
    {
        _deviceService = deviceService;
        _scopeFactory = scopeFactory;
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct = default)
    {
        await RunBusyAsync(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var devices = await db.Devices
                .Include(d => d.Company)
                .Where(d => d.IsActive)
                .OrderBy(d => d.Name)
                .AsNoTracking()
                .ToListAsync(ct);

            Application.Current.Dispatcher.Invoke(() =>
            {
                Devices.Clear();
                foreach (var d in devices)
                {
                    var vm = new DeviceItemViewModel(d);
                    if (_deviceService.IsConnected(d.Id))
                        vm.Status = DeviceConnectionStatus.Connected;
                    Devices.Add(vm);
                }
            });
        }, "Cargando dispositivos...");
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var companies = await db.Companies.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();

        var dialog = new DeviceFormDialog(companies);
        if (dialog.ShowDialog() != true) return;

        var device = dialog.Result!;

        // Check if a soft-deleted device with the same IP:Port exists — reactivate it
        var existing = await db.Devices
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.IpAddress == device.IpAddress
                                   && d.Port      == device.Port
                                   && !d.IsActive);
        if (existing is not null)
        {
            existing.Name         = device.Name;
            existing.CompanyId    = device.CompanyId;
            existing.CommPassword = device.CommPassword;
            existing.Location     = device.Location;
            existing.IsActive     = true;
            existing.UpdatedAt    = DateTime.UtcNow;
        }
        else
        {
            device.CreatedAt = DateTime.UtcNow;
            device.UpdatedAt = DateTime.UtcNow;
            db.Devices.Add(device);
        }

        await db.SaveChangesAsync();
        await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var companies = await db.Companies.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();

        var dialog = new DeviceFormDialog(companies, SelectedDevice!.Device);
        if (dialog.ShowDialog() != true) return;

        var updated = dialog.Result!;
        var entity = await db.Devices.FindAsync(updated.Id);
        if (entity is null) return;

        entity.Name = updated.Name;
        entity.CompanyId = updated.CompanyId;
        entity.IpAddress = updated.IpAddress;
        entity.Port = updated.Port;
        entity.CommPassword = updated.CommPassword;
        entity.Location = updated.Location;
        entity.Model = updated.Model;
        entity.UpdatedAt = DateTime.UtcNow;

        _deviceService.Disconnect(entity.Id);
        await db.SaveChangesAsync();
        await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteAsync()
    {
        var result = MessageBox.Show(
            $"¿Eliminar el dispositivo '{SelectedDevice!.Name}'?",
            "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = await db.Devices.FindAsync(SelectedDevice.Id);
        if (entity is null) return;

        entity.IsActive = false;
        entity.UpdatedAt = DateTime.UtcNow;
        _deviceService.Disconnect(entity.Id);
        await db.SaveChangesAsync();
        await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task TestConnectionAsync()
    {
        var item = SelectedDevice!;
        item.Status = DeviceConnectionStatus.Connecting;
        StatusMessage = $"Verificando {item.IpAddress}:{item.Port}...";

        var result = await _deviceService.TestConnectionAsync(item.Device);

        item.Status = result.Connected ? DeviceConnectionStatus.Connected : DeviceConnectionStatus.Error;

        if (result.Connected && result.Error is null)
        {
            StatusMessage = $"Conexión exitosa con {item.Name}";
        }
        else if (result.Connected && result.Error is not null)
        {
            // Connected but with a warning (e.g. password mismatch detected)
            StatusMessage = $"Conectado con advertencia — {item.Name}";
            MessageBox.Show(result.Error, $"Advertencia — {item.Name}",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else
        {
            StatusMessage = $"Error: {item.Name}";
            MessageBox.Show(result.Error, $"No se pudo conectar a {item.Name}",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DownloadLogsAsync()
    {
        var item = SelectedDevice!;
        await RunBusyAsync(async () =>
        {
            var result = await _deviceService.DownloadLogsAsync(item.Device);
            if (result.Success)
            {
                item.Status = DeviceConnectionStatus.Connected;
                MessageBox.Show(
                    result.NewLogs > 0
                        ? $"Se descargaron {result.NewLogs} registros nuevos."
                        : "No hay registros nuevos en el dispositivo.",
                    "Descarga completada", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                item.Status = DeviceConnectionStatus.Error;
                MessageBox.Show($"Error: {result.Error}", "Error de descarga",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }, $"Descargando logs de {item.Name}...");

        await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task SyncInfoAsync()
    {
        var item = SelectedDevice!;
        await RunBusyAsync(async () =>
        {
            var result = await _deviceService.SyncDeviceInfoAsync(item.Device);
            if (result.Success)
            {
                item.Status = DeviceConnectionStatus.Connected;
                MessageBox.Show(
                    $"Sincronización completada.\nModelo: {result.Model ?? "N/D"}\n" +
                    $"Serie: {result.SerialNumber ?? "N/D"}\n" +
                    $"Hora del reloj: {result.DeviceTime?.ToString("HH:mm:ss") ?? "N/D"}",
                    "Sincronización exitosa", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                item.Status = DeviceConnectionStatus.Error;
                MessageBox.Show($"Error: {result.Error}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }, "Sincronizando...");

        await LoadAsync();
    }

    private bool HasSelection() => SelectedDevice is not null;
}
