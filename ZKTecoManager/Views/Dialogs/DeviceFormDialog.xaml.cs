using System.Windows;
using ZKTecoManager.Models.Entities;

namespace ZKTecoManager.Views.Dialogs;

public partial class DeviceFormDialog : Window
{
    public Device? Result { get; private set; }
    private readonly Device? _editing;

    public DeviceFormDialog(IReadOnlyList<Company> companies, Device? editing = null)
    {
        InitializeComponent();
        _editing = editing;

        CmbCompany.ItemsSource = companies;

        if (editing is not null)
        {
            TitleText.Text = "Editar Dispositivo";
            TxtName.Text = editing.Name;
            CmbCompany.SelectedValue = editing.CompanyId;
            CmbCompany.SelectedItem = companies.FirstOrDefault(c => c.Id == editing.CompanyId);
            TxtIp.Text = editing.IpAddress;
            TxtPort.Text = editing.Port.ToString();
            TxtPassword.Text = editing.CommPassword ?? string.Empty;
            TxtLocation.Text = editing.Location ?? string.Empty;
            TxtModel.Text = editing.Model ?? string.Empty;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        if (string.IsNullOrWhiteSpace(TxtName.Text))
        { ShowError("El nombre es obligatorio."); return; }

        if (CmbCompany.SelectedItem is not Company company)
        { ShowError("Selecciona una empresa."); return; }

        if (string.IsNullOrWhiteSpace(TxtIp.Text))
        { ShowError("La dirección IP es obligatoria."); return; }

        if (!int.TryParse(TxtPort.Text, out var port) || port < 1 || port > 65535)
        { ShowError("El puerto debe ser un número entre 1 y 65535."); return; }

        Result = new Device
        {
            Id = _editing?.Id ?? 0,
            Name = TxtName.Text.Trim(),
            CompanyId = company.Id,
            IpAddress = TxtIp.Text.Trim(),
            Port = port,
            CommPassword = string.IsNullOrWhiteSpace(TxtPassword.Text) ? null : TxtPassword.Text.Trim(),
            Location = string.IsNullOrWhiteSpace(TxtLocation.Text) ? null : TxtLocation.Text.Trim(),
            Model = string.IsNullOrWhiteSpace(TxtModel.Text) ? null : TxtModel.Text.Trim(),
            IsActive = true
        };

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ShowError(string msg)
    {
        ErrorText.Text = msg;
        ErrorText.Visibility = Visibility.Visible;
    }
}
