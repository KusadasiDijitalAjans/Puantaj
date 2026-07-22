namespace Puantaj.Core.Data;

public sealed record AppSettings(
    string HotelName,
    string DepartmentName,
    string LogoPath,
    string DepartmentManager,
    string DepartmentManagerTitle,
    string HumanResourcesManager,
    string HumanResourcesTitle,
    string GeneralManager,
    string GeneralManagerTitle,
    decimal LogoSizeCm,
    decimal MarginLeftCm,
    decimal MarginRightCm,
    decimal MarginTopCm,
    decimal MarginBottomCm,
    bool PrintLogo,
    bool CenterHorizontally)
{
    public static AppSettings CreateDefault(string hotelName, string departmentName) => new(
        hotelName.Trim(), departmentName.Trim(), string.Empty, string.Empty, string.Empty,
        string.Empty, string.Empty, string.Empty, string.Empty, 2.5m, 0.7m, 0.7m, 0.7m, 0.7m, true, true);
}
