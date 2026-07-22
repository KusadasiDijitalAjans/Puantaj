using System.Globalization;
using Puantaj.Core.Data;
using Puantaj.Core.Excel;
using Puantaj.Core.Planning;

namespace PuantajApp;

internal sealed class PersonnelCardControl : UserControl
{
    private static readonly string[] Days = ["Pzt", "Sal", "Çar", "Per", "Cum", "Cmt", "Paz"];
    private readonly PuantajDatabase _database;
    private readonly WeeklyPlanningService _planning = new();
    private readonly Func<int> _year; private readonly Func<int> _month;
    private readonly string _hotel; private readonly string _department;
    private readonly TextBox _search = new() { PlaceholderText = "Ara...", Dock = DockStyle.Top, Height = 34 };
    private readonly ListBox _employees = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 10) };
    private readonly Label _employeeName = Label("Personel seçin", 11, true);
    private readonly Label _employeeInfo = Label("", 9, false);
    private readonly FlowLayoutPanel _weekStates = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
    private readonly TabControl _weeks = new() { Dock = DockStyle.Top, Height = 70, Appearance = TabAppearance.FlatButtons, SizeMode = TabSizeMode.Fixed, ItemSize = new Size(155, 54) };
    private readonly ComboBox _defaultShift = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
    private readonly DataGridView _matrix = new() { Dock = DockStyle.Fill, AllowUserToAddRows = false, RowHeadersVisible = false, AutoGenerateColumns = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
    private readonly FlowLayoutPanel _result = new() { Dock = DockStyle.Top, Height = 132, Padding = new Padding(4), WrapContents = false };
    private readonly FlowLayoutPanel _legend = new() { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true, Padding = new Padding(8) };
    private readonly DataGridView _monthly = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, ColumnHeadersHeight = 42, BackgroundColor = Color.White };
    private readonly Label _totals = Label("", 9, true);
    private IReadOnlyList<AssignmentCodeDefinition> _codes = [];
    private IReadOnlyList<MonthWeek> _monthWeeks = [];
    private bool _loading;

    public PersonnelCardControl(PuantajDatabase database, Func<int> year, Func<int> month, string hotel, string department)
    {
        _database = database; _year = year; _month = month; _hotel = hotel; _department = department;
        Dock = DockStyle.Fill; BackColor = Color.FromArgb(246, 247, 249); Font = new Font("Segoe UI", 9);
        Controls.Add(BuildLayout());
        _search.TextChanged += (_, _) => LoadEmployees();
        _employees.SelectedIndexChanged += (_, _) => RefreshPerson();
        _weeks.SelectedIndexChanged += (_, _) => RefreshWeek();
        _matrix.CurrentCellDirtyStateChanged += (_, _) => { if (_matrix.IsCurrentCellDirty) _matrix.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        _matrix.CellValueChanged += MatrixValueChanged;
        ReloadAll();
    }

    public void ReloadAll()
    {
        _codes = _database.GetAssignmentCodes(); BuildMatrix(); BuildLegend(); LoadEmployees(); LoadMonth();
    }

    public void SaveCurrentWeek() => GenerateWeek(false);

    public void PrintCurrentWeek()
    {
        var employee = SelectedEmployee(); if (employee is null || SelectedWeek() is not { } week) return;
        var temp = Path.Combine(Path.GetTempPath(), $"puantaj-card-{Guid.NewGuid():N}.xlsx");
        try
        {
            var template = WeeklyExcelExporter.FindWeeklyTemplate(Path.Combine(AppContext.BaseDirectory, "templates"));
            new WeeklyExcelExporter().Export(template, temp, _hotel, _department, week.Monday, [employee],
                _database.GetAssignments(week.Monday, week.Sunday).Where(item => item.EmployeeId == employee.Id).ToList(), _codes, _database.GetSettings());
            new ExcelInteropService().PrintWithDialog(temp);
        }
        catch (Exception exception) { MessageBox.Show(exception.Message, "Yazdırma hatası", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { try { File.Delete(temp); } catch { } }
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(6) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 74)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 26));
        root.Controls.Add(BuildLeft(), 0, 0); root.SetRowSpan(root.GetControlFromPosition(0, 0)!, 2);
        root.Controls.Add(BuildWorkspace(), 1, 0); root.Controls.Add(BuildMonthly(), 1, 1); return root;
    }

    private Control BuildLeft()
    {
        var panel = Card();
        var title = Label("PERSONEL LİSTESİ", 9, true); title.Dock = DockStyle.Top; title.Height = 35; title.Padding = new Padding(10, 10, 0, 0);
        var listArea = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 6, 10, 6) }; listArea.Controls.Add(_employees); listArea.Controls.Add(_search);
        var info = new Panel { Dock = DockStyle.Bottom, Height = 335, Padding = new Padding(12), BackColor = Color.White };
        var avatar = Label("●", 34, false); avatar.ForeColor = Color.FromArgb(165, 169, 175); avatar.Dock = DockStyle.Top; avatar.Height = 65; avatar.TextAlign = ContentAlignment.MiddleLeft;
        _employeeName.Dock = DockStyle.Top; _employeeName.Height = 28; _employeeInfo.Dock = DockStyle.Top; _employeeInfo.Height = 55;
        var stateTitle = Label("HAFTA DURUMU", 9, true); stateTitle.Dock = DockStyle.Top; stateTitle.Height = 28;
        info.Controls.Add(_weekStates); info.Controls.Add(stateTitle); info.Controls.Add(_employeeInfo); info.Controls.Add(_employeeName); info.Controls.Add(avatar);
        panel.Controls.Add(listArea); panel.Controls.Add(info); panel.Controls.Add(title); return panel;
    }

    private Control BuildWorkspace()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6, 0, 0, 6) }; panel.Controls.Add(BuildMiddle()); panel.Controls.Add(_weeks); return panel;
    }

    private Control BuildMiddle()
    {
        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 600, BackColor = Color.FromArgb(220, 224, 230) };
        split.Panel1.Padding = new Padding(6); split.Panel2.Padding = new Padding(6); split.Panel1.BackColor = split.Panel2.BackColor = Color.White;
        var leftTitle = Label("HAFTALIK VARDİYA VE İZİN SEÇİMİ", 9, true); leftTitle.Dock = DockStyle.Top; leftTitle.Height = 30;
        var hint = Label("Önce varsayılan çalışma vardiyasını seçin. İstisna günlerini işaretleyip Haftayı Oluştur'a basın.", 9, false);
        hint.Dock = DockStyle.Top; hint.Height = 50; hint.BackColor = Color.FromArgb(239, 247, 255); hint.Padding = new Padding(10);
        var shiftBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42 }; shiftBar.Controls.Add(Label("Varsayılan Çalışma Vardiyası:", 9, true)); shiftBar.Controls.Add(_defaultShift);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft };
        actions.Controls.Add(Button("✓  Haftayı Oluştur", (_, _) => GenerateWeek(false), Color.FromArgb(31, 87, 180), Color.White));
        actions.Controls.Add(Button("Seçimleri Temizle", (_, _) => ClearMatrix()));
        split.Panel1.Controls.Add(_matrix); split.Panel1.Controls.Add(actions); split.Panel1.Controls.Add(shiftBar); split.Panel1.Controls.Add(hint); split.Panel1.Controls.Add(leftTitle);

        var rightTitle = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, WrapContents = false };
        var rightCaption = Label("HAFTALIK SONUÇ", 10, true); rightCaption.Width = 220; rightCaption.Height = 34;
        rightTitle.Controls.Add(rightCaption); rightTitle.Controls.Add(Button("▣ Haftayı Kopyala", (_, _) => CopyWeek()));
        var info = Label("Bilgi\nOluşturulan hafta üzerinde gün kutusuna çift tıklayarak değişiklik yapabilirsiniz.", 9, false);
        info.Dock = DockStyle.Bottom; info.Height = 82; info.BackColor = Color.FromArgb(255, 249, 231); info.Padding = new Padding(12);
        var rebuild = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 72, Padding = new Padding(8) };
        rebuild.Controls.Add(Button("Haftayı Yeniden Oluştur", (_, _) => GenerateWeek(true)));
        split.Panel2.Controls.Add(_legend); split.Panel2.Controls.Add(info); split.Panel2.Controls.Add(rebuild); split.Panel2.Controls.Add(_result); split.Panel2.Controls.Add(rightTitle);
        return split;
    }

    private Control BuildMonthly()
    {
        var card = Card(); var title = Label("AYLIK PUANTAJ ÖNİZLEMESİ", 9, true); title.Dock = DockStyle.Top; title.Height = 28; title.Padding = new Padding(8, 7, 0, 0);
        _totals.Dock = DockStyle.Bottom; _totals.Height = 32; _totals.TextAlign = ContentAlignment.MiddleRight; _totals.Padding = new Padding(0, 0, 12, 0);
        card.Controls.Add(_monthly); card.Controls.Add(_totals); card.Controls.Add(title); return card;
    }

    private void LoadEmployees()
    {
        var selectedId = SelectedEmployee()?.Id; var filter = _search.Text.Trim();
        var values = _database.GetEmployees().Where(item => item.FullName.Contains(filter, StringComparison.CurrentCultureIgnoreCase)).ToList();
        _employees.DataSource = null; _employees.DataSource = values; _employees.DisplayMember = nameof(Employee.FullName);
        if (selectedId is not null) _employees.SelectedItem = values.FirstOrDefault(item => item.Id == selectedId);
        if (_employees.SelectedIndex < 0 && values.Count > 0) _employees.SelectedIndex = 0;
    }

    private void LoadMonth()
    {
        _monthWeeks = _planning.GetMonthWeeks(_year(), _month()); _loading = true;
        _weeks.TabPages.Clear();
        foreach (var week in _monthWeeks) _weeks.TabPages.Add(new TabPage($"{week.Number}. Hafta\n{week.ActiveFrom:dd} – {week.ActiveTo:dd} {MonthName()}") { Tag = week });
        _loading = false; RefreshPerson();
    }

    private void RefreshPerson()
    {
        var employee = SelectedEmployee();
        _employeeName.Text = employee?.FullName ?? "Personel seçin";
        _employeeInfo.Text = employee is null ? "" : $"{(string.IsNullOrWhiteSpace(employee.Position) ? _department : employee.Position)}\nÇalışma Şekli: {(string.IsNullOrWhiteSpace(employee.WorkPattern) ? "Belirtilmedi" : employee.WorkPattern)}{(employee.HireDate is null ? "" : $"  •  Giriş: {employee.HireDate:dd.MM.yyyy}")}\nAktif dönem: {MonthName()} {_year()}";
        RefreshStates(); RefreshWeek(); RefreshMonthly();
    }

    private void RefreshStates()
    {
        _weekStates.Controls.Clear(); var employee = SelectedEmployee(); if (employee is null) return;
        var monthAssignments = MonthAssignments(employee.Id).ToDictionary(item => item.WorkDate, item => item.Code);
        foreach (var week in _monthWeeks)
        {
            var status = _planning.GetStatus(week, monthAssignments);
            var color = status switch { WeekCompletionStatus.Completed => Color.FromArgb(55, 170, 95), WeekCompletionStatus.Missing => Color.FromArgb(226, 112, 42), _ => Color.FromArgb(248, 184, 55) };
            var text = status switch { WeekCompletionStatus.Completed => "Dolduruldu", WeekCompletionStatus.Missing => "Eksik", _ => "Bekliyor" };
            var label = Label($"{week.Number}. Hafta ({week.ActiveFrom:dd} – {week.ActiveTo:dd})     ● {text}", 8, false); label.Width = 245; label.Height = 29; label.ForeColor = color; _weekStates.Controls.Add(label);
        }
    }

    private void BuildMatrix()
    {
        _loading = true; _matrix.Columns.Clear(); _matrix.Columns.Add(new DataGridViewTextBoxColumn { Name = "Definition", HeaderText = "", Width = 170, ReadOnly = true });
        for (var index = 0; index < 7; index++) _matrix.Columns.Add(new DataGridViewCheckBoxColumn { Name = $"Day{index}", HeaderText = Days[index], Width = 52 });
        _matrix.Rows.Clear(); foreach (var code in _codes) { var row = _matrix.Rows[_matrix.Rows.Add(code.Description)]; row.Tag = code; row.DefaultCellStyle.BackColor = CodeColor(code); }
        var workCodes = _codes.Where(item => item.IsWorkShift).ToList(); _defaultShift.DataSource = null; _defaultShift.DataSource = workCodes; _defaultShift.DisplayMember = nameof(AssignmentCodeDefinition.Description); _defaultShift.ValueMember = nameof(AssignmentCodeDefinition.Code);
        _loading = false;
    }

    private void BuildLegend()
    {
        _legend.Controls.Clear(); foreach (var code in _codes) { var label = Label($"■  {code.Code}: {code.Description}", 8, false); label.ForeColor = CodeColor(code); label.Width = 170; label.Height = 28; _legend.Controls.Add(label); }
    }

    private void RefreshWeek()
    {
        if (_loading) return;
        ClearMatrix();
        if (SelectedEmployee() is not { } employee || SelectedWeek() is not { } week) return;
        var map = _database.GetAssignments(week.Monday, week.Sunday).Where(item => item.EmployeeId == employee.Id).ToDictionary(item => item.WorkDate, item => item.Code);
        _result.Controls.Clear();
        for (var offset = 0; offset < 7; offset++)
        {
            var date = week.Monday.AddDays(offset); var inMonth = date >= week.ActiveFrom && date <= week.ActiveTo;
            map.TryGetValue(date, out var code); var definition = code is null ? null : _codes.FirstOrDefault(item => item.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
            var box = new Label { Width = 76, Height = 112, TextAlign = ContentAlignment.MiddleCenter, BorderStyle = BorderStyle.FixedSingle,
                Text = $"{Days[offset]}\n{date:dd}\n\n{code ?? "–"}", BackColor = inMonth ? definition is null ? Color.White : CodeColor(definition) : Color.FromArgb(225, 227, 230), Tag = date };
            box.DoubleClick += (_, _) => EditDay(date); _result.Controls.Add(box);
        }
        SetMatrixEnabledDays(week); 
    }

    private void RefreshMonthly()
    {
        _monthly.Columns.Clear(); _monthly.Rows.Clear(); var employee = SelectedEmployee(); if (employee is null) return;
        var days = DateTime.DaysInMonth(_year(), _month()); _monthly.Columns.Add("Person", "Personel"); _monthly.Columns[0].Width = 145;
        for (var day = 1; day <= days; day++) { var date = new DateOnly(_year(), _month(), day); _monthly.Columns.Add($"D{day}", $"{day}\n{Days[((int)date.DayOfWeek + 6) % 7]}"); _monthly.Columns[^1].Width = 43; }
        var assignments = MonthAssignments(employee.Id); var map = assignments.ToDictionary(item => item.WorkDate, item => item.Code); var values = new object?[days + 1]; values[0] = employee.FullName;
        var resolver = new AssignmentCodeResolver(_codes); var ended = false;
        for (var day = 1; day <= days; day++)
        {
            var date = new DateOnly(_year(), _month(), day); if (map.TryGetValue(date, out var code) && resolver.Resolve(code).IsEmploymentEnded) ended = true;
            values[day] = ended ? "" : map.GetValueOrDefault(date);
        }
        var rowIndex = _monthly.Rows.Add(values); ended = false;
        for (var day = 1; day <= days; day++) { var date = new DateOnly(_year(), _month(), day); if (map.TryGetValue(date, out var code) && resolver.Resolve(code).IsEmploymentEnded) ended = true; _monthly.Rows[rowIndex].Cells[day].Style.BackColor = ended ? Color.Black : code is null ? Color.White : CodeColor(resolver.Resolve(code)); }
        var totals = _planning.CalculateTotals(assignments, _codes); _totals.Text = $"Toplam Çalışma Günü: {totals.WorkDays}     |     Toplam İzin Günü: {totals.LeaveDays}     |     Toplam Geçerli Gün: {totals.ValidDays}";
    }

    private void GenerateWeek(bool confirmOverwrite)
    {
        var employee = SelectedEmployee(); var week = SelectedWeek(); if (employee is null || week is null) return;
        try
        {
            _database.EnsureMonthUnlocked(_year(), _month());
            if (confirmOverwrite && MessageBox.Show("Mevcut hafta yeniden oluşturulacak. Devam edilsin mi?", "Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            var exceptions = new Dictionary<DateOnly, string>();
            for (var day = 0; day < 7; day++) foreach (DataGridViewRow row in _matrix.Rows)
                if (Convert.ToBoolean(row.Cells[day + 1].Value) && row.Tag is AssignmentCodeDefinition definition) exceptions[week.Monday.AddDays(day)] = definition.Code;
            var built = _planning.BuildWeek(week, _defaultShift.SelectedValue?.ToString(), exceptions);
            foreach (var group in built.GroupBy(item => item.Value)) _database.AssignMany([employee.Id], group.Select(item => item.Key), group.Key);
            ClearMatrix(); RefreshPerson();
        }
        catch (Exception exception) { MessageBox.Show(exception.Message, "Hafta oluşturulamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void EditDay(DateOnly date)
    {
        var employee = SelectedEmployee(); if (employee is null) return;
        try { _database.EnsureMonthUnlocked(date.Year, date.Month); var selected = CodeChoiceDialog.Select(this, _codes); if (selected is null) return; _database.Assign(employee.Id, date, selected.Code); RefreshPerson(); }
        catch (Exception exception) { MessageBox.Show(exception.Message, "Gün değiştirilemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void CopyWeek()
    {
        var sourceEmployee = SelectedEmployee(); var sourceWeek = SelectedWeek(); if (sourceEmployee is null || sourceWeek is null) return;
        using var dialog = new CopyWeekForm(_database.GetEmployees(), _monthWeeks, sourceEmployee, sourceWeek);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.TargetWeek is null || dialog.EmployeeIds.Count == 0) return;
        try
        {
            _database.EnsureMonthUnlocked(_year(), _month());
            var sourceMap = _database.GetAssignments(sourceWeek.Monday, sourceWeek.Sunday).Where(item => item.EmployeeId == sourceEmployee.Id).ToDictionary(item => item.WorkDate, item => item.Code);
            var copied = _planning.CopyToWeek(sourceWeek, dialog.TargetWeek, sourceMap);
            var existing = _database.GetAssignments(dialog.TargetWeek.ActiveFrom, dialog.TargetWeek.ActiveTo).Any(item => dialog.EmployeeIds.Contains(item.EmployeeId));
            if (existing && MessageBox.Show("Hedefte mevcut kayıtlar var. Üzerine yazılsın mı?", "Kopyalama onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            foreach (var group in copied.GroupBy(item => item.Value)) _database.AssignMany(dialog.EmployeeIds, group.Select(item => item.Key), group.Key);
            RefreshPerson();
        }
        catch (Exception exception) { MessageBox.Show(exception.Message, "Hafta kopyalanamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void MatrixValueChanged(object? sender, DataGridViewCellEventArgs args)
    {
        if (_loading || args.RowIndex < 0 || args.ColumnIndex <= 0 || !Convert.ToBoolean(_matrix.Rows[args.RowIndex].Cells[args.ColumnIndex].Value)) return;
        _loading = true; foreach (DataGridViewRow row in _matrix.Rows) if (row.Index != args.RowIndex) row.Cells[args.ColumnIndex].Value = false; _loading = false;
    }

    private void SetMatrixEnabledDays(MonthWeek week) { for (var day = 0; day < 7; day++) { var enabled = week.Monday.AddDays(day) >= week.ActiveFrom && week.Monday.AddDays(day) <= week.ActiveTo; foreach (DataGridViewRow row in _matrix.Rows) { row.Cells[day + 1].ReadOnly = !enabled; if (!enabled) { row.Cells[day + 1].Value = false; row.Cells[day + 1].Style.BackColor = Color.LightGray; } } } }
    private void ClearMatrix() { _loading = true; foreach (DataGridViewRow row in _matrix.Rows) for (var column = 1; column < row.Cells.Count; column++) row.Cells[column].Value = false; _loading = false; }
    private IReadOnlyList<Assignment> MonthAssignments(long employeeId) { var from = new DateOnly(_year(), _month(), 1); return _database.GetAssignments(from, from.AddMonths(1).AddDays(-1)).Where(item => item.EmployeeId == employeeId).ToList(); }
    private Employee? SelectedEmployee() => _employees.SelectedItem as Employee;
    private MonthWeek? SelectedWeek() => _weeks.SelectedTab?.Tag as MonthWeek;
    private string MonthName() => CultureInfo.GetCultureInfo("tr-TR").DateTimeFormat.GetMonthName(_month());
    private static Color CodeColor(AssignmentCodeDefinition value) { if (value.IsEmploymentEnded) return Color.FromArgb(70, 70, 75); var colors = new[] { Color.FromArgb(213, 235, 250), Color.FromArgb(218, 240, 210), Color.FromArgb(255, 238, 190), Color.FromArgb(255, 214, 214), Color.FromArgb(230, 220, 250), Color.FromArgb(203, 239, 241), Color.FromArgb(213, 239, 201), Color.FromArgb(255, 224, 199) }; return colors[Math.Abs(value.DisplayOrder - 1) % colors.Length]; }
    private static Panel Card() => new() { Dock = DockStyle.Fill, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
    private static Label Label(string text, float size, bool bold) => new() { Text = text, AutoSize = false, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular) };
    private static Button Button(string text, EventHandler handler, Color? back = null, Color? fore = null) { var button = new Button { Text = text, AutoSize = true, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = back ?? Color.White, ForeColor = fore ?? Color.FromArgb(25, 32, 43) }; button.Click += handler; return button; }
}

internal static class CodeChoiceDialog
{
    public static AssignmentCodeDefinition? Select(IWin32Window owner, IReadOnlyList<AssignmentCodeDefinition> codes)
    {
        using var form = new Form { Text = "Gün Atamasını Değiştir", StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(360, 115), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
        var combo = new ComboBox { Left = 15, Top = 15, Width = 330, DropDownStyle = ComboBoxStyle.DropDownList, DataSource = codes.ToList(), DisplayMember = nameof(AssignmentCodeDefinition.Description) };
        var ok = new Button { Text = "Kaydet", Left = 245, Top = 60, Width = 100, DialogResult = DialogResult.OK }; form.Controls.Add(combo); form.Controls.Add(ok); form.AcceptButton = ok;
        return form.ShowDialog(owner) == DialogResult.OK ? combo.SelectedItem as AssignmentCodeDefinition : null;
    }
}

internal sealed class CopyWeekForm : Form
{
    private readonly CheckedListBox _employees = new() { Dock = DockStyle.Fill, CheckOnClick = true };
    private readonly ComboBox _week = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    public MonthWeek? TargetWeek => (_week.SelectedItem as WeekItem)?.Value;
    public IReadOnlyList<long> EmployeeIds => _employees.CheckedItems.Cast<EmployeeItem>().Select(item => item.Value.Id).ToList();

    public CopyWeekForm(IReadOnlyList<Employee> employees, IReadOnlyList<MonthWeek> weeks, Employee selected, MonthWeek source)
    {
        Text = "Haftayı Kopyala"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(430, 430);
        _week.DataSource = weeks.Select(item => new WeekItem(item)).ToList(); _week.DisplayMember = nameof(WeekItem.Text);
        var next = weeks.ToList().FindIndex(item => item.Number == source.Number) + 1; if (next < weeks.Count) _week.SelectedIndex = next;
        foreach (var employee in employees) { var index = _employees.Items.Add(new EmployeeItem(employee)); if (employee.Id == selected.Id) _employees.SetItemChecked(index, true); }
        var ok = new Button { Text = "Kopyala", Dock = DockStyle.Bottom, Height = 42, DialogResult = DialogResult.OK };
        Controls.Add(_employees); Controls.Add(new Label { Text = "Hedef hafta", Dock = DockStyle.Top, Height = 24 }); Controls.Add(_week); Controls.Add(ok); AcceptButton = ok;
    }
    private sealed record EmployeeItem(Employee Value) { public override string ToString() => Value.FullName; }
    private sealed record WeekItem(MonthWeek Value) { public string Text => $"{Value.Number}. Hafta ({Value.ActiveFrom:dd.MM}–{Value.ActiveTo:dd.MM})"; public static implicit operator MonthWeek(WeekItem value) => value.Value; }
}
