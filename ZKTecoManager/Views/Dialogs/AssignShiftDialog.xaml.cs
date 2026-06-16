using System.Windows;
using ZKTecoManager.Models.Entities;

namespace ZKTecoManager.Views.Dialogs;

public partial class AssignShiftDialog : Window
{
    public (int ShiftId, DateOnly EffectiveFrom) Result { get; private set; }

    public AssignShiftDialog(Employee employee, IReadOnlyList<WorkShift> shifts, string currentShift)
    {
        InitializeComponent();
        TxtEmployeeName.Text = employee.FullName;
        TxtCurrentShift.Text = currentShift;
        CmbShift.ItemsSource = shifts;
        DpEffectiveFrom.SelectedDate = DateTime.Today;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        if (CmbShift.SelectedItem is not WorkShift shift)
        { ShowError("Selecciona un turno."); return; }

        if (!DpEffectiveFrom.SelectedDate.HasValue)
        { ShowError("Selecciona la fecha de inicio."); return; }

        Result = (shift.Id, DateOnly.FromDateTime(DpEffectiveFrom.SelectedDate.Value));
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ShowError(string msg)
    {
        ErrorText.Text = msg;
        ErrorText.Visibility = Visibility.Visible;
    }
}
