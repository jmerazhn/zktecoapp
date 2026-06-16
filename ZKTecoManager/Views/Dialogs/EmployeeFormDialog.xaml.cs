using System.Windows;
using System.Windows.Controls;
using ZKTecoManager.Models.Entities;

namespace ZKTecoManager.Views.Dialogs;

public partial class EmployeeFormDialog : Window
{
    public Employee? Result { get; private set; }
    private readonly Employee? _editing;
    private readonly IReadOnlyList<Department> _allDepartments;

    public EmployeeFormDialog(
        IReadOnlyList<Company> companies,
        IReadOnlyList<Department> allDepartments,
        Employee? editing = null)
    {
        InitializeComponent();
        _editing = editing;
        _allDepartments = allDepartments;

        CmbCompany.ItemsSource = companies;

        if (editing is not null)
        {
            TitleText.Text = "Editar Empleado";
            CmbCompany.SelectedItem = companies.FirstOrDefault(c => c.Id == editing.CompanyId);
            TxtCode.Text = editing.EmployeeCode;
            TxtFirstName.Text = editing.FirstName;
            TxtLastName.Text = editing.LastName;
            TxtPosition.Text = editing.Position ?? string.Empty;
            DpHireDate.SelectedDate = editing.HireDate.HasValue
                ? editing.HireDate.Value.ToDateTime(TimeOnly.MinValue) : null;
            TxtCard.Text = editing.CardNumber ?? string.Empty;
        }
    }

    private void CmbCompany_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbCompany.SelectedItem is not Company company)
        {
            CmbDepartment.ItemsSource = null;
            return;
        }
        var depts = _allDepartments.Where(d => d.CompanyId == company.Id).ToList();
        CmbDepartment.ItemsSource = depts;

        if (_editing is not null)
            CmbDepartment.SelectedItem = depts.FirstOrDefault(d => d.Id == _editing.DepartmentId);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        if (CmbCompany.SelectedItem is not Company company)
        { ShowError("Selecciona una empresa."); return; }
        if (string.IsNullOrWhiteSpace(TxtCode.Text))
        { ShowError("El código de empleado es obligatorio."); return; }
        if (string.IsNullOrWhiteSpace(TxtFirstName.Text))
        { ShowError("El nombre es obligatorio."); return; }
        if (string.IsNullOrWhiteSpace(TxtLastName.Text))
        { ShowError("El apellido es obligatorio."); return; }

        DateOnly? hireDate = null;
        if (DpHireDate.SelectedDate.HasValue)
            hireDate = DateOnly.FromDateTime(DpHireDate.SelectedDate.Value);

        Result = new Employee
        {
            Id = _editing?.Id ?? 0,
            CompanyId = company.Id,
            DepartmentId = (CmbDepartment.SelectedItem as Department)?.Id,
            EmployeeCode = TxtCode.Text.Trim(),
            FirstName = TxtFirstName.Text.Trim(),
            LastName = TxtLastName.Text.Trim(),
            Position = string.IsNullOrWhiteSpace(TxtPosition.Text) ? null : TxtPosition.Text.Trim(),
            HireDate = hireDate,
            CardNumber = string.IsNullOrWhiteSpace(TxtCard.Text) ? null : TxtCard.Text.Trim(),
            IsActive = _editing?.IsActive ?? true
        };

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ShowError(string msg)
    {
        ErrorText.Text = msg;
        ErrorText.Visibility = Visibility.Visible;
    }
}
