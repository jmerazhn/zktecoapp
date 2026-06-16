using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Windows;
using ZKTecoManager.Data.Repositories.Interfaces;
using ZKTecoManager.Helpers;
using ZKTecoManager.Models.Entities;
using ZKTecoManager.Models.Enums;
using ZKTecoManager.Services;
using ZKTecoManager.ViewModels.Base;
using ZKTecoManager.Views.Dialogs;

namespace ZKTecoManager.ViewModels;

public partial class DevicesViewModel : ViewModelBase
{
    private readonly IDeviceService _deviceService;
    private readonly IDeviceRepository _deviceRepo;
    private readonly ICompanyRepository _companyRepo;

    [ObservableProperty]
    private ObservableCollection<DeviceItemViewModel> _devices = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(TestConnectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(DownloadLogsCommand))]
    [NotifyCanExecuteChangedFor(nameof(SyncInfoCommand))]
    private DeviceItemViewModel? _selectedDevice;

    public DevicesViewModel(IDeviceService deviceService, IDeviceRepository deviceRepo, ICompanyRepository companyRepo)
    {
        _deviceService = deviceService;
        _deviceRepo = deviceRepo;
        _companyRepo = companyRepo;
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunBusyAsync(async () =>
        {
            var list = await _deviceRepo.FindAsync(d => d.IsActive);
            // Load companies via include — reload with navigation props
            using var scope = ServiceLocator.GetService<IServiceScopeFactory>().CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
            var devices = await repo.FindAsync(d => d.IsActive);

            Application.Current.Dispatcher.Invoke(() =>
            {
                Devices.Clear();
                foreach (var d in devices.OrderBy(x => x.Name))
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
        var companies = await _companyRepo.GetActiveAsync();
        var dialog = new DeviceFormDialog(companies);
        if (dialog.ShowDialog() != true) return;

        var device = dialog.Result!;
        device.CreatedAt = DateTime.UtcNow;
        device.UpdatedAt = DateTime.UtcNow;

        await _deviceRepo.AddAsync(device);
        await _deviceRepo.SaveChangesAsync();

        await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditAsync()
    {
        var companies = await _companyRepo.GetActiveAsync();
        var dialog = new DeviceFormDialog(companies, SelectedDevice!.Device);
        if (dialog.ShowDialog() != true) return;

        var updated = dialog.Result!;
        var entity = await _deviceRepo.GetByIdAsync(updated.Id);
        if (entity is null) return;

        entity.Name = updated.Name;
        entity.CompanyId = updated.CompanyId;
        entity.IpAddress = updated.IpAddress;
        entity.Port = updated.Port;
        entity.CommPassword = updated.CommPassword;
        entity.Location = updated.Location;
        entity.Model = updated.Model;
        entity.UpdatedAt = DateTime.UtcNow;

        // Reconnect if IP/port changed
        _deviceService.Disconnect(entity.Id);

        await _deviceRepo.UpdateAsync(entity);
        await _deviceRepo.SaveChangesAsync();
        await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteAsync()
    {
        var result = MessageBox.Show(
            $"¿Eliminar el dispositivo '{SelectedDevice!.Name}'?",
            "Confirmar eliminación",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        var entity = await _deviceRepo.GetByIdAsync(SelectedDevice.Id);
        if (entity is null) return;

        entity.IsActive = false;
        entity.UpdatedAt = DateTime.UtcNow;
        _deviceService.Disconnect(entity.Id);

        await _deviceRepo.UpdateAsync(entity);
        await _deviceRepo.SaveChangesAsync();
        await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task TestConnectionAsync()
    {
        var item = SelectedDevice!;
        item.Status = DeviceConnectionStatus.Connecting;
        StatusMessage = $"Conectando a {item.IpAddress}:{item.Port}...";

        var ok = await _deviceService.TestConnectionAsync(item.Device);
        item.Status = ok ? DeviceConnectionStatus.Connected : DeviceConnectionStatus.Error;
        StatusMessage = ok
            ? $"Conexión exitosa con {item.Name}"
            : $"No se pudo conectar a {item.Name}";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DownloadLogsAsync()
    {
        var item = SelectedDevice!;
        await RunBusyAsync(async () =>
        {
            StatusMessage = $"Descargando logs de {item.Name}...";
            var result = await _deviceService.DownloadLogsAsync(item.Device);

            if (result.Success)
            {
                // Refresh LastSync display
                var entity = await _deviceRepo.GetByIdAsync(item.Id);
                if (entity is not null) item.Device.LastSync = entity.LastSync;
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
            StatusMessage = $"Sincronizando información de {item.Name}...";
            var result = await _deviceService.SyncDeviceInfoAsync(item.Device);

            if (result.Success)
            {
                item.Status = DeviceConnectionStatus.Connected;
                MessageBox.Show(
                    $"Sincronización completada.\n" +
                    $"Modelo: {result.Model ?? "N/D"}\n" +
                    $"Serie:  {result.SerialNumber ?? "N/D"}\n" +
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
