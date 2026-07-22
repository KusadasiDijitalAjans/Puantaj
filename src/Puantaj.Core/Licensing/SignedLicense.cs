namespace Puantaj.Core.Licensing;

public sealed record SignedLicense
{
    public required LicenseData License { get; init; }
    public required string Signature { get; init; }
}
