using System.Security.Cryptography;
using Puantaj.Core.Device;
using Puantaj.Core.Licensing;

namespace Puantaj.Core.Tests;

public sealed class LicenseTests : IDisposable
{
    private const string DeviceId = "PUAN-7A42-91BC-D80F-552A";
    private readonly RSA _keys = RSA.Create(2048);
    private readonly LicenseSerializer _serializer = new();
    private readonly DateTimeOffset _now = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Valid_license_is_accepted()
    {
        var code = CreateCode(CreateLicense(expiresAt: _now.AddDays(30)));

        var result = Validate(code, DeviceId);

        Assert.True(result.IsValid);
        Assert.Equal("Housekeeping", result.License!.DepartmentName);
    }

    [Fact]
    public void License_for_another_device_is_rejected()
    {
        var code = CreateCode(CreateLicense(expiresAt: _now.AddDays(30)));

        var result = Validate(code, "PUAN-0000-0000-0000-0000");

        Assert.Equal(LicenseValidationStatus.DeviceMismatch, result.Status);
    }

    [Fact]
    public void Modified_license_is_rejected()
    {
        var signed = _serializer.DeserializeCode(CreateCode(CreateLicense(expiresAt: _now.AddDays(30))));
        var changed = signed with
        {
            License = signed.License with { HotelName = "Değiştirilmiş Otel" }
        };

        var result = Validate(_serializer.SerializeCode(changed), DeviceId);

        Assert.Equal(LicenseValidationStatus.InvalidSignature, result.Status);
    }

    [Fact]
    public void Expired_license_is_rejected()
    {
        var code = CreateCode(CreateLicense(expiresAt: _now.AddSeconds(-1)));

        var result = Validate(code, DeviceId);

        Assert.Equal(LicenseValidationStatus.Expired, result.Status);
    }

    [Fact]
    public void Lifetime_license_is_accepted_without_expiration()
    {
        var code = CreateCode(CreateLicense(isLifetime: true));

        var result = Validate(code, DeviceId);

        Assert.True(result.IsValid);
        Assert.True(result.License!.IsLifetime);
        Assert.Null(result.License.ExpiresAt);
    }

    [Fact]
    public void Wrong_public_key_is_rejected()
    {
        var code = CreateCode(CreateLicense(expiresAt: _now.AddDays(30)));
        using var otherKeys = RSA.Create(2048);
        var validator = new LicenseValidator(_serializer);

        var result = validator.Validate(
            code,
            DeviceId,
            otherKeys.ExportSubjectPublicKeyInfoPem(),
            _now);

        Assert.Equal(LicenseValidationStatus.InvalidSignature, result.Status);
    }

    [Fact]
    public void Missing_optional_device_components_do_not_crash_identity_generation()
    {
        var source = new FakeDeviceSource(new Dictionary<string, string?>
        {
            ["MachineGuid"] = " stable-guid ",
            ["SystemVolumeSerial"] = null,
            ["BiosUuid"] = string.Empty,
            ["ProcessorId"] = null
        });

        var deviceId = new DeviceIdentityService(source).GetDeviceId();

        Assert.Matches("^PUAN-[0-9A-F]{4}(-[0-9A-F]{4}){3}$", deviceId);
    }

    [Fact]
    public void Device_id_is_stable_despite_component_order_and_whitespace()
    {
        var first = new DeviceIdentityService(new FakeDeviceSource(new Dictionary<string, string?>
        {
            ["MachineGuid"] = "ABC",
            ["ProcessorId"] = "CPU  1"
        })).GetDeviceId();
        var second = new DeviceIdentityService(new FakeDeviceSource(new Dictionary<string, string?>
        {
            ["ProcessorId"] = " cpu 1 ",
            ["MachineGuid"] = " abc "
        })).GetDeviceId();

        Assert.Equal(first, second);
    }

    private LicenseValidationResult Validate(string code, string deviceId) =>
        new LicenseValidator(_serializer).Validate(
            code,
            deviceId,
            _keys.ExportSubjectPublicKeyInfoPem(),
            _now);

    private string CreateCode(LicenseData license) =>
        _serializer.SerializeCode(
            new RsaLicenseSigner(_serializer).Sign(license, _keys.ExportPkcs8PrivateKeyPem()));

    private LicenseData CreateLicense(bool isLifetime = false, DateTimeOffset? expiresAt = null) => new()
    {
        LicenseId = Guid.NewGuid(),
        CustomerName = "Test Müşteri",
        HotelName = "Test Otel",
        DepartmentName = "Housekeeping",
        DeviceId = DeviceId,
        IssuedAt = _now.AddDays(-1),
        ExpiresAt = isLifetime ? null : expiresAt,
        IsLifetime = isLifetime,
        LicenseVersion = LicenseConstants.CurrentVersion
    };

    public void Dispose() => _keys.Dispose();

    private sealed class FakeDeviceSource(IReadOnlyDictionary<string, string?> values)
        : IDeviceComponentSource
    {
        public IReadOnlyDictionary<string, string?> ReadComponents() => values;
    }
}
