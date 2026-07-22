namespace Puantaj.Core.Licensing;

public enum GeneratedLicenseType
{
    OneYear,
    Lifetime
}

public sealed record LicenseGenerationRequest(
    string CustomerName,
    string HotelName,
    string DepartmentName,
    string DeviceId,
    DateTimeOffset IssuedAt,
    GeneratedLicenseType LicenseType,
    string PrivateKeyPath);

public sealed class LicenseGenerationService
{
    private readonly LicenseSerializer _serializer;

    public LicenseGenerationService(LicenseSerializer serializer) => _serializer = serializer;

    public string GenerateCode(LicenseGenerationRequest request)
    {
        if (!File.Exists(request.PrivateKeyPath))
            throw new FileNotFoundException("Private key bulunamadı. Git dışında saklanan PEM anahtar dosyasını seçin.", request.PrivateKeyPath);

        var customer = Required(request.CustomerName, "Müşteri adı");
        var hotel = Required(request.HotelName, "Otel adı");
        var department = Required(request.DepartmentName, "Departman adı");
        var deviceId = Required(request.DeviceId, "Cihaz kodu").ToUpperInvariant();
        var issuedAt = request.IssuedAt.ToUniversalTime();
        var lifetime = request.LicenseType == GeneratedLicenseType.Lifetime;
        var license = new LicenseData
        {
            LicenseId = Guid.NewGuid(),
            CustomerName = customer,
            HotelName = hotel,
            DepartmentName = department,
            DeviceId = deviceId,
            IssuedAt = issuedAt,
            ExpiresAt = lifetime ? null : issuedAt.AddYears(1),
            IsLifetime = lifetime,
            LicenseVersion = LicenseConstants.CurrentVersion
        };
        var signed = new RsaLicenseSigner(_serializer).Sign(license, File.ReadAllText(request.PrivateKeyPath));
        return _serializer.SerializeCode(signed);
    }

    private static string Required(string value, string fieldName) => string.IsNullOrWhiteSpace(value)
        ? throw new ArgumentException($"{fieldName} boş bırakılamaz.")
        : value.Trim();
}
