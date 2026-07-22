using Puantaj.Core.Licensing;
using Puantaj.Core.Storage;

namespace PuantajApp;

public sealed class ActivationForm : Form
{
    private readonly string _deviceId;
    private readonly ILicenseStore _store;
    private readonly LicenseValidator _validator;
    private readonly TextBox _licenseCode = new() { Multiline = true, ScrollBars = ScrollBars.Vertical };

    public LicenseData? ActivatedLicense { get; private set; }

    public ActivationForm(
        string deviceId,
        ILicenseStore store,
        LicenseValidator validator,
        LicenseValidationStatus initialStatus)
    {
        _deviceId = deviceId;
        _store = store;
        _validator = validator;

        Text = "Puantaj Lisans Aktivasyonu";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(570, 520);

        var message = initialStatus == LicenseValidationStatus.Expired
            ? $"Lisans süreniz sona ermiştir.\n\nYenileme işlemi için iletişime geçin."
            : "Bu bilgisayar için geçerli bir lisans bulunamadı.\n\nLisans satın almak veya aktivasyon kodu almak için iletişime geçin.";

        var title = new Label
        {
            Text = "PUANTAJ LİSANS AKTİVASYONU",
            Font = new Font(Font, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(3, 3, 3, 12)
        };
        var description = new Label { Text = message, AutoSize = true };
        var contact = new Label
        {
            Text = $"Yetkili:\n{LicenseConstants.AuthorityName}\n\nTelefon:\n{LicenseConstants.AuthorityPhone}",
            AutoSize = true
        };
        var deviceLabel = new Label { Text = "Cihaz Kodu:", AutoSize = true };
        var deviceBox = new TextBox { Text = deviceId, ReadOnly = true, Dock = DockStyle.Fill };
        var codeLabel = new Label { Text = "Lisans Kodu:", AutoSize = true };
        _licenseCode.Dock = DockStyle.Fill;
        _licenseCode.Height = 110;

        var activate = new Button { Text = "Etkinleştir", AutoSize = true };
        activate.Click += ActivateClicked;
        var exit = new Button { Text = "Çıkış", AutoSize = true, DialogResult = DialogResult.Cancel };

        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.Add(activate);
        buttons.Controls.Add(exit);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 8
        };
        layout.Controls.Add(title);
        layout.Controls.Add(description);
        layout.Controls.Add(contact);
        layout.Controls.Add(deviceLabel);
        layout.Controls.Add(deviceBox);
        layout.Controls.Add(codeLabel);
        layout.Controls.Add(_licenseCode);
        layout.Controls.Add(buttons);
        Controls.Add(layout);
        AcceptButton = activate;
        CancelButton = exit;
    }

    private void ActivateClicked(object? sender, EventArgs eventArgs)
    {
        var result = _validator.Validate(
            _licenseCode.Text,
            _deviceId,
            PublicKeyProvider.Pem,
            DateTimeOffset.UtcNow);

        if (!result.IsValid)
        {
            var message = result.Status == LicenseValidationStatus.Expired
                ? "Lisans süreniz sona ermiştir.\n\nYenileme işlemi için iletişime geçin."
                : "Lisans kodu geçersiz veya bu bilgisayara ait değil.";
            MessageBox.Show(message, "Puantaj", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            _store.Write(_licenseCode.Text);
            ActivatedLicense = result.License;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Lisans kaydedilemedi.\n\n{exception.Message}",
                "Puantaj",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
