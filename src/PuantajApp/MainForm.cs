using System.Globalization;
using Puantaj.Core.Data;
using Puantaj.Core.Licensing;

namespace PuantajApp;

public sealed class MainForm : Form
{
    private readonly NumericUpDown _year = new() { Minimum = 2000, Maximum = 2100, Width = 70 };
    private readonly ComboBox _month = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 105 };
    private readonly PersonnelCardControl _card;

    public MainForm(LicenseData license, PuantajDatabase database)
    {
        var settings = database.EnsureSettings(license.HotelName, license.DepartmentName);
        Text = "Haftalık Vardiya Planı – Personel Kartı"; StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized; MinimumSize = new Size(1180, 720); BackColor = Color.FromArgb(245, 246, 248);
        _year.Value = DateTime.Today.Year; _month.Items.AddRange(CultureInfo.GetCultureInfo("tr-TR").DateTimeFormat.MonthNames.Take(12).Cast<object>().ToArray()); _month.SelectedIndex = DateTime.Today.Month - 1;
        _card = new PersonnelCardControl(database, () => (int)_year.Value, () => _month.SelectedIndex + 1, settings.HotelName, settings.DepartmentName);
        var header = BuildHeader(database, settings);
        Controls.Add(_card); Controls.Add(header);
        _year.ValueChanged += (_, _) => _card.ReloadAll(); _month.SelectedIndexChanged += (_, _) => _card.ReloadAll();
    }

    private Control BuildHeader(PuantajDatabase database, AppSettings settings)
    {
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, BackColor = Color.FromArgb(18, 28, 40), Padding = new Padding(12, 5, 8, 4), WrapContents = false };
        var title = new Label { Text = "♙  HAFTALIK VARDİYA PLANI – PERSONEL KARTI", ForeColor = Color.White, Font = new Font("Segoe UI", 11, FontStyle.Bold), AutoSize = false, Width = 480, Height = 32, TextAlign = ContentAlignment.MiddleLeft };
        bar.Controls.Add(title); bar.Controls.Add(new Label { Width = 15 }); bar.Controls.Add(HeaderLabel("Yıl")); bar.Controls.Add(_year); bar.Controls.Add(HeaderLabel("Ay")); bar.Controls.Add(_month);
        bar.Controls.Add(HeaderButton("⚙ Ayarlar", (_, _) => ShowDialog(new SettingsControl(database), "Ayarlar", new Size(850, 760))));
        bar.Controls.Add(HeaderButton("Personeller", (_, _) => { var control = new EmployeesControl(database); control.EmployeesChanged += (_, _) => _card.ReloadAll(); ShowDialog(control, "Personeller", new Size(720, 600)); }));
        bar.Controls.Add(HeaderButton("Kilitli Aylar", (_, _) => ShowDialog(new LockedMonthsControl(database), "Kilitli Ayları Yönet", new Size(560, 500))));
        bar.Controls.Add(HeaderButton("▣ Kaydet", (_, _) => _card.SaveCurrentWeek()));
        Button printButton = null!;
        printButton = HeaderButton("▤ Yazdır", (_, _) => RunExclusive(printButton, () => _card.PrintCurrentWeek()));
        bar.Controls.Add(printButton);
        bar.Controls.Add(HeaderButton("📅 Aylık Puantaj", (_, _) =>
            ShowDialog(new MonthlyExportControl(database, () => (int)_year.Value, () => _month.SelectedIndex + 1, settings.HotelName, settings.DepartmentName),
                "Aylık Puantaj", new Size(560, 220))));
        bar.Controls.Add(HeaderButton("× Kapat", (_, _) => Close()));
        return bar;
    }

    private static void RunExclusive(Control control, Action action)
    {
        control.Enabled = false;
        try { action(); }
        finally { control.Enabled = true; }
    }

    private void ShowDialog(Control content, string title, Size size)
    {
        using var form = new Form { Text = title, StartPosition = FormStartPosition.CenterParent, Size = size, MinimizeBox = false };
        content.Dock = DockStyle.Fill; form.Controls.Add(content); form.ShowDialog(this); _card.ReloadAll();
    }

    private static Label HeaderLabel(string text) => new() { Text = text, ForeColor = Color.White, AutoSize = true, Margin = new Padding(8, 7, 2, 0) };
    private static Button HeaderButton(string text, EventHandler click) { var button = new Button { Text = text, AutoSize = true, Height = 32, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(18, 28, 40), Margin = new Padding(8, 0, 0, 0) }; button.FlatAppearance.BorderSize = 0; button.Click += click; return button; }
}
