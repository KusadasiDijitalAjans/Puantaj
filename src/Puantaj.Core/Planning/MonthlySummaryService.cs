using System.Globalization;
using System.Text;
using Puantaj.Core.Data;

namespace Puantaj.Core.Planning;

public static class MonthlySummaryKeys
{
    public const string Work = "work";
    public const string WeeklyRest = "weekly-rest";
    public const string OfficialHoliday = "official-holiday";
    public const string MedicalReport = "medical-report";
    public const string PaidLeave = "paid-leave";
    public const string AnnualLeave = "annual-leave";
    public const string UnpaidLeave = "unpaid-leave";
    public const string CompensatoryLeave = "compensatory-leave";
    public const string Duty = "duty";
    public const string TotalLeave = "total-leave";
    public const string TotalValid = "total-valid";
}

public sealed record MonthlySummaryItem(string Key, string Title, int Days, int DisplayOrder, bool IsLeave);

public sealed record MonthlySummary(
    IReadOnlyList<MonthlySummaryItem> Items,
    int WorkDays,
    int LeaveDays,
    int ValidDays);

public sealed class MonthlySummaryService
{
    public MonthlySummary Calculate(
        IEnumerable<Assignment> assignments,
        IReadOnlyList<AssignmentCodeDefinition> definitions)
    {
        var resolver = new AssignmentCodeResolver(definitions);
        var counts = new Dictionary<string, (MonthlySummaryItem Item, int Count)>(StringComparer.OrdinalIgnoreCase);
        var workDays = 0;
        var leaveDays = 0;
        var validDays = 0;

        foreach (var assignment in assignments.OrderBy(item => item.WorkDate))
        {
            var definition = resolver.Resolve(assignment.Code);
            if (definition.IsEmploymentEnded) break;

            validDays++;
            var category = Classify(definition);
            if (category.Key == MonthlySummaryKeys.Work) workDays++;
            if (category.IsLeave) leaveDays++;
            counts[category.Key] = counts.TryGetValue(category.Key, out var current)
                ? (current.Item, current.Count + 1)
                : (category, 1);
        }

        var items = counts.Values
            .Where(value => value.Count > 0)
            .Select(value => value.Item with { Days = value.Count })
            .OrderBy(item => item.DisplayOrder)
            .ToList();

        if (leaveDays > 0)
            items.Add(new(MonthlySummaryKeys.TotalLeave, "Toplam İzin Günü", leaveDays, 900, false));
        if (validDays > 0)
            items.Add(new(MonthlySummaryKeys.TotalValid, "Toplam Geçerli Gün", validDays, 1000, false));

        return new(items, workDays, leaveDays, validDays);
    }

    private static MonthlySummaryItem Classify(AssignmentCodeDefinition definition)
    {
        if (definition.IsWorkShift)
            return new(MonthlySummaryKeys.Work, "Çalışma Günü", 0, 10, false);

        var code = definition.Code.Trim().ToUpper(CultureInfo.GetCultureInfo("tr-TR"));
        var description = Normalize(definition.Description);
        if (code == "HT" || description == "hafta tatili") return Known(MonthlySummaryKeys.WeeklyRest, "Hafta Tatili", 20, false);
        if (code == "RT" || description == "resmi tatil") return Known(MonthlySummaryKeys.OfficialHoliday, "Resmî Tatil", 30, false);
        if (code == "RP" || description.Contains("rapor", StringComparison.Ordinal)) return Known(MonthlySummaryKeys.MedicalReport, "Raporlu", 40, true);
        if (code == "Üİ" || description == "ucretli izin") return Known(MonthlySummaryKeys.PaidLeave, "Ücretli İzin", 50, true);
        if (code == "Yİ" || description == "yillik izin") return Known(MonthlySummaryKeys.AnnualLeave, "Yıllık İzin", 60, true);
        if (code == "ÜZ" || description == "ucretsiz izin") return Known(MonthlySummaryKeys.UnpaidLeave, "Ücretsiz İzin", 70, true);
        if (code == "Aİ" || description is "alacak izin" or "mazeret izni") return Known(MonthlySummaryKeys.CompensatoryLeave, definition.Description, 80, true);
        if (code == "G" || description == "gorevli") return Known(MonthlySummaryKeys.Duty, "Görevli", 90, false);

        var isLeave = description.Contains("izin", StringComparison.Ordinal) ||
            description.Contains("izni", StringComparison.Ordinal);
        return new($"custom:{code}", definition.Description, 0, 100 + definition.DisplayOrder, isLeave);
    }

    private static MonthlySummaryItem Known(string key, string title, int order, bool isLeave) =>
        new(key, title, 0, order, isLeave);

    private static string Normalize(string value)
    {
        var decomposed = value.Trim().ToLower(CultureInfo.GetCultureInfo("tr-TR")).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character == 'ı' ? 'i' : character);
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
