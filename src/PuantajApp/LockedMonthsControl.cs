using System.Globalization;
using Puantaj.Core.Data;

namespace PuantajApp;

internal sealed class LockedMonthsControl : UserControl
{
    private readonly PuantajDatabase _database;
    private readonly ListBox _months = new() { Dock = DockStyle.Fill, Font = new Font(SystemFonts.DefaultFont.FontFamily, 11) };

    public LockedMonthsControl(PuantajDatabase database)
    {
        _database = database;
        var unlock = new Button { Text = "Seçili Ayın Kilidini Kaldır", AutoSize = true };
        unlock.Click += (_, _) => UnlockSelected();
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 65, Padding = new Padding(15) };
        top.Controls.Add(new Label { Text = "Kilitli aylar", Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold), AutoSize = true, Margin = new Padding(3, 7, 20, 3) });
        top.Controls.Add(unlock);
        Controls.Add(_months); Controls.Add(top); Reload();
    }

    public void Reload()
    {
        _months.DataSource = null;
        _months.DataSource = _database.GetLockedMonths().Select(item => new MonthItem(item)).ToList();
    }

    private void UnlockSelected()
    {
        if (_months.SelectedItem is not MonthItem selected) return;
        var result = MessageBox.Show($"{selected} kilidi kaldırılsın mı? Bu aya ait kayıtlar yeniden değiştirilebilir olacaktır.",
            "Kilit kaldırma onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (result != DialogResult.Yes) return;
        _database.UnlockMonth(selected.Value.Year, selected.Value.Month); Reload();
    }

    private sealed record MonthItem(LockedMonth Value)
    {
        public override string ToString() => $"🔒 {CultureInfo.GetCultureInfo("tr-TR").DateTimeFormat.GetMonthName(Value.Month)} {Value.Year}";
    }
}
