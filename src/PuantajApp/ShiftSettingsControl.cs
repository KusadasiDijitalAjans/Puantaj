using System.Globalization;
using Puantaj.Core.Data;

namespace PuantajApp;

internal sealed class ShiftSettingsControl : UserControl
{
    private readonly PuantajDatabase _database;
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, AllowUserToAddRows = true, AutoGenerateColumns = false };

    public ShiftSettingsControl(PuantajDatabase database)
    {
        _database = database;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Code", HeaderText = "Kod", Width = 80 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "Tanım", Width = 180 });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Work", HeaderText = "Vardiya", Width = 70 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Start", HeaderText = "Başlangıç (SS:DD)", Width = 150 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "End", HeaderText = "Bitiş (SS:DD)", Width = 150 });
        var save = new Button { Text = "Vardiya Saatlerini Kaydet", AutoSize = true, Dock = DockStyle.Top };
        save.Click += (_, _) => SaveChanges();
        Controls.Add(_grid);
        Controls.Add(save);
        Reload();
    }

    private void Reload()
    {
        _grid.Rows.Clear();
        foreach (var shift in _database.GetAssignmentCodes())
            _grid.Rows.Add(shift.Code, shift.Description, shift.IsWorkShift, Format(shift.StartTime), Format(shift.EndTime));
    }

    public void SaveChanges(bool showConfirmation = true)
    {
        try
        {
            _grid.EndEdit();
            var definitions = new List<AssignmentCodeDefinition>();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                var code = row.Cells["Code"].Value?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(code)) continue;
                var work = Convert.ToBoolean(row.Cells["Work"].Value);
                TimeSpan? start = work ? Parse(row.Cells["Start"].Value?.ToString()) : null;
                TimeSpan? end = work ? Parse(row.Cells["End"].Value?.ToString()) : null;
                definitions.Add(new AssignmentCodeDefinition(code, row.Cells["Description"].Value?.ToString() ?? string.Empty,
                    start, end, work, definitions.Count + 1));
            }
            _database.SynchronizeAssignmentCodes(definitions);
            if (showConfirmation) MessageBox.Show("Vardiya saatleri kaydedildi.", "Puantaj", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Reload();
        }
        catch (Exception exception)
        {
            if (showConfirmation) MessageBox.Show(exception.Message, "Kayıt hatası", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else throw;
        }
    }

    private static string Format(TimeSpan? time, bool midnightAs24 = false) =>
        midnightAs24 && time == TimeSpan.Zero ? "24:00" : time?.ToString(@"hh\:mm", CultureInfo.InvariantCulture) ?? string.Empty;
    private static TimeSpan Parse(string? value) =>
        value == "24:00" ? TimeSpan.Zero :
        TimeSpan.TryParseExact(value, @"hh\:mm", CultureInfo.InvariantCulture, out var result) && result < TimeSpan.FromDays(1)
            ? result
            : throw new FormatException($"Geçersiz saat: {value}. SS:DD biçimini kullanın.");
}
