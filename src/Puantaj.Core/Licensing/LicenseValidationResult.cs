namespace Puantaj.Core.Licensing;

public enum LicenseValidationStatus
{
    Valid,
    Missing,
    Malformed,
    InvalidSignature,
    DeviceMismatch,
    NotYetValid,
    Expired,
    UnsupportedVersion,
    InvalidData
}

public sealed record LicenseValidationResult(
    LicenseValidationStatus Status,
    LicenseData? License = null,
    string? Error = null)
{
    public bool IsValid => Status == LicenseValidationStatus.Valid;
}
