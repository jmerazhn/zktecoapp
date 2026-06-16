using System.Windows.Controls;
using ZKTecoManager.ViewModels;

namespace ZKTecoManager.Views;

public partial class EmployeesView : UserControl
{
    public EmployeesView(EmployeesViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        Loaded += async (_, _) => await vm.InitializeCommand.ExecuteAsync(null);
    }
}
