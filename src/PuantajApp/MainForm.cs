using System.Globalization;
using Puantaj.Core.Data;
using Puantaj.Core.Licensing;

namespace PuantajApp;

public sealed class MainForm : Form
{
    private readonly NumericUpDown _year = new() { Minimum = 2000, Maximum = 2100, Width = 70 };
    private readonly ComboBox _month = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 105 };
    private readonly PersonnelCardControl _card;
    private readonly Label _clock = new() { ForeColor = Color.White, AutoSize = false, Width = 205, Height = 32, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
    private readonly System.Windows.Forms.Timer _clockTimer = new() { Interval = 1000 };

    public MainForm(LicenseData license, PuantajDatabase database)
    {
        var settings = database.EnsureSettings(license.HotelName, license.DepartmentName);
        Text = "Haftalık Vardiya Planı – Personel Kartı"; StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized; MinimumSize = new Size(1180, 720); BackColor = Color.FromArgb(245, 246, 248);
        _year.Value = DateTime.Today.Year; _month.Items.AddRange(CultureInfo.GetCultureInfo("tr-TR").DateTimeFormat.MonthNames.Take(12).Cast<object>().ToArray()); _month.SelectedIndex = DateTime.Today.Month - 1;
        _card = new PersonnelCardControl(database, () => (int)_year.Value, () => _month.SelectedIndex + 1, settings.HotelName, settings.DepartmentName);
        var header = BuildHeader(database);
        Controls.Add(_card); Controls.Add(header);
        _year.ValueChanged += (_, _) => _card.ReloadAll(); _month.SelectedIndexChanged += (_, _) => _card.ReloadAll();
        _clockTimer.Tick += (_, _) => UpdateClock(); UpdateClock(); _clockTimer.Start();
        FormClosed += (_, _) => _clockTimer.Dispose();
    }

    private Control BuildHeader(PuantajDatabase database)
    {
        var bar = new TableLayoutPanel { Dock = DockStyle.Top, Height = 56, BackColor = Color.FromArgb(19, 42, 74), Padding = new Padding(14, 8, 10, 7), ColumnCount = 3 };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185)); bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 410));
        var title = new Label { Text = "▣  PUANTAJ", ForeColor = Color.White, Font = new Font("Segoe UI", 13, FontStyle.Bold), AutoSize = false, Width = 185, Height = 32, TextAlign = ContentAlignment.MiddleLeft };
        var menu = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true, BackColor = Color.Transparent };
        menu.Controls.Add(HeaderLabel("Yıl")); menu.Controls.Add(_year); menu.Controls.Add(HeaderLabel("Ay")); menu.Controls.Add(_month);
        menu.Controls.Add(HeaderButton("⚙ Ayarlar", (_, _) => ShowDialog(new SettingsControl(database), "Ayarlar", new Size(850, 760))));
        menu.Controls.Add(HeaderButton("Personeller", (_, _) => { var control = new EmployeesControl(database); control.EmployeesChanged += (_, _) => _card.ReloadAll(); ShowDialog(control, "Personeller", new Size(720, 600)); }));
        menu.Controls.Add(HeaderButton("Kilitli Aylar", (_, _) => ShowDialog(new LockedMonthsControl(database), "Kilitli Ayları Yönet", new Size(560, 500))));
        menu.Controls.Add(HeaderButton("▣ Kaydet", (_, _) => _card.SaveCurrentWeek()));
        Button printButton = null!;
        printButton = HeaderButton("▤ Yazdır", async (_, _) => await RunExclusive(printButton, _card.PrintCurrentWeekAsync));
        menu.Controls.Add(printButton); menu.Controls.Add(HeaderButton("📅 Puantaj", (_, _) => OpenPuantaj(database)));
        var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, BackColor = Color.Transparent };
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82)); right.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76)); right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        right.Controls.Add(HeaderButton("↻ Yenile", (_, _) => _card.ReloadSettings()), 0, 0);
        right.Controls.Add(HeaderButton("× Kapat", (_, _) => Close()), 1, 0); right.Controls.Add(_clock, 2, 0); _clock.Dock = DockStyle.Fill;
        bar.Controls.Add(title, 0, 0); bar.Controls.Add(menu, 1, 0); bar.Controls.Add(right, 2, 0);
        return bar;
    }

    private void OpenMonthlyExport(PuantajDatabase database)
    {
        var settings = database.GetSettings();
        ShowDialog(new MonthlyExportControl(database, () => (int)_year.Value, () => _month.SelectedIndex + 1,
            settings.HotelName, settings.DepartmentName), "Aylık Puantaj", new Size(560, 220));
    }

    private void OpenPuantaj(PuantajDatabase database)
    {
        using var chooser = new Form { Text = "Puantaj", StartPosition = FormStartPosition.CenterParent, Size = new Size(700, 390), MinimizeBox = false, MaximizeBox = false };
        var monthly = ReportChoice("Aylık Puantaj", "Mevcut aylık puantaj raporunu oluşturur.");
        var weekly = ReportChoice("Haftalık Çalışma Planı", "Housman, Laundry ve Kat İstekleri görevleri bu rapora dahil edilmez. Bu personeller için Vardiyalı Çalışma Planını kullanın.");
        var shifted = ReportChoice("Vardiyalı Çalışma Planı", "Görev tanımında Housman, Laundry veya Kat İstekleri bulunan personeller bu raporda gösterilir.");
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(18) };
        weekly.Click += (_, _) => { chooser.Hide(); ShowDialog(new WeeklyAttendanceExportControl(database, (int)_year.Value, _month.SelectedIndex + 1), "Haftalık Çalışma Planı", new Size(570, 330)); chooser.Close(); };
        shifted.Click += (_, _) => { chooser.Hide(); ShowDialog(new WeeklyAttendanceExportControl(database, (int)_year.Value, _month.SelectedIndex + 1, true), "Vardiyalı Çalışma Planı", new Size(570, 330)); chooser.Close(); };
        monthly.Click += (_, _) => { chooser.Hide(); OpenMonthlyExport(database); chooser.Close(); };
        panel.Controls.Add(monthly); panel.Controls.Add(weekly); panel.Controls.Add(shifted); chooser.Controls.Add(panel); chooser.ShowDialog(this);
    }

    private static Button ReportChoice(string title, string description) => new()
    {
        Text = $"{title}\r\n{description}", Width = 635, Height = 92, TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(12, 4, 12, 4), Margin = new Padding(4), AutoEllipsis = true
    };

    private void UpdateClock()
    {
        var now = DateTime.Now;
        _clock.Text = $"{now:dd MMMM yyyy, dddd}   ◷  {now:HH:mm:ss}";
    }

    private static async Task RunExclusive(Control control, Func<Task> action)
    {
        control.Enabled = false;
        try { await action(); }
        finally { control.Enabled = true; }
    }

    private void ShowDialog(Control content, string title, Size size)
    {
        using var form = new Form { Text = title, StartPosition = FormStartPosition.CenterParent, Size = size, MinimizeBox = false };
        content.Dock = DockStyle.Fill; form.Controls.Add(content); form.ShowDialog(this); _card.ReloadAll();
    }

    private static Label HeaderLabel(string text) => new() { Text = text, ForeColor = Color.White, AutoSize = true, Margin = new Padding(8, 7, 2, 0) };
    private static Button HeaderButton(string text, EventHandler click)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 34, FlatStyle = FlatStyle.Flat, ForeColor = Color.White,
            BackColor = Color.FromArgb(35, 65, 102), Margin = new Padding(6, 1, 0, 0), Cursor = Cursors.Hand };
        button.FlatAppearance.BorderColor = Color.FromArgb(82, 112, 148); button.FlatAppearance.MouseOverBackColor = Color.FromArgb(48, 87, 132);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(25, 51, 84); button.Click += click; return button;
    }
}
