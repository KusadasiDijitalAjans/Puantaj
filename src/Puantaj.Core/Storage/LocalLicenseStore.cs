using Puantaj.Core.Licensing;

namespace Puantaj.Core.Storage;

public sealed class LocalLicenseStore : ILicenseStore
{
    public string FilePath { get; }

    public LocalLicenseStore(string? localApplicationData = null)
    {
        var root = localApplicationData
                   ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        FilePath = Path.Combine(
            root,
            LicenseConstants.ApplicationFolderName,
            LicenseConstants.LicenseFileName);
    }

    public string? Read()
    {
        try
        {
            return File.Exists(FilePath) ? File.ReadAllText(FilePath).Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    public void Write(string licenseCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(licenseCode);
        var directory = Path.GetDirectoryName(FilePath)
                        ?? throw new InvalidOperationException("Lisans klasörü belirlenemedi.");
        Directory.CreateDirectory(directory);

        var temporaryPath = FilePath + ".tmp";
        File.WriteAllText(temporaryPath, licenseCode.Trim());
        File.Move(temporaryPath, FilePath, true);
    }
}
