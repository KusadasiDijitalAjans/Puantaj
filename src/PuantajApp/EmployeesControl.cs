using Puantaj.Core.Data;

namespace PuantajApp;

internal sealed class EmployeesControl : UserControl
{
    private readonly PuantajDatabase _database;
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, ReadOnly = true, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoGenerateColumns = false };
    private readonly CheckBox _showInactive = new() { Text = "Aktif listeden kaldırılanları göster", AutoSize = true, Margin = new Padding(12, 8, 3, 3) };
    private readonly Button _activeToggle;

    public event EventHandler? EmployeesChanged;

    public EmployeesControl(PuantajDatabase database)
    {
        _database = database;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Order", HeaderText = "Sıra", Width = 60 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Personel Adı", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Position", HeaderText = "Görevi", Width = 150 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Active", HeaderText = "Durum", Width = 70 });

        var add = Button("Personel Ekle", (_, _) => AddEmployee());
        var edit = Button("Düzenle", (_, _) => EditEmployee());
        _activeToggle = Button("Aktif Listeden Kaldır", (_, _) => ToggleEmployeeActive());
        var up = Button("Yukarı", (_, _) => MoveSelectedEmployee(-1));
        var down = Button("Aşağı", (_, _) => MoveSelectedEmployee(1));
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42 };
        buttons.Controls.AddRange([add, edit, _activeToggle, up, down, _showInactive]);
        _showInactive.CheckedChanged += (_, _) => Reload();
        _grid.SelectionChanged += (_, _) => UpdateToggleText();
        Controls.Add(_grid);
        Controls.Add(buttons);
        Reload();
    }

    public void Reload()
    {
        _grid.Rows.Clear();
        foreach (var employee in _database.GetEmployees(!_showInactive.Checked))
            _grid.Rows.Add(employee.Id, employee.DisplayOrder, employee.FullName, employee.Position, employee.IsActive ? "Aktif" : "Pasif");
        UpdateToggleText();
    }

    private static Button Button(string text, EventHandler handler)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += handler;
        return button;
    }

    private long? SelectedId() => _grid.CurrentRow?.Cells["Id"].Value is long id ? id : null;

    private void AddEmployee()
    {
        using var dialog = new EmployeeEditDialog("Personel Ekle", string.Empty, string.Empty, null);
        if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.EmployeeName)) return;
        var id = _database.AddEmployee(dialog.EmployeeName);
        _database.UpdateEmployeeDetails(id, dialog.Position, string.Empty, dialog.HireDate);
        Changed();
    }

    private void EditEmployee()
    {
        var id = SelectedId();
        if (id is null) return;
        var employee = _database.GetEmployees(false).First(item => item.Id == id.Value);
        using var dialog = new EmployeeEditDialog("Personel Düzenle", employee.FullName, employee.Position, employee.HireDate);
        if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.EmployeeName)) return;
        _database.UpdateEmployee(id.Value, dialog.EmployeeName);
        _database.UpdateEmployeeDetails(id.Value, dialog.Position, employee.WorkPattern, dialog.HireDate);
        Changed();
    }

    private void ToggleEmployeeActive()
    {
        var id = SelectedId();
        if (id is null) return;
        var employee = _database.GetEmployees(false).First(item => item.Id == id.Value);
        var activate = !employee.IsActive;
        var action = activate ? "Aktif Listeye Ekle" : "Aktif Listeden Kaldır";
        if (MessageBox.Show($"{employee.FullName} için '{action}' işlemi yapılsın mı?\n\nGeçmiş puantaj kayıtları silinmeyecektir.",
            action, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        _database.SetEmployeeActive(id.Value, activate);
        Changed();
    }

    private void UpdateToggleText()
    {
        var id = SelectedId(); if (id is null) { _activeToggle.Enabled = false; return; }
        _activeToggle.Enabled = true;
        _activeToggle.Text = _database.GetEmployees(false).First(item => item.Id == id.Value).IsActive
            ? "Aktif Listeden Kaldır" : "Aktif Listeye Ekle";
    }

    private void MoveSelectedEmployee(int direction)
    {
        var id = SelectedId();
        if (id is null) return;
        _database.MoveEmployee(id.Value, direction);
        Changed();
    }

    private void Changed()
    {
        Reload();
        EmployeesChanged?.Invoke(this, EventArgs.Empty);
    }
}

internal sealed class EmployeeEditDialog : Form
{
    private readonly TextBox _name = new() { Left = 150, Top = 15, Width = 250 };
    private readonly TextBox _position = new() { Left = 150, Top = 50, Width = 250 };
    private readonly DateTimePicker _hireDate = new() { Left = 150, Top = 85, Width = 250, Format = DateTimePickerFormat.Short, ShowCheckBox = true };

    public string EmployeeName => _name.Text.Trim();
    public string Position => _position.Text.Trim();
    public DateOnly? HireDate => _hireDate.Checked ? DateOnly.FromDateTime(_hireDate.Value) : null;

    public EmployeeEditDialog(string title, string name, string position, DateOnly? hireDate)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(420, 175);

        _name.Text = name;
        _position.Text = position;
        _hireDate.Checked = hireDate is not null;
        if (hireDate is { } value) _hireDate.Value = value.ToDateTime(TimeOnly.MinValue);
        var ok = new Button { Text = "Kaydet", DialogResult = DialogResult.OK, Left = 245, Top = 125, Width = 75 };
        var cancel = new Button { Text = "İptal", DialogResult = DialogResult.Cancel, Left = 326, Top = 125, Width = 75 };

        Controls.Add(new Label { Text = "Ad Soyad:", Left = 16, Top = 18, AutoSize = true });
        Controls.Add(_name);
        Controls.Add(new Label { Text = "Görevi:", Left = 16, Top = 53, AutoSize = true });
        Controls.Add(_position);
        Controls.Add(new Label { Text = "İşe Giriş:", Left = 16, Top = 88, AutoSize = true });
        Controls.Add(_hireDate);
        Controls.Add(ok);
        Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;
    }
}
