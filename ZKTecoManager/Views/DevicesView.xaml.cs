using System.Windows.Controls;
using ZKTecoManager.ViewModels;

namespace ZKTecoManager.Views;

public partial class DevicesView : UserControl
{
    public DevicesView(DevicesViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        Loaded += async (_, _) => await vm.LoadCommand.ExecuteAsync(null);
    }
}
