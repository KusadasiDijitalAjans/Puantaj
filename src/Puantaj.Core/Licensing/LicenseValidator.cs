using System.Security.Cryptography;
using System.Text.Json;

namespace Puantaj.Core.Licensing;

public sealed class LicenseValidator
{
    private readonly LicenseSerializer _serializer;

    public LicenseValidator(LicenseSerializer serializer) => _serializer = serializer;

    public LicenseValidationResult Validate(
        string? licenseCode,
        string expectedDeviceId,
        string publicKeyPem,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(licenseCode))
        {
            return new(LicenseValidationStatus.Missing);
        }

        SignedLicense signed;
        try
        {
            signed = _serializer.DeserializeCode(licenseCode);
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return new(LicenseValidationStatus.Malformed, Error: exception.Message);
        }

        if (signed.License is null || string.IsNullOrWhiteSpace(signed.Signature))
        {
            return new(LicenseValidationStatus.InvalidData);
        }

        bool signatureValid;
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);
            signatureValid = rsa.VerifyData(
                _serializer.SerializePayload(signed.License),
                Convert.FromBase64String(signed.Signature),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pss);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            return new(LicenseValidationStatus.InvalidSignature, Error: exception.Message);
        }

        if (!signatureValid)
        {
            return new(LicenseValidationStatus.InvalidSignature);
        }

        if (signed.License.LicenseVersion != LicenseConstants.CurrentVersion)
        {
            return new(LicenseValidationStatus.UnsupportedVersion, signed.License);
        }

        if (!string.Equals(signed.License.DeviceId, expectedDeviceId, StringComparison.Ordinal))
        {
            return new(LicenseValidationStatus.DeviceMismatch, signed.License);
        }

        if (now < signed.License.IssuedAt)
        {
            return new(LicenseValidationStatus.NotYetValid, signed.License);
        }

        if (!signed.License.IsLifetime &&
            (signed.License.ExpiresAt is null || now > signed.License.ExpiresAt.Value))
        {
            return new(LicenseValidationStatus.Expired, signed.License);
        }

        return new(LicenseValidationStatus.Valid, signed.License);
    }
}
