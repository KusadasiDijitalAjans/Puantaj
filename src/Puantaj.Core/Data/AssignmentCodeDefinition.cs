using System.Globalization;

namespace Puantaj.Core.Data;

public sealed record AssignmentCodeDefinition(
    string Code, string Description, TimeSpan? StartTime, TimeSpan? EndTime, bool IsWorkShift, int DisplayOrder)
{
    public bool IsEmploymentEnded => CultureInfo.GetCultureInfo("tr-TR").CompareInfo.Compare(
        Description.Trim(), "İşten Ayrıldı", CompareOptions.IgnoreCase) == 0;
}
