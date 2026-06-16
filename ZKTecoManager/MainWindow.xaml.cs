using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using ZKTecoManager.Helpers;
using ZKTecoManager.Services;
using ZKTecoManager.Views;
using ZKTecoManager.Views.Dialogs;

namespace ZKTecoManager;

public partial class MainWindow : Window
{
    private Button? _activeNav;
    private IServiceScope? _currentScope;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;

        SetActiveNav(btn);

        // Dispose previous scope (releases scoped DbContext, VMs, Views)
        _currentScope?.Dispose();
        _currentScope = ServiceLocator.GetService<IServiceScopeFactory>().CreateScope();

        MainContent.Content = btn.Tag?.ToString() switch
        {
            "Devices"    => _currentScope.ServiceProvider.GetRequiredService<DevicesView>(),
            "Employees"  => _currentScope.ServiceProvider.GetRequiredService<EmployeesView>(),
            "Attendance" => _currentScope.ServiceProvider.GetRequiredService<AttendanceView>(),
            "Incidents"  => _currentScope.ServiceProvider.GetRequiredService<IncidentsView>(),
            "Reports"    => _currentScope.ServiceProvider.GetRequiredService<ReportsView>(),
            "DbSettings" => ShowDbSettings(),
            _            => CreatePlaceholder(btn.Content?.ToString()?.Trim() ?? "")
        };
    }

    private void SetActiveNav(Button btn)
    {
        if (_activeNav is not null)
            _activeNav.ClearValue(BackgroundProperty);
        _activeNav = btn;
        btn.Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x2E, 0x4D, 0x7A));
    }

    private UIElement ShowDbSettings()
    {
        var current = ConnectionConfig.Load();
        var dlg = new DatabaseSettingsDialog(current) { Owner = this };
        if (dlg.ShowDialog() != true) return MainContent.Content as UIElement ?? CreatePlaceholder("");

        var newCfg = dlg.Result!;
        newCfg.Save();

        var result = MessageBox.Show(
            "Configuración guardada. ¿Reiniciar la aplicación para aplicar los cambios?",
            "Reiniciar", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            System.Diagnostics.Process.Start(
                System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName);
            Application.Current.Shutdown();
        }

        return MainContent.Content as UIElement ?? CreatePlaceholder("");
    }

    private static UIElement CreatePlaceholder(string module) =>
        new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = module,
                    FontSize = 20, FontWeight = FontWeights.SemiBold,
                    Foreground = System.Windows.Media.Brushes.Gray,
                    HorizontalAlignment = HorizontalAlignment.Center
                },
                new TextBlock
                {
                    Text = "Módulo en desarrollo — próxima fase.",
                    FontSize = 13, Margin = new Thickness(0, 8, 0, 0),
                    Foreground = System.Windows.Media.Brushes.LightGray,
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            }
        };

    protected override void OnClosed(EventArgs e)
    {
        _currentScope?.Dispose();
        base.OnClosed(e);
    }
}
