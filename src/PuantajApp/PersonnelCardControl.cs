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
    private string _hotel; private string _department;
    private readonly TextBox _search = new() { PlaceholderText = "Ara...", Dock = DockStyle.Top, Height = 34 };
    private readonly ListBox _employees = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 10), DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 34 };
    private readonly Label _employeeName = Label("Personel seçin", 11, true);
    private readonly Label _employeeInfo = Label("", 9, false);
    private readonly FlowLayoutPanel _weekStates = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
    private readonly TabControl _weeks = new() { Dock = DockStyle.Top, Height = 70, Appearance = TabAppearance.FlatButtons, SizeMode = TabSizeMode.Fixed, ItemSize = new Size(155, 54) };
    private readonly ComboBox _defaultShift = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
    private readonly DataGridView _matrix = new() { Dock = DockStyle.Fill, AllowUserToAddRows = false, RowHeadersVisible = false, AutoGenerateColumns = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.FixedSingle, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, AllowUserToResizeColumns = false, ScrollBars = ScrollBars.Vertical };
    private readonly Panel _matrixHost = new() { Dock = DockStyle.Fill };
    private readonly LockedGridOverlay _matrixOverlay = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Button _editButton;
    private readonly Button _clearButton;
    private readonly Button _generateButton;
    private readonly Button _generateMonthButton;
    private readonly Button _copyButton;
    private readonly Button _copyMonthButton;
    private readonly Button _monthLockButton;
    private readonly Label _lockStatus = Label("", 8, true);
    private readonly DataGridView _monthly = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, ColumnHeadersHeight = 42, BackgroundColor = Color.White };
    private readonly MonthlySummaryPanel _monthlySummary = new();
    private IReadOnlyList<AssignmentCodeDefinition> _codes = [];
    private IReadOnlyList<MonthWeek> _monthWeeks = [];
    private bool _loading;
    private bool _editing;
    private bool _dirty;
    private IReadOnlySet<long> _completedEmployeeIds = new HashSet<long>();

    public PersonnelCardControl(PuantajDatabase database, Func<int> year, Func<int> month, string hotel, string department)
    {
        _database = database; _year = year; _month = month; _hotel = hotel; _department = department;
        _editButton = Button("✎  Düzenle", (_, _) => BeginEdit());
        _clearButton = Button("▣  Tümünü Temizle", (_, _) => ClearSelectionsWithConfirmation());
        _generateButton = Button("✓  Haftayı Oluştur", (_, _) => GenerateWeek(), Color.FromArgb(13, 104, 220), Color.White);
        _generateMonthButton = Button("▣  Ayı Oluştur", (_, _) => GenerateMonth(), Color.White, Color.FromArgb(18, 142, 73));
        _copyButton = Button("▣  Haftayı Kopyala", (_, _) => CopyWeek());
        _copyMonthButton = Button("▣  Ayı Kopyala", (_, _) => CopyMonth());
        _monthLockButton = Button("🔒  Ayı Kilitle", (_, _) => ToggleMonthLock());
        Dock = DockStyle.Fill; BackColor = Color.FromArgb(246, 247, 249); Font = new Font("Segoe UI", 9);
        Controls.Add(BuildLayout());
        _search.TextChanged += (_, _) => LoadEmployees();
        _employees.SelectedIndexChanged += (_, _) => { WarnUnsavedChanges(); _editing = false; _dirty = false; RefreshPerson(); };
        _employees.DrawItem += DrawEmployee;
        _weeks.SelectedIndexChanged += (_, _) => { WarnUnsavedChanges(); _editing = false; _dirty = false; RefreshWeek(); };
        _matrix.CurrentCellDirtyStateChanged += (_, _) => { if (_matrix.IsCurrentCellDirty) _matrix.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        _matrix.CellValueChanged += MatrixValueChanged;
        _matrix.CellPainting += PaintCheckBox;
        ReloadAll();
    }

    public void ReloadAll()
    {
        var employeeId = SelectedEmployee()?.Id;
        var selectedWeek = SelectedWeek()?.ActiveFrom;
        _codes = _database.GetAssignmentCodes(); BuildMatrix(); LoadEmployees(); LoadMonth();
        if (employeeId is not null)
            _employees.SelectedItem = (_employees.DataSource as IEnumerable<Employee>)?.FirstOrDefault(item => item.Id == employeeId);
        if (selectedWeek is not null)
            _weeks.SelectedTab = _weeks.TabPages.Cast<TabPage>().FirstOrDefault(page => page.Tag is MonthWeek week && week.ActiveFrom == selectedWeek);
        RefreshPerson();
    }

    public void ReloadSettings()
    {
        var settings = _database.GetSettings();
        _hotel = settings.HotelName; _department = settings.DepartmentName;
        ReloadAll();
    }

    public void SaveCurrentWeek() => GenerateWeek();

    public async Task PrintCurrentWeekAsync()
    {
        var employee = SelectedEmployee(); if (employee is null || SelectedWeek() is not { } week) return;
        var temp = Path.Combine(Path.GetTempPath(), $"puantaj-card-{Guid.NewGuid():N}.xlsx");
        try
        {
            var template = WeeklyExcelExporter.FindWeeklyTemplate(Path.Combine(AppContext.BaseDirectory, "templates"));
            var assignments = _database.GetAssignments(week.Monday, week.Sunday).Where(item => item.EmployeeId == employee.Id).ToList();
            var settings = _database.GetSettings();
            await Task.Run(() => new WeeklyExcelExporter().Export(template, temp, _hotel, _department, week.Monday,
                [employee], assignments, _codes, settings));
            await ExcelInteropService.RunStaAsync(() => new ExcelInteropService().PrintWithDialog(temp));
        }
        catch (Exception exception) { MessageBox.Show(exception.Message, "Yazdırma hatası", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { try { File.Delete(temp); } catch { } }
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(8), BackColor = Color.FromArgb(244, 247, 252) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 155));
        root.Controls.Add(BuildLeft(), 0, 0); root.Controls.Add(BuildWorkspace(), 1, 0);
        var monthly = BuildMonthly(); root.Controls.Add(monthly, 0, 1); root.SetColumnSpan(monthly, 2); return root;
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
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 0, 0, 8) };
        var weekBar = new Panel { Dock = DockStyle.Top, Height = 70 };
        _monthLockButton.Dock = DockStyle.Right; _monthLockButton.Width = 140; _copyButton.Dock = DockStyle.Right; _copyButton.Width = 155; _copyMonthButton.Dock = DockStyle.Right; _copyMonthButton.Width = 135; _weeks.Dock = DockStyle.Fill;
        _lockStatus.Dock = DockStyle.Right; _lockStatus.Width = 105; _lockStatus.TextAlign = ContentAlignment.MiddleCenter;
        weekBar.Controls.Add(_weeks); weekBar.Controls.Add(_lockStatus); weekBar.Controls.Add(_monthLockButton); weekBar.Controls.Add(_copyMonthButton); weekBar.Controls.Add(_copyButton);
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(0, 6, 0, 0) };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 245));
        content.Controls.Add(BuildMiddle(), 0, 0); content.Controls.Add(BuildSummary(), 1, 0);
        panel.Controls.Add(content); panel.Controls.Add(weekBar); return panel;
    }

    private Control BuildMiddle()
    {
        var card = Card(); card.Padding = new Padding(16, 12, 16, 12);
        var leftTitle = Label("▣  HAFTALIK VARDİYA VE İZİN SEÇİMİ", 11, true); leftTitle.Dock = DockStyle.Top; leftTitle.Height = 38;
        var hint = Label("Önce varsayılan çalışma vardiyasını seçin. İstisna günlerini işaretleyip Haftayı Oluştur'a basın.", 9, false);
        hint.Dock = DockStyle.Top; hint.Height = 42; hint.BackColor = Color.FromArgb(237, 246, 255); hint.ForeColor = Color.FromArgb(31, 82, 145); hint.Padding = new Padding(12);
        var shiftBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(0, 9, 0, 3) };
        shiftBar.Controls.Add(Label("Varsayılan vardiya", 9, true)); shiftBar.Controls.Add(_defaultShift);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 10, 4, 4), WrapContents = false };
        _generateButton.Width = 160; _generateMonthButton.Width = 140; _clearButton.Width = 105; _editButton.Width = 105;
        _clearButton.ForeColor = Color.FromArgb(210, 49, 49); _editButton.BackColor = Color.FromArgb(13, 104, 220); _editButton.ForeColor = Color.White;
        actions.Controls.Add(_generateButton); actions.Controls.Add(_clearButton); actions.Controls.Add(_editButton); actions.Controls.Add(_generateMonthButton);
        _matrixHost.Controls.Clear(); _matrixHost.Controls.Add(_matrix); _matrixHost.Controls.Add(_matrixOverlay);
        card.Controls.Add(_matrixHost); card.Controls.Add(actions); card.Controls.Add(shiftBar); card.Controls.Add(hint); card.Controls.Add(leftTitle);
        return card;
    }

    private Control BuildMonthly()
    {
        var card = Card(); var title = Label("AYLIK PUANTAJ ÖNİZLEMESİ", 9, true); title.Dock = DockStyle.Top; title.Height = 28; title.Padding = new Padding(8, 7, 0, 0);
        card.Margin = new Padding(0, 6, 0, 0); card.Controls.Add(_monthly); card.Controls.Add(title); return card;
    }

    private Control BuildSummary()
    {
        var card = Card(); card.Margin = new Padding(10, 0, 0, 0); card.Padding = new Padding(10);
        var title = Label("AYLIK ÖZET", 10, true); title.Dock = DockStyle.Top; title.Height = 38; title.ForeColor = Color.FromArgb(20, 45, 80);
        _monthlySummary.Dock = DockStyle.Fill;
        card.Controls.Add(_monthlySummary); card.Controls.Add(title); return card;
    }

    private void LoadEmployees()
    {
        var selectedId = SelectedEmployee()?.Id; var filter = _search.Text.Trim();
        var values = _database.GetEmployees().Where(item => item.FullName.Contains(filter, StringComparison.CurrentCultureIgnoreCase)).ToList();
        _employees.DataSource = null; _employees.DataSource = values; _employees.DisplayMember = nameof(Employee.FullName);
        if (selectedId is not null) _employees.SelectedItem = values.FirstOrDefault(item => item.Id == selectedId);
        if (_employees.SelectedIndex < 0 && values.Count > 0) _employees.SelectedIndex = 0;
        UpdateCompletionIndicators();
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
        UpdateCompletionIndicators(); RefreshLockState();
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
        _matrix.GridColor = Color.FromArgb(224, 228, 234); _matrix.RowTemplate.Height = 28; _matrix.ColumnHeadersHeight = 34;
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
            _editButton.Enabled = false; _copyButton.Enabled = false; _copyMonthButton.Enabled = false; SetMatrixLocked(false); return;
        }
        for (var offset = 0; offset < 7; offset++)
        {
            var date = week.Monday.AddDays(offset);
            _matrix.Columns[offset + 1].HeaderText = $"{Days[offset]} {date:dd}";
        }
        SetMatrixEnabledDays(week);
        var existing = _database.GetAssignments(week.ActiveFrom, week.ActiveTo).Where(item => item.EmployeeId == employee.Id).ToList();
        ApplyPlanningUiState(existing.Count > 0, _database.IsMonthLocked(_year(), _month()));
        _generateButton.Text = _editing ? "✓  Değişiklikleri Kaydet" : "✓  Haftayı Oluştur";
        if (existing.Count > 0) LoadExistingSelections(existing);
        if (existing.Count > 0 && !_editing) SetActiveMatrixReadOnly(week, true);
        SetMatrixLocked(existing.Count > 0 && !_editing);
        if (_database.IsMonthLocked(_year(), _month())) SetMatrixLocked(true);
    }

    private void ApplyPlanningUiState(bool hasSavedWeek, bool monthLocked)
    {
        var canEdit = hasSavedWeek && !monthLocked;
        _editButton.Enabled = canEdit && !_editing;
        _clearButton.Enabled = !monthLocked && (!hasSavedWeek || _editing);
        _defaultShift.Enabled = !monthLocked && (!hasSavedWeek || _editing);
        _generateButton.Enabled = !monthLocked && (!hasSavedWeek || _editing);
        _generateMonthButton.Enabled = !monthLocked && hasSavedWeek && !_editing;
        var hasCopyTarget = EligibleCopyTargets(SelectedEmployee()?.Id).Count > 0;
        _copyButton.Enabled = _copyMonthButton.Enabled = !monthLocked && hasCopyTarget;
    }

    private void WarnUnsavedChanges()
    {
        if (_editing && _dirty)
            MessageBox.Show("Kaydedilmemiş düzenleme değişiklikleri iptal edildi.", "Puantaj", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            _editing = false; _dirty = false; ClearMatrix(); RefreshPerson();
        }
        catch (Exception exception) { MessageBox.Show(exception.Message, "Hafta oluşturulamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void BeginEdit()
    {
        if (SelectedEmployee() is not { } employee || SelectedWeek() is not { } week) return;
        var existing = _database.GetAssignments(week.ActiveFrom, week.ActiveTo).Where(item => item.EmployeeId == employee.Id).ToList();
        if (existing.Count == 0) return;
        _editing = true; _dirty = false; ClearMatrix(); SetMatrixEnabledDays(week); LoadExistingSelections(existing);
        SetMatrixLocked(false);
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
        var employee = SelectedEmployee(); var week = SelectedWeek();
        if (employee is null || week is null) return;
        var persisted = _database.GetAssignments(week.ActiveFrom, week.ActiveTo).Any(item => item.EmployeeId == employee.Id);
        var hasSelection = _matrix.Rows.Cast<DataGridViewRow>().SelectMany(row => row.Cells.Cast<DataGridViewCell>().Skip(1)).Any(cell => Convert.ToBoolean(cell.Value));
        if ((hasSelection || persisted) && MessageBox.Show("Aktif haftanın kayıtları kalıcı olarak temizlenecek. Devam etmek istiyor musunuz?", "Tümünü Temizle",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        try
        {
            _database.ClearWeekAssignments(employee.Id, week.ActiveFrom, week.ActiveTo);
            _editing = false; _dirty = false; if (_defaultShift.Items.Count > 0) _defaultShift.SelectedIndex = 0; RefreshPerson();
        }
        catch (Exception exception) { MessageBox.Show(exception.Message, "Hafta temizlenemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void GenerateMonth()
    {
        var employee = SelectedEmployee(); var week = SelectedWeek();
        if (employee is null || week is null) return;
        var monthFrom = new DateOnly(_year(), _month(), 1); var monthTo = monthFrom.AddMonths(1).AddDays(-1);
        var existingOutsideSource = _database.GetAssignments(monthFrom, monthTo)
            .Any(item => item.EmployeeId == employee.Id && (item.WorkDate < week.ActiveFrom || item.WorkDate > week.ActiveTo));
        if (existingOutsideSource && MessageBox.Show("Seçili ayın diğer haftalarındaki mevcut kayıtlar, aktif haftanın deseniyle değiştirilecek. Devam edilsin mi?",
            "Ayı Oluştur", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        try
        {
            _database.ApplyWeekPatternToMonth(employee.Id, week.Monday, week.ActiveFrom, week.ActiveTo, _year(), _month());
            RefreshPerson();
        }
        catch (Exception exception) { MessageBox.Show(exception.Message, "Ay oluşturulamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void CopyWeek()
    {
        var sourceEmployee = SelectedEmployee(); var sourceWeek = SelectedWeek(); if (sourceEmployee is null || sourceWeek is null) return;
        var targets = EligibleCopyTargets(sourceEmployee.Id);
        if (targets.Count == 0) { MessageBox.Show("Kopyalanabilecek eksik personel bulunmuyor.", "Haftayı Kopyala", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        using var dialog = new CopyWeekForm(targets, sourceEmployee, sourceWeek, _year(), MonthName());
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.TargetEmployee is not { } targetEmployee) return;
        try
        {
            var existing = _database.GetAssignments(sourceWeek.ActiveFrom, sourceWeek.ActiveTo).Any(item => item.EmployeeId == targetEmployee.Id);
            if (existing && MessageBox.Show("Hedefte mevcut kayıtlar var. Üzerine yazılsın mı?", "Kopyalama onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            _database.CopyWeekAssignments(sourceEmployee.Id, targetEmployee.Id, sourceWeek.ActiveFrom, sourceWeek.ActiveTo);
            _employees.SelectedItem = (_employees.DataSource as IEnumerable<Employee>)?.FirstOrDefault(item => item.Id == targetEmployee.Id);
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
        var targets = EligibleCopyTargets(sourceEmployee.Id);
        if (targets.Count == 0) { MessageBox.Show("Kopyalanabilecek eksik personel bulunmuyor.", "Ayı Kopyala", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        using var dialog = new CopyMonthForm(targets, sourceEmployee, $"{MonthName()} {_year()}");
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
        if (_loading || args.RowIndex < 0 || args.ColumnIndex <= 0) return;
        _dirty = true;
        if (!Convert.ToBoolean(_matrix.Rows[args.RowIndex].Cells[args.ColumnIndex].Value)) return;
        _loading = true; foreach (DataGridViewRow row in _matrix.Rows) if (row.Index != args.RowIndex) row.Cells[args.ColumnIndex].Value = false; _loading = false;
    }

    private void SetMatrixLocked(bool locked)
    {
        _matrixOverlay.Visible = locked;
        if (locked) _matrixOverlay.BringToFront();
    }

    private void PaintCheckBox(object? sender, DataGridViewCellPaintingEventArgs args)
    {
        if (args.RowIndex < 0 || args.ColumnIndex <= 0) return;
        args.PaintBackground(args.CellBounds, true);
        const int size = 18;
        var bounds = new Rectangle(args.CellBounds.X + (args.CellBounds.Width - size) / 2,
            args.CellBounds.Y + (args.CellBounds.Height - size) / 2, size, size);
        var selected = Convert.ToBoolean(args.FormattedValue);
        using var fill = new SolidBrush(selected ? Color.FromArgb(13, 104, 220) : Color.White);
        using var border = new Pen(selected ? Color.FromArgb(13, 104, 220) : Color.FromArgb(154, 165, 180), 1.5f);
        var graphics = args.Graphics ?? throw new InvalidOperationException("Hücre çizim yüzeyi oluşturulamadı.");
        graphics.FillRectangle(fill, bounds); graphics.DrawRectangle(border, bounds);
        if (selected)
        {
            using var tick = new Pen(Color.White, 2f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            graphics.DrawLines(tick, new Point[] { new(bounds.Left + 4, bounds.Top + 9), new(bounds.Left + 8, bounds.Bottom - 4), new(bounds.Right - 3, bounds.Top + 4) });
        }
        args.Handled = true;
    }

    private void UpdateCompletionIndicators()
    {
        _completedEmployeeIds = _database.EvaluateMonthCompletion(_year(), _month()).CompletedEmployeeIds;
        _employees.Invalidate();
    }

    private void DrawEmployee(object? sender, DrawItemEventArgs args)
    {
        if (args.Index < 0 || args.Index >= _employees.Items.Count) return;
        var employee = (Employee)_employees.Items[args.Index]; var selected = (args.State & DrawItemState.Selected) != 0;
        var completed = _completedEmployeeIds.Contains(employee.Id);
        var background = selected ? Color.FromArgb(13, 104, 220) : completed ? Color.FromArgb(224, 247, 231) : Color.White;
        var foreground = selected ? Color.White : completed ? Color.FromArgb(20, 112, 61) : Color.FromArgb(31, 41, 55);
        using var brush = new SolidBrush(background); args.Graphics.FillRectangle(brush, args.Bounds);
        TextRenderer.DrawText(args.Graphics, completed ? $"✓  {employee.FullName}" : employee.FullName, _employees.Font,
            new Rectangle(args.Bounds.X + 10, args.Bounds.Y, args.Bounds.Width - 12, args.Bounds.Height), foreground,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private void RefreshLockState()
    {
        var locked = _database.IsMonthLocked(_year(), _month());
        _monthLockButton.Text = locked ? "🔓  Ay Kilidini Aç" : "🔒  Ayı Kilitle";
        _monthLockButton.ForeColor = locked ? Color.FromArgb(176, 50, 50) : Color.FromArgb(20, 112, 61);
        _lockStatus.Text = locked ? "KİLİTLİ" : "AÇIK"; _lockStatus.ForeColor = locked ? Color.FromArgb(176, 50, 50) : Color.FromArgb(70, 90, 115);
    }

    private void ToggleMonthLock()
    {
        var year = _year(); var month = _month(); var locked = _database.IsMonthLocked(year, month);
        if (locked)
        {
            if (MessageBox.Show($"{MonthName()} {year} ayının kilidi açılacak. Devam edilsin mi?", "Ay Kilidini Aç",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            _database.UnlockMonth(year, month); RefreshPerson(); return;
        }
        var completion = _database.EvaluateMonthCompletion(year, month);
        if (completion.Missing.Count > 0)
        {
            var lines = completion.Missing.Take(30).Select(item =>
            {
                var week = _monthWeeks.FirstOrDefault(value => item.WorkDate >= value.ActiveFrom && item.WorkDate <= value.ActiveTo);
                return $"• {item.EmployeeName} — {item.WorkDate:dd.MM.yyyy}" + (week is null ? "" : $" ({week.Number}. hafta)");
            });
            var more = completion.Missing.Count > 30 ? $"\n… ve {completion.Missing.Count - 30} eksik kayıt daha" : "";
            MessageBox.Show("Ay kilitlenemedi. Eksik kayıtlar:\n\n" + string.Join("\n", lines) + more,
                "Eksik Puantaj", MessageBoxButtons.OK, MessageBoxIcon.Warning); return;
        }
        if (MessageBox.Show($"{MonthName()} {year} ayı değişikliklere kapatılacak. Devam edilsin mi?", "Ayı Kilitle",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        var lockedResult = _database.LockMonthIfComplete(year, month);
        if (lockedResult.Missing.Count > 0) { RefreshPerson(); return; }
        RefreshPerson();
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
    private IReadOnlyList<Employee> EligibleCopyTargets(long? sourceEmployeeId)
    {
        var completed = _database.EvaluateMonthCompletion(_year(), _month()).CompletedEmployeeIds;
        return _database.GetEmployees().Where(item => item.Id != sourceEmployeeId && !completed.Contains(item.Id))
            .OrderBy(item => item.FullName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
    private Employee? SelectedEmployee() => _employees.SelectedItem as Employee;
    private MonthWeek? SelectedWeek() => _weeks.SelectedTab?.Tag as MonthWeek;
    private string MonthName() => CultureInfo.GetCultureInfo("tr-TR").DateTimeFormat.GetMonthName(_month());
    private static Color CodeColor(AssignmentCodeDefinition value) { if (value.IsEmploymentEnded) return Color.FromArgb(232, 219, 211); var colors = new[] { Color.FromArgb(220, 237, 250), Color.FromArgb(221, 241, 216), Color.FromArgb(255, 242, 204), Color.FromArgb(251, 222, 222), Color.FromArgb(235, 224, 248), Color.FromArgb(211, 239, 240), Color.FromArgb(220, 241, 211), Color.FromArgb(255, 228, 207) }; return colors[Math.Abs(value.DisplayOrder - 1) % colors.Length]; }
    private static Panel Card() => new() { Dock = DockStyle.Fill, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
    private static Label Label(string text, float size, bool bold) => new() { Text = text, AutoSize = false, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular) };
    private static Button Button(string text, EventHandler handler, Color? back = null, Color? fore = null) { var button = new Button { Text = text, AutoSize = true, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = back ?? Color.White, ForeColor = fore ?? Color.FromArgb(25, 32, 43) }; button.Click += handler; return button; }
}

internal sealed class LockedGridOverlay : Control
{
    public LockedGridOverlay()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        BackColor = Color.Transparent; Cursor = Cursors.No;
    }

    protected override void OnPaint(PaintEventArgs args)
    {
        using var shade = new SolidBrush(Color.FromArgb(72, 70, 78, 90));
        args.Graphics.FillRectangle(shade, ClientRectangle);
        const string message = "✓  Hafta oluşturuldu  •  Değişiklik için Düzenle'yi kullanın";
        var size = TextRenderer.MeasureText(message, Font);
        var bounds = new Rectangle((Width - size.Width - 30) / 2, 12, size.Width + 30, 34);
        using var background = new SolidBrush(Color.FromArgb(235, 31, 41, 55));
        args.Graphics.FillRectangle(background, bounds);
        TextRenderer.DrawText(args.Graphics, message, Font, bounds, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}

internal sealed class CopyWeekForm : Form
{
    private readonly ComboBox _targetEmployee = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    public Employee? TargetEmployee => (_targetEmployee.SelectedItem as EmployeeItem)?.Value;

    public CopyWeekForm(IReadOnlyList<Employee> employees, Employee sourceEmployee, MonthWeek week, int year, string month)
    {
        Text = "Haftayı Kopyala"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(430, 190);
        var targets = employees.Where(item => item.Id != sourceEmployee.Id).OrderBy(item => item.FullName, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => new EmployeeItem(item)).Cast<object>().ToArray();
        _targetEmployee.Items.AddRange(targets); if (_targetEmployee.Items.Count > 0) _targetEmployee.SelectedIndex = 0;
        var context = $"{year} {month} • {week.Number}. Hafta • {week.ActiveFrom:dd.MM}–{week.ActiveTo:dd.MM}\nKaynak: {sourceEmployee.FullName}";
        var ok = new Button { Text = "Kopyala", Dock = DockStyle.Bottom, Height = 42, DialogResult = DialogResult.OK, Enabled = targets.Length > 0 };
        Controls.Add(_targetEmployee); Controls.Add(new Label { Text = "Hedef personel", Dock = DockStyle.Top, Height = 24 });
        Controls.Add(new Label { Text = context, Dock = DockStyle.Top, Height = 52, Padding = new Padding(0, 6, 0, 0) });
        Controls.Add(ok); AcceptButton = ok;
    }
    private sealed record EmployeeItem(Employee Value) { public override string ToString() => Value.FullName; }
}

internal sealed class CopyMonthForm : Form
{
    private readonly ComboBox _targetEmployee = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    public Employee? TargetEmployee => (_targetEmployee.SelectedItem as EmployeeItem)?.Value;

    public CopyMonthForm(IReadOnlyList<Employee> employees, Employee sourceEmployee, string month)
    {
        Text = "Ayı Kopyala"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(430, 180);
        var targets = employees.Where(item => item.Id != sourceEmployee.Id)
            .OrderBy(item => item.FullName, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => new EmployeeItem(item)).Cast<object>().ToArray();
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
