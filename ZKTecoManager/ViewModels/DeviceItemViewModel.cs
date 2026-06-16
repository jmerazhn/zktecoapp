using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;
using ZKTecoManager.Models.Entities;
using ZKTecoManager.Models.Enums;

namespace ZKTecoManager.ViewModels;

public partial class DeviceItemViewModel : ObservableObject
{
    public Device Device { get; }

    [ObservableProperty]
    private DeviceConnectionStatus _status = DeviceConnectionStatus.Disconnected;

    public DeviceItemViewModel(Device device)
    {
        Device = device;
    }

    // Passthrough properties for easy binding
    public int Id => Device.Id;
    public string Name => Device.Name;
    public string IpAddress => Device.IpAddress;
    public int Port => Device.Port;
    public string? Location => Device.Location;
    public string? Model => Device.Model;
    public string? SerialNumber => Device.SerialNumber;
    public string CompanyName => Device.Company?.Name ?? string.Empty;
    public DateTime? LastSync => Device.LastSync;

    public string LastSyncDisplay => Device.LastSync.HasValue
        ? Device.LastSync.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
        : "Nunca";

    public string StatusLabel => Status switch
    {
        DeviceConnectionStatus.Connected    => "Conectado",
        DeviceConnectionStatus.Connecting   => "Conectando...",
        DeviceConnectionStatus.Error        => "Error",
        _                                   => "Desconectado"
    };

    public Brush StatusBrush => Status switch
    {
        DeviceConnectionStatus.Connected    => new SolidColorBrush(Color.FromRgb(34, 197, 94)),
        DeviceConnectionStatus.Connecting   => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
        DeviceConnectionStatus.Error        => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
        _                                   => new SolidColorBrush(Color.FromRgb(156, 163, 175))
    };

    partial void OnStatusChanged(DeviceConnectionStatus value)
    {
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(StatusBrush));
    }
}
