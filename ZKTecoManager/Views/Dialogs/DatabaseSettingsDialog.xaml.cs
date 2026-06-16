using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Data.SqlClient;
using System.Windows;
using System.Windows.Media;
using ZKTecoManager.Services;

namespace ZKTecoManager.Views.Dialogs;

public partial class DatabaseSettingsDialogVM : ObservableObject
{
    [ObservableProperty] private string  _server   = "(localdb)\\MSSQLLocalDB";
    [ObservableProperty] private string  _database = "ZKTecoManager";
    [ObservableProperty] private bool    _isWindowsAuth = true;
    [ObservableProperty] private bool    _isSqlAuth     = false;
    [ObservableProperty] private string? _username;
    [ObservableProperty] private string? _password;

    partial void OnIsWindowsAuthChanged(bool value) { if (value) IsSqlAuth = !value; }
    partial void OnIsSqlAuthChanged(bool value)     { if (value) IsWindowsAuth = !value; }

    public void LoadFrom(ConnectionConfig cfg)
    {
        Server        = cfg.Server;
        Database      = cfg.Database;
        IsWindowsAuth = cfg.AuthType == DbAuthType.Windows;
        IsSqlAuth     = cfg.AuthType == DbAuthType.SqlServer;
        Username      = cfg.Username;
        Password      = cfg.Password;
    }

    public ConnectionConfig ToConfig() => new()
    {
        Server   = Server.Trim(),
        Database = Database.Trim(),
        AuthType = IsSqlAuth ? DbAuthType.SqlServer : DbAuthType.Windows,
        Username = IsSqlAuth ? Username?.Trim() : null,
        Password = IsSqlAuth ? Password : null
    };

    public string PreviewConnectionString() => ToConfig().BuildConnectionString();
}

public partial class DatabaseSettingsDialog : Window
{
    private readonly DatabaseSettingsDialogVM _vm = new();

    public ConnectionConfig? Result { get; private set; }

    public DatabaseSettingsDialog(ConnectionConfig? current = null)
    {
        DataContext = _vm;
        InitializeComponent();

        if (current is not null)
        {
            _vm.LoadFrom(current);
            if (current.Password is not null) PwdBox.Password = current.Password;
        }

        UpdatePreview();
        _vm.PropertyChanged += (_, _) => UpdatePreview();
    }

    private void UpdatePreview()
        => ConnStrPreview.Text = _vm.PreviewConnectionString();

    private void PwdBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _vm.Password = PwdBox.Password;
        UpdatePreview();
    }

    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        TestResultBorder.Visibility = Visibility.Visible;
        TestResultText.Text         = "Probando conexión...";
        TestResultBorder.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x40, 0x6E));
        TestResultText.Foreground   = Brushes.White;

        var connStr = _vm.PreviewConnectionString();
        try
        {
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();
            TestResultBorder.Background = new SolidColorBrush(Color.FromRgb(0x14, 0x53, 0x2D));
            TestResultText.Text         = $"✔  Conexión exitosa  ·  SQL Server {conn.ServerVersion}";
        }
        catch (Exception ex)
        {
            TestResultBorder.Background = new SolidColorBrush(Color.FromRgb(0x7F, 0x1D, 0x1D));
            TestResultText.Text         = $"✖  {ex.Message}";
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_vm.Server) || string.IsNullOrWhiteSpace(_vm.Database))
        {
            MessageBox.Show("Servidor y base de datos son obligatorios.", "Validación",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result = _vm.ToConfig();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
