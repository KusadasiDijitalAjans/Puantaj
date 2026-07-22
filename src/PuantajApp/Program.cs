using Puantaj.Core.Device;
using Puantaj.Core.Licensing;
using Puantaj.Core.Storage;
using Puantaj.Core.Data;

namespace PuantajApp;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var singleInstanceMutex = new Mutex(true, "Global\\Puantaj_SingleInstance_9F2E1C77-4B7D-4E4A-9C7E-6E3E2E9C6B1A", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("Puantaj zaten çalışıyor. Lütfen açık olan pencereyi kullanın.", "Puantaj", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();

        var serializer = new LicenseSerializer();
        var validator = new LicenseValidator(serializer);
        var store = new LocalLicenseStore();
        string deviceId;
        try
        {
            deviceId = new DeviceIdentityService(new WindowsDeviceComponentSource()).GetDeviceId();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Cihaz kodu üretilemedi.\n\n{exception.Message}",
                "Puantaj",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        var result = validator.Validate(
            store.Read(),
            deviceId,
            PublicKeyProvider.Pem,
            DateTimeOffset.UtcNow);

        if (!result.IsValid)
        {
            using var activation = new ActivationForm(deviceId, store, validator, result.Status);
            if (activation.ShowDialog() != DialogResult.OK || activation.ActivatedLicense is null)
            {
                return;
            }

            result = new LicenseValidationResult(
                LicenseValidationStatus.Valid,
                activation.ActivatedLicense);
        }

        var clockPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Puantaj", "license-time.dat");
        if (!new LicenseClockGuard().ValidateAndRecord(clockPath, result.License!.LicenseId, deviceId, DateTimeOffset.UtcNow))
        {
            MessageBox.Show("Sistem tarihi geri alınmış veya lisans zaman kaydı değiştirilmiş görünüyor. Tarih ve saat ayarlarını kontrol edin.",
                "Lisans doğrulanamadı", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        try
        {
            var database = new PuantajDatabase();
            database.Initialize();
            Application.Run(new MainForm(result.License!, database));
        }
        catch (Exception exception)
        {
            MessageBox.Show($"Uygulama başlatılamadı.\n\n{exception.Message}", "Puantaj", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
