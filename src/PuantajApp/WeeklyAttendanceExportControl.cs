using System.Diagnostics;
using System.Globalization;
using Puantaj.Core.Calendar;
using Puantaj.Core.Data;
using Puantaj.Core.Excel;

namespace PuantajApp;

internal sealed class WeeklyAttendanceExportControl : UserControl
{
    private readonly PuantajDatabase _database;
    private readonly bool _shiftBased;
    private readonly NumericUpDown _year = new() { Minimum = 2000, Maximum = 2100, Width = 90 };
    private readonly ComboBox _month = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly ComboBox _week = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250 };
    private readonly ComboBox _format = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
    private readonly Button _save = new() { Text = "Bilgisayara Kaydet", AutoSize = true };
    private readonly Label _status = new() { AutoSize = true };

    public WeeklyAttendanceExportControl(PuantajDatabase database, int selectedYear, int selectedMonth, bool shiftBased = false)
    {
        _database = database;
        _shiftBased = shiftBased;
        _year.Value = selectedYear;
        _month.Items.AddRange(CultureInfo.GetCultureInfo("tr-TR").DateTimeFormat.MonthNames.Take(12).Cast<object>().ToArray());
        _month.SelectedIndex = selectedMonth - 1;
        _format.Items.AddRange(["Excel", "PDF"]); _format.SelectedIndex = 0;
        _year.ValueChanged += (_, _) => ReloadWeeks(); _month.SelectedIndexChanged += (_, _) => ReloadWeeks();
        _save.Click += async (_, _) => await SaveAsync();

        var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(22), ColumnCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        AddRow(layout, 0, "Puantaj dönemi", PeriodPanel());
        AddRow(layout, 1, "Haftalık tarih aralığı", _week);
        AddRow(layout, 2, "Çıktı formatı", _format);
        AddRow(layout, 3, string.Empty, _save);
        AddRow(layout, 4, string.Empty, _status);
        Controls.Add(layout); ReloadWeeks();
    }

    private Control PeriodPanel()
    {
        var panel = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        panel.Controls.Add(_year); panel.Controls.Add(_month); return panel;
    }

    private static void AddRow(TableLayoutPanel panel, int row, string label, Control control)
    {
        panel.RowCount = row + 1;
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 8, 8, 8) }, 0, row);
        control.Margin = new Padding(0, 5, 0, 5); panel.Controls.Add(control, 1, row);
    }

    private void ReloadWeeks()
    {
        _week.Items.Clear();
        foreach (var monday in CalendarHelper.WeeksIntersectingMonth((int)_year.Value, _month.SelectedIndex + 1))
            _week.Items.Add(new WeekItem(monday, WeeklyExcelExporter.FormatWeekRange(monday)));
        if (_week.Items.Count > 0) _week.SelectedIndex = 0;
    }

    private async Task SaveAsync()
    {
        if (_week.SelectedItem is not WeekItem selected) return;
        var isPdf = _format.SelectedIndex == 1;
        var extension = isPdf ? ".pdf" : ".xlsx";
        using var dialog = new SaveFileDialog
        {
            Filter = isPdf ? "PDF dosyası (*.pdf)|*.pdf" : "Excel dosyası (*.xlsx)|*.xlsx",
            FileName = _shiftBased ? ShiftBasedWeeklyExcelExporter.CreateOutputFileName(selected.Monday, extension)
                : WeeklyExcelExporter.CreateOutputFileName(selected.Monday, extension)
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _save.Enabled = false; _status.Text = "Hazırlanıyor…";
        var temporary = isPdf ? Path.Combine(Path.GetTempPath(), $"puantaj-weekly-{Guid.NewGuid():N}.xlsx") : dialog.FileName;
        try
        {
            await CreateExcelAsync(selected.Monday, temporary);
            if (isPdf) await ExcelInteropService.RunStaAsync(() => new ExcelInteropService().ExportPdf(temporary, dialog.FileName));
            MessageBox.Show($"{(isPdf ? "PDF" : "Excel")} oluşturuldu:\n{dialog.FileName}", "Haftalık Puantaj", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (ExcelNotInstalledException)
        {
            MessageBox.Show("PDF oluşturmak için Microsoft Excel kurulu olmalıdır.", "Haftalık Puantaj", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception exception) { MessageBox.Show(exception.Message, "Haftalık Puantaj oluşturulamadı", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { if (isPdf) TryDelete(temporary); _status.Text = string.Empty; _save.Enabled = true; }
    }

    private Task CreateExcelAsync(DateOnly monday, string outputPath)
    {
        var sunday = monday.AddDays(6);
        var employees = _database.GetEmployeesForPeriod(monday, sunday);
        var assignments = _database.GetAssignments(monday, sunday);
        var definitions = _database.GetAssignmentCodes(false);
        var activeDefinitions = _database.GetAssignmentCodes();
        var settings = _database.GetSettings();
        var templates = Path.Combine(AppContext.BaseDirectory, "templates");
        if (_shiftBased)
        {
            var template = ShiftBasedWeeklyExcelExporter.FindTemplate(templates);
            return Task.Run(() => new ShiftBasedWeeklyExcelExporter().Export(template, outputPath, settings.HotelName,
                settings.DepartmentName, monday, employees, assignments, definitions, settings, activeDefinitions));
        }
        var weeklyTemplate = WeeklyExcelExporter.FindWeeklyTemplate(templates);
        return Task.Run(() => new WeeklyExcelExporter().Export(weeklyTemplate, outputPath, settings.HotelName,
            settings.DepartmentName, monday, employees, assignments, definitions, settings));
    }

    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    private sealed record WeekItem(DateOnly Monday, string Text) { public override string ToString() => Text; }
}
