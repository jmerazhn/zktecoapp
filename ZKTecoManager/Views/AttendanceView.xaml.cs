using System.Windows.Controls;
using ZKTecoManager.ViewModels;

namespace ZKTecoManager.Views;

public partial class AttendanceView : UserControl
{
    public AttendanceView(AttendanceViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        Loaded += async (_, _) => await vm.InitializeCommand.ExecuteAsync(null);
    }
}
