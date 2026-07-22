using System.Security.Cryptography;
using Puantaj.Core.Licensing;

namespace Puantaj.Core.Tests;

public sealed class LicenseGeneratorTests : IDisposable
{
    private const string DeviceId = "PUAN-1234-5678-9ABC-DEF0";
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"puantaj-generator-{Guid.NewGuid():N}");
    private readonly RSA _rsa = RSA.Create(2048);
    private readonly LicenseSerializer _serializer = new();
    private readonly DateTimeOffset _issuedAt = new(2026, 7, 22, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void OneYearLicenseIsValidAndExpiresExactlyOneYearAfterIssue()
    {
        var code = Generate(GeneratedLicenseType.OneYear);
        var signed = _serializer.DeserializeCode(code);

        Assert.False(signed.License.IsLifetime);
        Assert.Equal(_issuedAt.AddYears(1), signed.License.ExpiresAt);
        Assert.True(Validate(code, DeviceId, _issuedAt.AddMonths(11)).IsValid);
        Assert.Equal(LicenseValidationStatus.Expired, Validate(code, DeviceId, _issuedAt.AddYears(1).AddTicks(1)).Status);
    }

    [Fact]
    public void LifetimeLicenseHasNoExpiryAndValidates()
    {
        var code = Generate(GeneratedLicenseType.Lifetime);
        var signed = _serializer.DeserializeCode(code);
        Assert.True(signed.License.IsLifetime); Assert.Null(signed.License.ExpiresAt);
        Assert.True(Validate(code, DeviceId, _issuedAt.AddYears(25)).IsValid);
    }

    [Fact]
    public void GeneratedLicenseIsRejectedForWrongDevice()
    {
        var result = Validate(Generate(GeneratedLicenseType.OneYear), "PUAN-0000-0000-0000-0000", _issuedAt.AddDays(1));
        Assert.Equal(LicenseValidationStatus.DeviceMismatch, result.Status);
    }

    [Fact]
    public void ModifiedGeneratedLicenseIsRejected()
    {
        var signed = _serializer.DeserializeCode(Generate(GeneratedLicenseType.OneYear));
        var modified = signed with { License = signed.License with { HotelName = "Değiştirilmiş Otel" } };
        var result = Validate(_serializer.SerializeCode(modified), DeviceId, _issuedAt.AddDays(1));
        Assert.Equal(LicenseValidationStatus.InvalidSignature, result.Status);
    }

    [Fact]
    public void GeneratorAndValidatorWorkWithRsa4096KeyPair()
    {
        using var rsa4096 = RSA.Create(4096);
        var directory = Path.Combine(Path.GetTempPath(), $"puantaj-rsa4096-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var keyPath = Path.Combine(directory, "production-style.private.pem");
            File.WriteAllText(keyPath, rsa4096.ExportPkcs8PrivateKeyPem());
            var publicKeyPem = rsa4096.ExportSubjectPublicKeyInfoPem();
            var generationService = new LicenseGenerationService(_serializer);
            var validator = new LicenseValidator(_serializer);

            var oneYearCode = generationService.GenerateCode(Request(GeneratedLicenseType.OneYear, keyPath));
            var oneYearResult = validator.Validate(oneYearCode, DeviceId, publicKeyPem, _issuedAt.AddDays(1));
            Assert.True(oneYearResult.IsValid);

            var lifetimeCode = generationService.GenerateCode(Request(GeneratedLicenseType.Lifetime, keyPath));
            var lifetimeResult = validator.Validate(lifetimeCode, DeviceId, publicKeyPem, _issuedAt.AddYears(25));
            Assert.True(lifetimeResult.IsValid);
            Assert.True(lifetimeResult.License!.IsLifetime);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void MissingPrivateKeyReturnsClearTurkishError()
    {
        var exception = Assert.Throws<FileNotFoundException>(() => new LicenseGenerationService(_serializer).GenerateCode(Request(
            GeneratedLicenseType.OneYear, Path.Combine(_directory, "olmayan.private.pem"))));
        Assert.Contains("Private key bulunamadı", exception.Message, StringComparison.Ordinal);
        Assert.Contains("PEM anahtar dosyasını seçin", exception.Message, StringComparison.Ordinal);
    }

    private string Generate(GeneratedLicenseType type)
    {
        Directory.CreateDirectory(_directory); var keyPath = Path.Combine(_directory, "test.private.pem");
        File.WriteAllText(keyPath, _rsa.ExportPkcs8PrivateKeyPem());
        return new LicenseGenerationService(_serializer).GenerateCode(Request(type, keyPath));
    }

    private LicenseGenerationRequest Request(GeneratedLicenseType type, string keyPath) => new(
        "Test Müşteri", "Test Otel", "Teknik Servis", DeviceId, _issuedAt, type, keyPath);

    private LicenseValidationResult Validate(string code, string deviceId, DateTimeOffset now) =>
        new LicenseValidator(_serializer).Validate(code, deviceId, _rsa.ExportSubjectPublicKeyInfoPem(), now);

    public void Dispose()
    {
        _rsa.Dispose(); try { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); } catch { }
    }
}
