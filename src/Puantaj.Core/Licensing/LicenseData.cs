namespace Puantaj.Core.Licensing;

public sealed record LicenseData
{
    public required Guid LicenseId { get; init; }
    public required string CustomerName { get; init; }
    public required string HotelName { get; init; }
    public required string DepartmentName { get; init; }
    public required string DeviceId { get; init; }
    public required DateTimeOffset IssuedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public required bool IsLifetime { get; init; }
    public required int LicenseVersion { get; init; }
}
