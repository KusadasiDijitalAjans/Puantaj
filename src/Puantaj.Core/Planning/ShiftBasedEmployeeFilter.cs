using System.Globalization;
using Puantaj.Core.Data;

namespace Puantaj.Core.Planning;

public static class ShiftBasedEmployeeFilter
{
    private static readonly string[] PositionTerms = ["Housman", "Laundry", "Kat istekleri"];
    private static readonly CompareInfo TurkishCompare = CultureInfo.GetCultureInfo("tr-TR").CompareInfo;

    public static bool IsShiftBasedEmployee(Employee employee) => IsShiftBasedPosition(employee.Position);

    public static bool IsShiftBasedPosition(string? position) => !string.IsNullOrWhiteSpace(position) &&
        PositionTerms.Any(term => TurkishCompare.IndexOf(position, term,
            CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0);
}
