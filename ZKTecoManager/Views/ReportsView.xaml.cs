using System.Windows.Controls;
using ZKTecoManager.ViewModels;

namespace ZKTecoManager.Views;

public partial class ReportsView : UserControl
{
    private readonly ReportsViewModel _vm;

    public ReportsView(ReportsViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        => await _vm.InitializeCommand.ExecuteAsync(null);
}
