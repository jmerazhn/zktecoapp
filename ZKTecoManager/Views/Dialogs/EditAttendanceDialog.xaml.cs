using System.Windows;
using ZKTecoManager.Models.Entities;
using ZKTecoManager.Models.Enums;

namespace ZKTecoManager.Views.Dialogs;

public partial class EditAttendanceDialog : Window
{
    public (AttendanceStatus Status, string? Notes) Result { get; private set; }

    private static readonly (AttendanceStatus Value, string Label)[] Statuses =
    [
        (AttendanceStatus.Normal,    "Normal"),
        (AttendanceStatus.Late,      "Tardanza"),
        (AttendanceStatus.Absent,    "Falta"),
        (AttendanceStatus.Justified, "Justificado"),
        (AttendanceStatus.DayOff,    "Descanso"),
        (AttendanceStatus.Pending,   "Pendiente"),
    ];

    public EditAttendanceDialog(AttendanceRecord record)
    {
        InitializeComponent();

        TxtEmployee.Text = record.Employee.FullName;
        TxtDate.Text     = record.WorkDate.ToString("dddd, dd/MM/yyyy");
        TxtNotes.Text    = record.Notes ?? string.Empty;

        CmbStatus.ItemsSource   = Statuses.Select(s => s.Label).ToList();
        CmbStatus.SelectedIndex = Array.FindIndex(Statuses, s => s.Value == record.Status);
        if (CmbStatus.SelectedIndex < 0) CmbStatus.SelectedIndex = 0;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var idx    = CmbStatus.SelectedIndex;
        var status = idx >= 0 ? Statuses[idx].Value : AttendanceStatus.Pending;
        var notes  = string.IsNullOrWhiteSpace(TxtNotes.Text) ? null : TxtNotes.Text.Trim();
        Result     = (status, notes);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
