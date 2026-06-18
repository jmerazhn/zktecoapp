using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZKTecoManager.Models.Entities;
using ZKTecoManager.Services;

namespace ZKTecoManager.Views.Dialogs;

public partial class DeviceEnrollmentDialog : Window
{
    private readonly Employee _employee;
    private readonly IEmployeeService _employeeService;
    private readonly List<EnrollmentItem> _items;

    public DeviceEnrollmentDialog(
        Employee employee,
        IReadOnlyList<Device> devices,
        IReadOnlyList<DeviceUser> enrollments,
        IEmployeeService employeeService)
    {
        InitializeComponent();
        _employee = employee;
        _employeeService = employeeService;

        TxtEmployeeName.Text = $"{employee.FullName}  —  Código: {employee.EmployeeCode}";

        _items = devices.Select(d => new EnrollmentItem(d)
        {
            IsEnrolled = enrollments.Any(e => e.DeviceId == d.Id)
        }).ToList();

        DeviceGrid.ItemsSource = _items;
    }

    private async void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not EnrollmentItem item) return;

        item.IsIdle = false;
        item.StatusLabel = "Procesando...";
        TxtStatus.Text = string.Empty;

        EnrollResult result;
        if (item.IsEnrolled)
            result = await _employeeService.RemoveFromDeviceAsync(_employee, item.Device);
        else
            result = await _employeeService.EnrollOnDeviceAsync(_employee, item.Device);

        if (result.Success)
        {
            item.IsEnrolled = !item.IsEnrolled;
            TxtStatus.Text = item.IsEnrolled
                ? $"✔ Empleado enrolado en {item.DeviceName}."
                : $"✔ Empleado eliminado de {item.DeviceName}.";
        }
        else
        {
            TxtStatus.Text = $"✖ Error en {item.DeviceName}: {result.Error}";
        }

        item.IsIdle = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // EXPERIMENTO (Fase A): prueba el protocolo ADMS/Push como alternativa, ya que
    // tanto Pull SDK como Standalone SDK confirmaron que este firmware no soporta
    // escritura remota de usuarios.
    private async void TestAdmsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not EnrollmentItem item) return;

        item.IsIdle = false;
        TxtStatus.Text = $"Probando ADMS en {item.DeviceName}...";

        var (success, detail) = await _employeeService.TestAdmsEnrollOnDeviceAsync(_employee, item.Device);

        TxtStatus.Text = success
            ? $"✔ ADMS funcionó en {item.DeviceName}: {detail}"
            : $"✖ ADMS falló en {item.DeviceName}: {detail}";

        item.IsIdle = true;
    }
}

// ── Item ViewModel (local to dialog) ─────────────────────────────────────────

public partial class EnrollmentItem : ObservableObject
{
    public Device Device { get; }
    public string DeviceName => Device.Name;
    public string IpAddress  => Device.IpAddress;
    public string? Location  => Device.Location;

    [ObservableProperty] private bool _isEnrolled;
    [ObservableProperty] private bool _isIdle = true;

    public string StatusLabel
    {
        get => IsEnrolled ? "Enrolado" : "Sin enrolar";
        set { } // setter needed for manual updates
    }

    public Brush StatusBackground => IsEnrolled
        ? new SolidColorBrush(Color.FromRgb(34, 197, 94))
        : new SolidColorBrush(Color.FromRgb(156, 163, 175));

    public string ActionLabel => IsEnrolled ? "Eliminar" : "Enrolar";

    public Style ActionStyle => IsEnrolled
        ? (Style)Application.Current.FindResource("DangerButton")
        : (Style)Application.Current.FindResource("PrimaryButton");

    partial void OnIsEnrolledChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(StatusBackground));
        OnPropertyChanged(nameof(ActionLabel));
        OnPropertyChanged(nameof(ActionStyle));
    }

    public EnrollmentItem(Device device) => Device = device;
}
