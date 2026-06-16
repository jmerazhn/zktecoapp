using System.Windows.Controls;
using ZKTecoManager.ViewModels;

namespace ZKTecoManager.Views;

public partial class IncidentsView : UserControl
{
    private readonly IncidentsViewModel _vm;

    public IncidentsView(IncidentsViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        => await _vm.InitializeCommand.ExecuteAsync(null);
}
