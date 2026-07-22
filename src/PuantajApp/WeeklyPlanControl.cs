using Puantaj.Core.Calendar;
using Puantaj.Core.Data;
using Puantaj.Core.Excel;

namespace PuantajApp;

internal sealed class WeeklyPlanControl : UserControl
{
    private static readonly string[] DayNames = ["Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi", "Pazar"];
    private readonly PuantajDatabase _database;
    private readonly DateTimePicker _weekStart = new() { Format = DateTimePickerFormat.Short, Width = 110 };
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, AllowUserToAddRows = false, AutoGenerateColumns = false };
    private readonly ComboBox _bulkCode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 70 };
    private readonly CheckBox[] _bulkDays = new CheckBox[7];
    private bool _loading;
    private readonly string _hotelName;
    private readonly string _departmentName;
    private readonly IReadOnlyList<AssignmentCodeDefinition> _codes;
    private bool _exporting;

    public WeeklyPlanControl(PuantajDatabase database, string hotelName, string departmentName)
    {
        _database = database;
        _hotelName = hotelName;
        _departmentName = departmentName;
        _codes = _database.GetAssignmentCodes();
        _weekStart.Value = CalendarHelper.StartOfWeek(DateOnly.FromDateTime(DateTime.Today)).ToDateTime(TimeOnly.MinValue);
        _weekStart.ValueChanged += (_, _) => Reload();
        _bulkCode.DataSource = _codes;
        _bulkCode.DisplayMember = nameof(AssignmentCodeDefinition.Description);
        _bulkCode.ValueMember = nameof(AssignmentCodeDefinition.Code);
        _bulkCode.SelectedIndex = 0;

        BuildGridColumns();
        _grid.CurrentCellDirtyStateChanged += (_, _) => { if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        _grid.CellValueChanged += GridCellValueChanged;
        _grid.DataError += (_, args) => args.ThrowException = false;

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 76, AutoScroll = true, WrapContents = true };
        top.Controls.AddRange([new Label { Text = "Hafta başlangıcı:", AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, _weekStart]);
        top.Controls.Add(ActionButton("Tümünü Seç", (_, _) => SetAllSelected(true)));
        top.Controls.Add(ActionButton("Seçimi Kaldır", (_, _) => SetAllSelected(false)));
        top.Controls.Add(new Label { Text = "Kod:", AutoSize = true, Margin = new Padding(12, 8, 3, 3) });
        top.Controls.Add(_bulkCode);
        for (var index = 0; index < 7; index++)
        {
            _bulkDays[index] = new CheckBox { Text = DayNames[index], AutoSize = true, Margin = new Padding(5, 7, 2, 2) };
            top.Controls.Add(_bulkDays[index]);
        }
        top.Controls.Add(ActionButton("Seçili Personele Uygula", (_, _) => ApplyBulk()));
        top.Controls.Add(ActionButton("Excel Olarak Kaydet", async (_, _) => await RunExportAsync(SaveExcelAsync)));
        top.Controls.Add(ActionButton("PDF Olarak Kaydet", async (_, _) => await RunExportAsync(SavePdfAsync)));
        top.Controls.Add(ActionButton("Yazdır", async (_, _) => await RunExportAsync(PrintAsync)));
        Controls.Add(_grid);
        Controls.Add(top);
        Reload();
    }

    public void Reload()
    {
        _loading = true;
        try
        {
            var monday = CalendarHelper.StartOfWeek(DateOnly.FromDateTime(_weekStart.Value));
            if (_weekStart.Value.Date != monday.ToDateTime(TimeOnly.MinValue))
                _weekStart.Value = monday.ToDateTime(TimeOnly.MinValue);
            var week = CalendarHelper.Week(monday);
            for (var index = 0; index < 7; index++)
                _grid.Columns[$"Day{index}"].HeaderText = $"{DayNames[index]}\n{week[index]:dd.MM.yyyy}";
            var assignments = _database.GetAssignments(week[0], week[6]).ToDictionary(item => (item.EmployeeId, item.WorkDate));
            _grid.Rows.Clear();
            foreach (var employee in _database.GetEmployees())
            {
                var values = new object?[10];
                values[0] = false;
                values[1] = employee.Id;
                values[2] = employee.FullName;
                for (var index = 0; index < 7; index++)
                    values[index + 3] = assignments.TryGetValue((employee.Id, week[index]), out var assignment) ? assignment.Code : null;
                _grid.Rows.Add(values);
            }
        }
        finally { _loading = false; }
    }

    private void BuildGridColumns()
    {
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Selected", HeaderText = "Seç", Width = 45 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Employee", HeaderText = "Personel", ReadOnly = true, Width = 190 });
        for (var index = 0; index < 7; index++)
        {
            var column = new DataGridViewComboBoxColumn { Name = $"Day{index}", HeaderText = DayNames[index], Width = 105, FlatStyle = FlatStyle.Flat };
            column.DataSource = _codes;
            column.DisplayMember = nameof(AssignmentCodeDefinition.Description);
            column.ValueMember = nameof(AssignmentCodeDefinition.Code);
            _grid.Columns.Add(column);
        }
        _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
    }

    private static Button ActionButton(string text, EventHandler handler)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += handler;
        return button;
    }

    private void SetAllSelected(bool selected)
    {
        foreach (DataGridViewRow row in _grid.Rows) row.Cells["Selected"].Value = selected;
    }

    private void ApplyBulk()
    {
        var employeeIds = _grid.Rows.Cast<DataGridViewRow>()
            .Where(row => Convert.ToBoolean(row.Cells["Selected"].Value))
            .Select(row => Convert.ToInt64(row.Cells["Id"].Value)).ToArray();
        var monday = CalendarHelper.StartOfWeek(DateOnly.FromDateTime(_weekStart.Value));
        var dates = _bulkDays.Select((box, index) => (box, index)).Where(item => item.box.Checked).Select(item => monday.AddDays(item.index)).ToArray();
        if (employeeIds.Length == 0 || dates.Length == 0)
        {
            MessageBox.Show("En az bir personel ve bir gün seçin.", "Puantaj", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try { _database.AssignMany(employeeIds, dates, _bulkCode.SelectedValue?.ToString() ?? string.Empty); Reload(); }
        catch (Exception exception) { MessageBox.Show(exception.Message, "Plan değiştirilemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void GridCellValueChanged(object? sender, DataGridViewCellEventArgs args)
    {
        if (_loading || args.RowIndex < 0 || args.ColumnIndex < 3) return;
        var value = _grid.Rows[args.RowIndex].Cells[args.ColumnIndex].Value?.ToString();
        if (string.IsNullOrWhiteSpace(value)) return;
        var employeeId = Convert.ToInt64(_grid.Rows[args.RowIndex].Cells["Id"].Value);
        var monday = CalendarHelper.StartOfWeek(DateOnly.FromDateTime(_weekStart.Value));
        try { _database.Assign(employeeId, monday.AddDays(args.ColumnIndex - 3), value); }
        catch (Exception exception) { MessageBox.Show(exception.Message, "Plan değiştirilemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning); Reload(); }
    }

    private Task<string> CreateExcelAsync(string outputPath)
    {
        var monday = CalendarHelper.StartOfWeek(DateOnly.FromDateTime(_weekStart.Value));
        var templates = Path.Combine(AppContext.BaseDirectory, "templates");
        var template = WeeklyExcelExporter.FindWeeklyTemplate(templates);
        var employees = _database.GetEmployees();
        var assignments = _database.GetAssignments(monday, monday.AddDays(6));
        var codes = _database.GetAssignmentCodes();
        var settings = _database.GetSettings();
        return Task.Run(() => new WeeklyExcelExporter().Export(template, outputPath,
            _hotelName, _departmentName, monday, employees, assignments, codes, settings));
    }

    private async Task RunExportAsync(Func<Task> action)
    {
        if (_exporting) return;
        _exporting = true;
        try { await action(); }
        finally { _exporting = false; }
    }

    private async Task SaveExcelAsync()
    {
        try
        {
            var monday = CalendarHelper.StartOfWeek(DateOnly.FromDateTime(_weekStart.Value));
            using var dialog = new SaveFileDialog { Filter = "Excel dosyası (*.xlsx)|*.xlsx", FileName = WeeklyExcelExporter.CreateOutputFileName(_departmentName, monday) };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            await CreateExcelAsync(dialog.FileName);
            MessageBox.Show($"Excel oluşturuldu:\n{dialog.FileName}", "Puantaj", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception exception) { ShowOutputError(exception); }
    }

    private async Task SavePdfAsync()
    {
        var monday = CalendarHelper.StartOfWeek(DateOnly.FromDateTime(_weekStart.Value));
        var pdfName = Path.ChangeExtension(WeeklyExcelExporter.CreateOutputFileName(_departmentName, monday), ".pdf");
        using var dialog = new SaveFileDialog { Filter = "PDF dosyası (*.pdf)|*.pdf", FileName = pdfName };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        await CreatePdfAsync(dialog.FileName);
    }

    private async Task PrintAsync()
    {
        var temporary = Path.Combine(Path.GetTempPath(), $"puantaj-weekly-{Guid.NewGuid():N}.xlsx");
        try
        {
            await CreateExcelAsync(temporary);
            await ExcelInteropService.RunStaAsync(() => new ExcelInteropService().PrintWithDialog(temporary));
        }
        catch (ExcelNotInstalledException)
        {
            MessageBox.Show("Yazdırmak için bu bilgisayarda Microsoft Excel kurulu olmalıdır.", "Puantaj", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception exception) { ShowOutputError(exception); }
        finally { TryDelete(temporary); }
    }

    private async Task CreatePdfAsync(string pdfPath)
    {
        var temporary = Path.Combine(Path.GetTempPath(), $"puantaj-weekly-{Guid.NewGuid():N}.xlsx");
        try
        {
            await CreateExcelAsync(temporary);
            await ExcelInteropService.RunStaAsync(() => new ExcelInteropService().ExportPdf(temporary, pdfPath));
            MessageBox.Show($"PDF oluşturuldu:\n{pdfPath}", "Puantaj", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (ExcelNotInstalledException)
        {
            var fallback = Path.ChangeExtension(pdfPath, ".xlsx");
            File.Move(temporary, fallback, true);
            ShowExcelRequired(fallback);
        }
        catch (Exception exception) { ShowOutputError(exception); }
        finally { TryDelete(temporary); }
    }

    private static void ShowExcelRequired(string? excelPath) => MessageBox.Show(
        "PDF oluşturmak için bu bilgisayarda Microsoft Excel kurulu olmalıdır. Excel dosyanız başarıyla oluşturuldu."
        + (excelPath is null ? string.Empty : $"\n\n{excelPath}"), "Puantaj", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    private static void ShowOutputError(Exception exception) => MessageBox.Show(exception.Message, "Belge oluşturulamadı", MessageBoxButtons.OK, MessageBoxIcon.Error);
    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
}
