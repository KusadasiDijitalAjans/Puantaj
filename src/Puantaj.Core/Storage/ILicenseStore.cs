namespace Puantaj.Core.Storage;

public interface ILicenseStore
{
    string? Read();
    void Write(string licenseCode);
}
