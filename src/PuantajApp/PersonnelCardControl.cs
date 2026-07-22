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
    private readonly MonthlySummaryService _summaryService = new();
    private readonly Func<int> _year; private readonly Func<int> _month;
    private readonly string _hotel; private readonly string _department;
    private readonly TextBox _search = new() { PlaceholderText = "Ara...", Dock = DockStyle.Top, Height = 34 };
    private readonly ListBox _employees = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 10) };
    private readonly Label _employeeName = Label("Personel seçin", 11, true);
    private readonly Label _employeeInfo = Label("", 9, false);
    private readonly FlowLayoutPanel _weekStates = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
    private readonly TabControl _weeks = new() { Dock = DockStyle.Top, Height = 70, Appearance = TabAppearance.FlatButtons, SizeMode = TabSizeMode.Fixed, ItemSize = new Size(155, 54) };
    private readonly ComboBox _defaultShift = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
    private readonly DataGridView _matrix = new() { Dock = DockStyle.Fill, AllowUserToAddRows = false, RowHeadersVisible = false, AutoGenerateColumns = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.FixedSingle, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, AllowUserToResizeColumns = false };
    private readonly Button _editButton;
    private readonly Button _clearButton;
    private readonly Button _generateButton;
    private readonly Button _copyButton;
    private readonly Button _copyMonthButton;
    private readonly DataGridView _monthly = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, ColumnHeadersHeight = 42, BackgroundColor = Color.White };
    private readonly MonthlySummaryPanel _monthlySummary = new();
    private IReadOnlyList<AssignmentCodeDefinition> _codes = [];
    private IReadOnlyList<MonthWeek> _monthWeeks = [];
    private bool _loading;
    private bool _editing;

    public PersonnelCardControl(PuantajDatabase database, Func<int> year, Func<int> month, string hotel, string department)
    {
        _database = database; _year = year; _month = month; _hotel = hotel; _department = department;
        _editButton = Button("✎  Düzenle", (_, _) => BeginEdit());
        _clearButton = Button("▣  Tümünü Temizle", (_, _) => ClearSelectionsWithConfirmation());
        _generateButton = Button("✓  Haftayı Oluştur", (_, _) => GenerateWeek(), Color.FromArgb(13, 104, 220), Color.White);
        _copyButton = Button("▣  Haftayı Kopyala", (_, _) => CopyWeek());
        _copyMonthButton = Button("▣  Ayı Kopyala", (_, _) => CopyMonth());
        Dock = DockStyle.Fill; BackColor = Color.FromArgb(246, 247, 249); Font = new Font("Segoe UI", 9);
        Controls.Add(BuildLayout());
        _search.TextChanged += (_, _) => LoadEmployees();
        _employees.SelectedIndexChanged += (_, _) => { _editing = false; RefreshPerson(); };
        _weeks.SelectedIndexChanged += (_, _) => { _editing = false; RefreshWeek(); };
        _matrix.CurrentCellDirtyStateChanged += (_, _) => { if (_matrix.IsCurrentCellDirty) _matrix.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        _matrix.CellValueChanged += MatrixValueChanged;
        ReloadAll();
    }

    public void ReloadAll()
    {
        _codes = _database.GetAssignmentCodes(); BuildMatrix(); LoadEmployees(); LoadMonth();
    }

    public void SaveCurrentWeek() => GenerateWeek();

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
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6, 0, 0, 6) };
        var weekBar = new Panel { Dock = DockStyle.Top, Height = 70 };
        _copyButton.Dock = DockStyle.Right; _copyButton.Width = 155; _copyMonthButton.Dock = DockStyle.Right; _copyMonthButton.Width = 135; _weeks.Dock = DockStyle.Fill;
        weekBar.Controls.Add(_weeks); weekBar.Controls.Add(_copyMonthButton); weekBar.Controls.Add(_copyButton);
        panel.Controls.Add(BuildMiddle()); panel.Controls.Add(weekBar); return panel;
    }

    private Control BuildMiddle()
    {
        var card = Card(); card.Padding = new Padding(16, 12, 16, 12);
        var leftTitle = Label("▣  HAFTALIK VARDİYA VE İZİN SEÇİMİ", 11, true); leftTitle.Dock = DockStyle.Top; leftTitle.Height = 38;
        var hint = Label("Önce varsayılan çalışma vardiyasını seçin. İstisna günlerini işaretleyip Haftayı Oluştur'a basın.", 9, false);
        hint.Dock = DockStyle.Top; hint.Height = 42; hint.BackColor = Color.FromArgb(237, 246, 255); hint.ForeColor = Color.FromArgb(31, 82, 145); hint.Padding = new Padding(12);
        var shiftBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(0, 9, 0, 3) };
        shiftBar.Controls.Add(Label("Varsayılan vardiya", 9, true)); shiftBar.Controls.Add(_defaultShift);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 56, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 9, 0, 3), WrapContents = false };
        _generateButton.Width = 160; _clearButton.Width = 145; _editButton.Width = 120;
        actions.Controls.Add(_generateButton); actions.Controls.Add(_clearButton); actions.Controls.Add(_editButton);
        card.Controls.Add(_matrix); card.Controls.Add(actions); card.Controls.Add(shiftBar); card.Controls.Add(hint); card.Controls.Add(leftTitle);
        return card;
    }

    private Control BuildMonthly()
    {
        var card = Card(); var title = Label("AYLIK PUANTAJ ÖNİZLEMESİ", 9, true); title.Dock = DockStyle.Top; title.Height = 28; title.Padding = new Padding(8, 7, 0, 0);
        card.Controls.Add(_monthly); card.Controls.Add(_monthlySummary); card.Controls.Add(title); return card;
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
        _loading = true; _matrix.Columns.Clear();
        _matrix.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _matrix.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 252); _matrix.EnableHeadersVisualStyles = false;
        _matrix.GridColor = Color.FromArgb(224, 228, 234); _matrix.RowTemplate.Height = 29;
        _matrix.Columns.Add(new DataGridViewTextBoxColumn { Name = "Definition", HeaderText = "Vardiyalar / Günler", FillWeight = 155, MinimumWidth = 190, ReadOnly = true });
        for (var index = 0; index < 7; index++) _matrix.Columns.Add(new DataGridViewCheckBoxColumn { Name = $"Day{index}", HeaderText = Days[index], FillWeight = 100, MinimumWidth = 68, FlatStyle = FlatStyle.Standard });
        _matrix.Rows.Clear();
        foreach (var code in _codes)
        {
            var row = _matrix.Rows[_matrix.Rows.Add(code.Description)]; row.Tag = code;
            row.Cells[0].Style.BackColor = CodeColor(code); row.Cells[0].Style.Padding = new Padding(10, 0, 0, 0);
            for (var day = 1; day <= 7; day++) row.Cells[day].Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }
        var workCodes = _codes.Where(item => item.IsWorkShift).ToList(); _defaultShift.DataSource = null; _defaultShift.DataSource = workCodes; _defaultShift.DisplayMember = nameof(AssignmentCodeDefinition.Description); _defaultShift.ValueMember = nameof(AssignmentCodeDefinition.Code);
        _loading = false;
    }

    private void RefreshWeek()
    {
        if (_loading) return;
        ClearMatrix();
        if (SelectedEmployee() is not { } employee || SelectedWeek() is not { } week)
        {
            _editButton.Enabled = false; _copyButton.Enabled = false; _copyMonthButton.Enabled = false; return;
        }
        for (var offset = 0; offset < 7; offset++)
        {
            var date = week.Monday.AddDays(offset);
            _matrix.Columns[offset + 1].HeaderText = $"{Days[offset]} {date:dd}";
        }
        SetMatrixEnabledDays(week);
        var existing = _database.GetAssignments(week.ActiveFrom, week.ActiveTo).Where(item => item.EmployeeId == employee.Id).ToList();
        _editButton.Enabled = existing.Count > 0; _copyButton.Enabled = _planning.GetCopyTargets(_monthWeeks, week).Count > 0;
        _copyMonthButton.Enabled = _database.GetEmployees().Count > 1;
        _clearButton.Enabled = existing.Count == 0 || _editing; _defaultShift.Enabled = existing.Count == 0 || _editing;
        _generateButton.Text = _editing ? "✓  Değişiklikleri Kaydet" : "✓  Haftayı Oluştur";
        if (existing.Count > 0) LoadExistingSelections(existing);
        if (existing.Count > 0 && !_editing) SetActiveMatrixReadOnly(week, true);
    }

    private void RefreshMonthly()
    {
        _monthly.Columns.Clear(); _monthly.Rows.Clear(); var employee = SelectedEmployee();
        if (employee is null) { _monthlySummary.SetSummary(new MonthlySummary([], 0, 0, 0)); return; }
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
        _monthlySummary.SetSummary(_summaryService.Calculate(assignments, _codes));
    }

    private void GenerateWeek()
    {
        var employee = SelectedEmployee(); var week = SelectedWeek(); if (employee is null || week is null) return;
        try
        {
            _database.EnsureMonthUnlocked(_year(), _month());
            var exceptions = new Dictionary<DateOnly, string>();
            for (var day = 0; day < 7; day++) foreach (DataGridViewRow row in _matrix.Rows)
                if (Convert.ToBoolean(row.Cells[day + 1].Value) && row.Tag is AssignmentCodeDefinition definition) exceptions[week.Monday.AddDays(day)] = definition.Code;
            var built = _planning.BuildWeek(week, _defaultShift.SelectedValue?.ToString(), exceptions);
            var expanded = _planning.ExpandEmploymentEndedToMonthEnd(built, _codes);
            _database.SaveWeekAssignments(employee.Id, week.ActiveFrom, week.ActiveTo, expanded, _editing);
            _editing = false; ClearMatrix(); RefreshPerson();
        }
        catch (Exception exception) { MessageBox.Show(exception.Message, "Hafta oluşturulamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void BeginEdit()
    {
        if (SelectedEmployee() is not { } employee || SelectedWeek() is not { } week) return;
        var existing = _database.GetAssignments(week.ActiveFrom, week.ActiveTo).Where(item => item.EmployeeId == employee.Id).ToList();
        if (existing.Count == 0) return;
        _editing = true; ClearMatrix(); SetMatrixEnabledDays(week); LoadExistingSelections(existing);
        _clearButton.Enabled = true; _defaultShift.Enabled = true;
        _generateButton.Text = "✓  Değişiklikleri Kaydet";
    }

    private void LoadExistingSelections(IReadOnlyList<Assignment> existing)
    {
        _loading = true;
        try
        {
            var resolver = new AssignmentCodeResolver(_codes);
            var defaultCode = existing.Where(item => resolver.Resolve(item.Code).IsWorkShift)
                .GroupBy(item => item.Code).OrderByDescending(group => group.Count()).Select(group => group.Key).FirstOrDefault();
            if (defaultCode is not null) _defaultShift.SelectedValue = defaultCode;
            DateOnly? firstEnded = existing.Where(item => resolver.Resolve(item.Code).IsEmploymentEnded).Select(item => (DateOnly?)item.WorkDate).Min();
            foreach (var assignment in existing)
            {
                if (assignment.Code.Equals(defaultCode, StringComparison.OrdinalIgnoreCase)) continue;
                if (resolver.Resolve(assignment.Code).IsEmploymentEnded && assignment.WorkDate != firstEnded) continue;
                var day = assignment.WorkDate.DayNumber - SelectedWeek()!.Monday.DayNumber;
                var row = _matrix.Rows.Cast<DataGridViewRow>().FirstOrDefault(item => item.Tag is AssignmentCodeDefinition definition && definition.Code.Equals(assignment.Code, StringComparison.OrdinalIgnoreCase));
                if (row is not null && day is >= 0 and < 7) row.Cells[day + 1].Value = true;
            }
        }
        finally { _loading = false; }
    }

    private void ClearSelectionsWithConfirmation()
    {
        var hasSelection = _matrix.Rows.Cast<DataGridViewRow>().SelectMany(row => row.Cells.Cast<DataGridViewCell>().Skip(1)).Any(cell => Convert.ToBoolean(cell.Value));
        if (hasSelection && MessageBox.Show("Ekrandaki tüm seçimler temizlenecek. Devam etmek istiyor musunuz?", "Tümünü Temizle",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        ClearMatrix(); if (_defaultShift.Items.Count > 0) _defaultShift.SelectedIndex = 0;
    }

    private void CopyWeek()
    {
        var sourceEmployee = SelectedEmployee(); var sourceWeek = SelectedWeek(); if (sourceEmployee is null || sourceWeek is null) return;
        var targets = _planning.GetCopyTargets(_monthWeeks, sourceWeek);
        if (targets.Count == 0) { MessageBox.Show("Kopyalanabilecek başka bir hafta bulunamadı.", "Haftayı Kopyala", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        using var dialog = new CopyWeekForm(_database.GetEmployees(), targets, sourceEmployee);
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

    private void CopyMonth()
    {
        var sourceEmployee = SelectedEmployee();
        if (sourceEmployee is null) return;
        var sourceAssignments = MonthAssignments(sourceEmployee.Id);
        if (sourceAssignments.Count == 0)
        {
            MessageBox.Show("Kaynak personelin seçili ayda kaydı bulunamadı.", "Ayı Kopyala", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dialog = new CopyMonthForm(_database.GetEmployees(), sourceEmployee, $"{MonthName()} {_year()}");
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.TargetEmployee is not { } targetEmployee) return;
        try
        {
            var existing = MonthAssignments(targetEmployee.Id).Count > 0;
            if (existing && MessageBox.Show("Hedef personelin seçili aya ait kayıtları kaynak personelin kayıtlarıyla değiştirilecek. Devam edilsin mi?",
                "Kopyalama onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            _database.CopyMonthAssignments(sourceEmployee.Id, targetEmployee.Id, _year(), _month());
            RefreshPerson();
        }
        catch (Exception exception) { MessageBox.Show(exception.Message, "Ay kopyalanamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void MatrixValueChanged(object? sender, DataGridViewCellEventArgs args)
    {
        if (_loading || args.RowIndex < 0 || args.ColumnIndex <= 0 || !Convert.ToBoolean(_matrix.Rows[args.RowIndex].Cells[args.ColumnIndex].Value)) return;
        _loading = true; foreach (DataGridViewRow row in _matrix.Rows) if (row.Index != args.RowIndex) row.Cells[args.ColumnIndex].Value = false; _loading = false;
    }

    private void SetMatrixEnabledDays(MonthWeek week)
    {
        var weekend = Color.FromArgb(255, 248, 235); var disabledWeekend = Color.FromArgb(246, 236, 218); var disabledWeekday = Color.FromArgb(239, 241, 244);
        for (var day = 0; day < 7; day++)
        {
            var date = week.Monday.AddDays(day); var enabled = date >= week.ActiveFrom && date <= week.ActiveTo; var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            _matrix.Columns[day + 1].HeaderCell.Style.BackColor = isWeekend ? weekend : Color.FromArgb(249, 250, 252);
            foreach (DataGridViewRow row in _matrix.Rows)
            {
                var cell = row.Cells[day + 1]; cell.ReadOnly = !enabled; cell.Style.BackColor = enabled ? isWeekend ? weekend : Color.White : isWeekend ? disabledWeekend : disabledWeekday;
                if (!enabled) cell.Value = false;
            }
        }
    }
    private void SetActiveMatrixReadOnly(MonthWeek week, bool readOnly)
    {
        for (var day = 0; day < 7; day++)
        {
            var date = week.Monday.AddDays(day); if (date < week.ActiveFrom || date > week.ActiveTo) continue;
            foreach (DataGridViewRow row in _matrix.Rows) row.Cells[day + 1].ReadOnly = readOnly;
        }
    }
    private void ClearMatrix() { _loading = true; foreach (DataGridViewRow row in _matrix.Rows) for (var column = 1; column < row.Cells.Count; column++) row.Cells[column].Value = false; _loading = false; }
    private IReadOnlyList<Assignment> MonthAssignments(long employeeId) { var from = new DateOnly(_year(), _month(), 1); return _database.GetAssignments(from, from.AddMonths(1).AddDays(-1)).Where(item => item.EmployeeId == employeeId).ToList(); }
    private Employee? SelectedEmployee() => _employees.SelectedItem as Employee;
    private MonthWeek? SelectedWeek() => _weeks.SelectedTab?.Tag as MonthWeek;
    private string MonthName() => CultureInfo.GetCultureInfo("tr-TR").DateTimeFormat.GetMonthName(_month());
    private static Color CodeColor(AssignmentCodeDefinition value) { if (value.IsEmploymentEnded) return Color.FromArgb(232, 219, 211); var colors = new[] { Color.FromArgb(220, 237, 250), Color.FromArgb(221, 241, 216), Color.FromArgb(255, 242, 204), Color.FromArgb(251, 222, 222), Color.FromArgb(235, 224, 248), Color.FromArgb(211, 239, 240), Color.FromArgb(220, 241, 211), Color.FromArgb(255, 228, 207) }; return colors[Math.Abs(value.DisplayOrder - 1) % colors.Length]; }
    private static Panel Card() => new() { Dock = DockStyle.Fill, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
    private static Label Label(string text, float size, bool bold) => new() { Text = text, AutoSize = false, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular) };
    private static Button Button(string text, EventHandler handler, Color? back = null, Color? fore = null) { var button = new Button { Text = text, AutoSize = true, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = back ?? Color.White, ForeColor = fore ?? Color.FromArgb(25, 32, 43) }; button.Click += handler; return button; }
}

internal sealed class CopyWeekForm : Form
{
    private readonly CheckedListBox _employees = new() { Dock = DockStyle.Fill, CheckOnClick = true };
    private readonly ComboBox _week = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    public MonthWeek? TargetWeek => (_week.SelectedItem as WeekItem)?.Value;
    public IReadOnlyList<long> EmployeeIds => _employees.CheckedItems.Cast<EmployeeItem>().Select(item => item.Value.Id).ToList();

    public CopyWeekForm(IReadOnlyList<Employee> employees, IReadOnlyList<MonthWeek> targetWeeks, Employee selected)
    {
        Text = "Haftayı Kopyala"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(430, 430);
        var items = targetWeeks.Select(item => new WeekItem(item)).ToList();
        _week.Items.AddRange(items.Cast<object>().ToArray());
        if (_week.Items.Count > 0) _week.SelectedIndex = 0;
        foreach (var employee in employees) { var index = _employees.Items.Add(new EmployeeItem(employee)); if (employee.Id == selected.Id) _employees.SetItemChecked(index, true); }
        var ok = new Button { Text = "Kopyala", Dock = DockStyle.Bottom, Height = 42, DialogResult = DialogResult.OK };
        ok.Enabled = items.Count > 0;
        Controls.Add(_employees); Controls.Add(new Label { Text = "Hedef hafta", Dock = DockStyle.Top, Height = 24 }); Controls.Add(_week); Controls.Add(ok); AcceptButton = ok;
    }
    private sealed record EmployeeItem(Employee Value) { public override string ToString() => Value.FullName; }
    private sealed record WeekItem(MonthWeek Value) { public string Text => $"{Value.Number}. Hafta ({Value.ActiveFrom:dd.MM}–{Value.ActiveTo:dd.MM})"; public static implicit operator MonthWeek(WeekItem value) => value.Value; }
}

internal sealed class CopyMonthForm : Form
{
    private readonly ComboBox _targetEmployee = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    public Employee? TargetEmployee => (_targetEmployee.SelectedItem as EmployeeItem)?.Value;

    public CopyMonthForm(IReadOnlyList<Employee> employees, Employee sourceEmployee, string month)
    {
        Text = "Ayı Kopyala"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(430, 180);
        var targets = employees.Where(item => item.Id != sourceEmployee.Id).Select(item => new EmployeeItem(item)).Cast<object>().ToArray();
        _targetEmployee.Items.AddRange(targets);
        if (_targetEmployee.Items.Count > 0) _targetEmployee.SelectedIndex = 0;
        var ok = new Button { Text = "Kopyala", Dock = DockStyle.Bottom, Height = 42, DialogResult = DialogResult.OK, Enabled = targets.Length > 0 };
        Controls.Add(_targetEmployee);
        Controls.Add(new Label { Text = "Hedef personel", Dock = DockStyle.Top, Height = 24 });
        Controls.Add(new Label { Text = $"Kaynak: {sourceEmployee.FullName}  •  {month}", Dock = DockStyle.Top, Height = 36, Padding = new Padding(0, 8, 0, 0) });
        Controls.Add(ok); AcceptButton = ok;
    }

    private sealed record EmployeeItem(Employee Value) { public override string ToString() => Value.FullName; }
}
