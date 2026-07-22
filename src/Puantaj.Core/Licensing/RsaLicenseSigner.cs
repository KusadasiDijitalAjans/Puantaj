using System.Security.Cryptography;

namespace Puantaj.Core.Licensing;

public sealed class RsaLicenseSigner
{
    private readonly LicenseSerializer _serializer;

    public RsaLicenseSigner(LicenseSerializer serializer) => _serializer = serializer;

    public SignedLicense Sign(LicenseData license, string privateKeyPem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyPem);
        ValidateLicenseForSigning(license);

        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        var signature = rsa.SignData(
            _serializer.SerializePayload(license),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pss);

        return new SignedLicense
        {
            License = license,
            Signature = Convert.ToBase64String(signature)
        };
    }

    private static void ValidateLicenseForSigning(LicenseData license)
    {
        if (license.LicenseId == Guid.Empty ||
            string.IsNullOrWhiteSpace(license.CustomerName) ||
            string.IsNullOrWhiteSpace(license.HotelName) ||
            string.IsNullOrWhiteSpace(license.DepartmentName) ||
            string.IsNullOrWhiteSpace(license.DeviceId))
        {
            throw new ArgumentException("Lisansın zorunlu alanları eksik.", nameof(license));
        }

        if (!license.IsLifetime && license.ExpiresAt is null)
        {
            throw new ArgumentException("Süreli lisans için bitiş tarihi gereklidir.", nameof(license));
        }

        if (license.IsLifetime && license.ExpiresAt is not null)
        {
            throw new ArgumentException("Süresiz lisansın bitiş tarihi olmamalıdır.", nameof(license));
        }
    }
}
