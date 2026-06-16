using System.Windows;
using ZKTecoManager.Models.Entities;

namespace ZKTecoManager.Views.Dialogs;

public partial class ProcessAttendanceDialog : Window
{
    public (DateTime From, DateTime To, int? CompanyId) Result { get; private set; }

    public ProcessAttendanceDialog(
        IReadOnlyList<Company> companies,
        DateTime defaultFrom,
        DateTime defaultTo,
        Company? selectedCompany)
    {
        InitializeComponent();

        CmbCompany.ItemsSource = companies;
        CmbCompany.SelectedItem = selectedCompany;
        DpFrom.SelectedDate = defaultFrom;
        DpTo.SelectedDate = defaultTo;
    }

    private void Process_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        if (!DpFrom.SelectedDate.HasValue || !DpTo.SelectedDate.HasValue)
        { ShowError("Selecciona el rango de fechas."); return; }

        if (DpFrom.SelectedDate > DpTo.SelectedDate)
        { ShowError("La fecha de inicio debe ser anterior a la fecha fin."); return; }

        Result = (DpFrom.SelectedDate.Value,
                  DpTo.SelectedDate.Value,
                  (CmbCompany.SelectedItem as Company)?.Id);

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ShowError(string msg)
    {
        ErrorText.Text = msg;
        ErrorText.Visibility = Visibility.Visible;
    }
}
