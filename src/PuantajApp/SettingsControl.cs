using Puantaj.Core.Backup;
using Puantaj.Core.Data;

namespace PuantajApp;

internal sealed class SettingsControl : UserControl
{
    private readonly PuantajDatabase _database;
    private readonly Dictionary<string, TextBox> _texts = [];
    private readonly NumericUpDown _logoSize = Number(0.5m, 20m);
    private readonly NumericUpDown _left = Number(0, 10), _right = Number(0, 10), _top = Number(0, 10), _bottom = Number(0, 10);
    private readonly CheckBox _printLogo = new() { Text = "Çıktılarda logoyu kullan", AutoSize = true };
    private readonly CheckBox _center = new() { Text = "Sayfayı yatay ortala", AutoSize = true };
    private readonly PictureBox _logoPreview = new() { Width = 180, Height = 80, SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle };

    public SettingsControl(PuantajDatabase database)
    {
        _database = database;
        var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(18), ColumnCount = 3 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        AddSection(table, "OTEL"); AddText(table, "Otel Adı", "HotelName"); AddText(table, "Departman", "DepartmentName");
        AddText(table, "Logo", "LogoPath", LogoActions()); AddControl(table, "Logo önizleme", _logoPreview);
        AddSection(table, "İMZA ALANLARI"); AddText(table, "Departman Müdürü", "DepartmentManager");
        AddText(table, "Ünvanı", "DepartmentManagerTitle"); AddText(table, "İnsan Kaynakları", "HumanResourcesManager");
        AddText(table, "Ünvanı", "HumanResourcesTitle"); AddText(table, "Genel Müdür", "GeneralManager"); AddText(table, "Ünvanı", "GeneralManagerTitle");
        AddSection(table, "YAZDIRMA"); AddControl(table, "Logo boyutu (cm)", _logoSize); AddControl(table, "Sol kenar (cm)", _left);
        AddControl(table, "Sağ kenar (cm)", _right); AddControl(table, "Üst kenar (cm)", _top); AddControl(table, "Alt kenar (cm)", _bottom);
        AddControl(table, "Seçenekler", new FlowLayoutPanel { AutoSize = true, Controls = { _printLogo, _center } });
        AddSection(table, "VARDİYA VE KOD TANIMLARI");
        var codeSettings = new ShiftSettingsControl(database) { Width = 700, Height = 260 };
        table.Controls.Add(codeSettings, 0, table.RowCount); table.SetColumnSpan(codeSettings, 3); table.RowCount++;
        var save = new Button { Text = "Ayarları Kaydet", AutoSize = true }; save.Click += (_, _) => Save();
        var backup = new Button { Text = "Yedek Al", AutoSize = true }; backup.Click += (_, _) => Backup();
        var restore = new Button { Text = "Geri Yükle", AutoSize = true }; restore.Click += (_, _) => Restore();
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 55, Padding = new Padding(18, 8, 0, 0) };
        actions.Controls.AddRange([save, backup, restore]);
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true }; scroll.Controls.Add(table);
        Controls.Add(scroll); Controls.Add(actions);
        LoadSettings();
    }

    private void LoadSettings()
    {
        var value = _database.GetSettings();
        Set("HotelName", value.HotelName); Set("DepartmentName", value.DepartmentName); Set("LogoPath", value.LogoPath);
        Set("DepartmentManager", value.DepartmentManager); Set("DepartmentManagerTitle", value.DepartmentManagerTitle);
        Set("HumanResourcesManager", value.HumanResourcesManager); Set("HumanResourcesTitle", value.HumanResourcesTitle);
        Set("GeneralManager", value.GeneralManager); Set("GeneralManagerTitle", value.GeneralManagerTitle);
        _logoSize.Value = value.LogoSizeCm; _left.Value = value.MarginLeftCm; _right.Value = value.MarginRightCm;
        _top.Value = value.MarginTopCm; _bottom.Value = value.MarginBottomCm; _printLogo.Checked = value.PrintLogo; _center.Checked = value.CenterHorizontally;
        UpdateLogoPreview();
    }

    private void Save()
    {
        try
        {
            _database.SaveSettings(new AppSettings(Get("HotelName"), Get("DepartmentName"), Get("LogoPath"), Get("DepartmentManager"),
                Get("DepartmentManagerTitle"), Get("HumanResourcesManager"), Get("HumanResourcesTitle"), Get("GeneralManager"),
                Get("GeneralManagerTitle"), _logoSize.Value, _left.Value, _right.Value, _top.Value, _bottom.Value, _printLogo.Checked, _center.Checked));
            MessageBox.Show("Ayarlar kaydedildi. Otel ve departman başlıkları bir sonraki açılışta yenilenir.", "Puantaj", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception exception) { ShowError(exception, "Ayarlar kaydedilemedi"); }
    }

    private void Backup()
    {
        using var dialog = new SaveFileDialog { Filter = "Puantaj yedeği (*.zip)|*.zip", FileName = $"Puantaj_Yedek_{DateTime.Now:yyyyMMdd_HHmm}.zip" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { new PuantajBackupService().Create(_database.DatabasePath, dialog.FileName, Get("LogoPath")); MessageBox.Show("Yedek başarıyla oluşturuldu.", "Puantaj"); }
        catch (Exception exception) { ShowError(exception, "Yedek alınamadı"); }
    }

    private void Restore()
    {
        using var dialog = new OpenFileDialog { Filter = "Puantaj yedeği (*.zip)|*.zip" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (MessageBox.Show("Mevcut kullanıcı verileri yedekteki verilerle değiştirilecek. Devam edilsin mi?", "Geri yükleme onayı",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        try
        {
            var safetyPath = Path.Combine(Path.GetDirectoryName(_database.DatabasePath)!, $"restore-oncesi-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
            new PuantajBackupService().Create(_database.DatabasePath, safetyPath, Get("LogoPath"));
            new PuantajBackupService().Restore(dialog.FileName, _database.DatabasePath);
            MessageBox.Show("Yedek geri yüklendi. Eski verilerle çalışmayı önlemek için Puantaj şimdi kapatılacak. Lütfen programı yeniden açın.",
                "Puantaj", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Application.Exit();
            return;
        }
        catch (Exception exception) { ShowError(exception, "Yedek geri yüklenemedi"); }
    }

    private Control LogoActions()
    {
        var panel = new FlowLayoutPanel { AutoSize = true };
        var browse = new Button { Text = "Logo Yükle…", AutoSize = true };
        browse.Click += (_, _) => { using var dialog = new OpenFileDialog { Filter = "Görsel (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp" }; if (dialog.ShowDialog(this) == DialogResult.OK) { Set("LogoPath", dialog.FileName); UpdateLogoPreview(); } };
        var remove = new Button { Text = "Logo Kaldır", AutoSize = true }; remove.Click += (_, _) => { Set("LogoPath", ""); UpdateLogoPreview(); };
        panel.Controls.Add(browse); panel.Controls.Add(remove); return panel;
    }

    private void UpdateLogoPreview()
    {
        _logoPreview.Image?.Dispose(); _logoPreview.Image = null; var path = Get("LogoPath"); if (!File.Exists(path)) return;
        using var stream = File.OpenRead(path); using var image = Image.FromStream(stream); _logoPreview.Image = new Bitmap(image);
    }

    private static NumericUpDown Number(decimal minimum, decimal maximum) => new() { Minimum = minimum, Maximum = maximum, DecimalPlaces = 1, Increment = 0.1m, Width = 100 };
    private static void AddSection(TableLayoutPanel table, string text) { var label = new Label { Text = text, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 16, 0, 6) }; table.Controls.Add(label, 0, table.RowCount); table.SetColumnSpan(label, 3); table.RowCount++; }
    private void AddText(TableLayoutPanel table, string label, string key, Control? extra = null) { var text = new TextBox { Width = 350 }; _texts[key] = text; AddControl(table, label, text, extra); }
    private static void AddControl(TableLayoutPanel table, string label, Control control, Control? extra = null) { var row = table.RowCount++; table.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 7, 0, 0) }, 0, row); table.Controls.Add(control, 1, row); if (extra is not null) table.Controls.Add(extra, 2, row); }
    private string Get(string key) => _texts[key].Text.Trim(); private void Set(string key, string value) => _texts[key].Text = value;
    private static void ShowError(Exception exception, string title) => MessageBox.Show(exception.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
}
