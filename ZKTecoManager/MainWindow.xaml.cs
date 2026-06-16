using System.Windows;
using System.Windows.Controls;
using ZKTecoManager.Helpers;
using ZKTecoManager.Views;

namespace ZKTecoManager;

public partial class MainWindow : Window
{
    private Button? _activeNav;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;

        SetActiveNav(btn);

        MainContent.Content = btn.Tag?.ToString() switch
        {
            "Devices"    => ServiceLocator.GetService<DevicesView>(),
            _            => CreatePlaceholder(btn.Content?.ToString()?.Trim() ?? "")
        };
    }

    private void SetActiveNav(Button btn)
    {
        if (_activeNav is not null)
            _activeNav.Background = System.Windows.Media.Brushes.Transparent;

        _activeNav = btn;
        btn.Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x2E, 0x4D, 0x7A));
    }

    private static UIElement CreatePlaceholder(string module) =>
        new TextBlock
        {
            Text = $"Módulo '{module}' — próximamente.",
            FontSize = 16,
            Foreground = System.Windows.Media.Brushes.Gray,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
}
