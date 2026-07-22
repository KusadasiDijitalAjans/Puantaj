using Puantaj.Core.Licensing;

namespace PuantajLicenseGenerator;

public sealed class GeneratorForm : Form
{
    private readonly TextBox _customer = new();
    private readonly TextBox _hotel = new();
    private readonly TextBox _department = new();
    private readonly TextBox _device = new();
    private readonly DateTimePicker _issuedAt = new() { Format = DateTimePickerFormat.Short };
    private readonly ComboBox _licenseType = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _privateKeyPath = new();
    private readonly TextBox _output = new() { Multiline = true, ScrollBars = ScrollBars.Both, ReadOnly = true };
    private readonly LicenseSerializer _serializer = new();
    private readonly LicenseGenerationService _generationService;

    public GeneratorForm()
    {
        _generationService = new LicenseGenerationService(_serializer);
        Text = "Puantaj Lisans Üretici";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(760, 650);
        _issuedAt.Value = DateTime.Today;
        _licenseType.Items.AddRange(["1 Yıllık", "Sınırsız"]); _licenseType.SelectedIndex = 0;

        var browse = new Button { Text = "Private key seç...", AutoSize = true };
        browse.Click += BrowsePrivateKey;
        var create = new Button { Text = "Lisans oluştur", AutoSize = true };
        create.Click += CreateLicense;
        var copy = new Button { Text = "Lisans kodunu kopyala", AutoSize = true };
        copy.Click += (_, _) => CopyLicense();
        var save = new Button { Text = "Lisans dosyası kaydet", AutoSize = true };
        save.Click += (_, _) => SaveLicense();

        var keyPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        _privateKeyPath.Width = 440;
        keyPanel.Controls.Add(_privateKeyPath);
        keyPanel.Controls.Add(browse);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        actions.Controls.Add(create);
        actions.Controls.Add(copy);
        actions.Controls.Add(save);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 2,
            RowCount = 10
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(layout, 0, "Müşteri adı", _customer);
        AddRow(layout, 1, "Otel adı", _hotel);
        AddRow(layout, 2, "Departman adı", _department);
        AddRow(layout, 3, "Cihaz kodu", _device);
        AddRow(layout, 4, "Başlangıç tarihi", _issuedAt);
        AddRow(layout, 5, "Lisans türü", _licenseType);
        AddRow(layout, 7, "Private key", keyPanel);
        AddRow(layout, 8, string.Empty, actions);
        _output.Dock = DockStyle.Fill;
        layout.Controls.Add(new Label { Text = "Lisans kodu", AutoSize = true }, 0, 9);
        layout.Controls.Add(_output, 1, 9);
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(layout);
    }

    private static void AddRow(TableLayoutPanel layout, int row, string label, Control control)
    {
        control.Dock = DockStyle.Fill;
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private void BrowsePrivateKey(object? sender, EventArgs eventArgs)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "PEM private key (*.pem)|*.pem|Tüm dosyalar (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _privateKeyPath.Text = dialog.FileName;
        }
    }

    private void CreateLicense(object? sender, EventArgs eventArgs)
    {
        try
        {
            var issuedAt = new DateTimeOffset(_issuedAt.Value.Date, TimeZoneInfo.Local.GetUtcOffset(_issuedAt.Value.Date));
            _output.Text = _generationService.GenerateCode(new LicenseGenerationRequest(
                _customer.Text, _hotel.Text, _department.Text, _device.Text, issuedAt,
                _licenseType.SelectedIndex == 1 ? GeneratedLicenseType.Lifetime : GeneratedLicenseType.OneYear,
                _privateKeyPath.Text));
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Lisans oluşturulamadı", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CopyLicense()
    {
        if (string.IsNullOrWhiteSpace(_output.Text))
        {
            MessageBox.Show("Önce lisans oluşturun.", "Puantaj", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Clipboard.SetText(_output.Text);
    }

    private void SaveLicense()
    {
        if (string.IsNullOrWhiteSpace(_output.Text))
        {
            MessageBox.Show("Önce lisans oluşturun.", "Puantaj", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "Puantaj lisansı (*.dat)|*.dat|Tüm dosyalar (*.*)|*.*",
            FileName = LicenseConstants.LicenseFileName
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            File.WriteAllText(dialog.FileName, _output.Text);
        }
    }
}
