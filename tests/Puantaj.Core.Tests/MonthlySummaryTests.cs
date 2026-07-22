using Puantaj.Core.Data;
using Puantaj.Core.Planning;

namespace Puantaj.Core.Tests;

public sealed class MonthlySummaryTests
{
    private readonly MonthlySummaryService _service = new();

    [Fact]
    public void OnlyWorkCreatesWorkAndValidCardsWithoutZeroCards()
    {
        var summary = _service.Calculate([Assignment(1, "Z9"), Assignment(2, "Z9")],
            [Definition("Z9", "Gece Vardiyası", true, 1)]);

        Assert.Equal(2, summary.WorkDays);
        Assert.Equal(0, summary.LeaveDays);
        Assert.Equal(2, summary.ValidDays);
        Assert.Equal([MonthlySummaryKeys.Work, MonthlySummaryKeys.TotalValid], summary.Items.Select(item => item.Key));
        Assert.DoesNotContain(summary.Items, item => item.Days == 0);
    }

    [Fact]
    public void CategoriesAreCountedSeparatelyAndLeaveTotalExcludesRestHolidayAndDuty()
    {
        var definitions = Definitions();
        var assignments = new[]
        {
            Assignment(1, "A"), Assignment(2, "A"), Assignment(3, "HT"), Assignment(4, "RT"),
            Assignment(5, "RP"), Assignment(6, "Yİ"), Assignment(7, "ÜZ"), Assignment(8, "Üİ"),
            Assignment(9, "Aİ"), Assignment(10, "G")
        };

        var summary = _service.Calculate(assignments, definitions);

        Assert.Equal(2, summary.WorkDays);
        Assert.Equal(5, summary.LeaveDays);
        Assert.Equal(10, summary.ValidDays);
        Assert.Equal(1, Days(summary, MonthlySummaryKeys.WeeklyRest));
        Assert.Equal(1, Days(summary, MonthlySummaryKeys.OfficialHoliday));
        Assert.Equal(1, Days(summary, MonthlySummaryKeys.MedicalReport));
        Assert.Equal(1, Days(summary, MonthlySummaryKeys.AnnualLeave));
        Assert.Equal(1, Days(summary, MonthlySummaryKeys.UnpaidLeave));
        Assert.Equal(1, Days(summary, MonthlySummaryKeys.PaidLeave));
        Assert.Equal(1, Days(summary, MonthlySummaryKeys.CompensatoryLeave));
        Assert.Equal(1, Days(summary, MonthlySummaryKeys.Duty));
        Assert.Equal(5, Days(summary, MonthlySummaryKeys.TotalLeave));
        Assert.Equal(10, Days(summary, MonthlySummaryKeys.TotalValid));
        var existingTotals = new WeeklyPlanningService().CalculateTotals(assignments, definitions);
        Assert.Equal(new PreviewTotals(2, 5, 10), existingTotals);
    }

    [Fact]
    public void DynamicLeaveDefinitionIsShownAndIncludedWithoutInventingANewDataType()
    {
        var definitions = new[]
        {
            Definition("F", "Esnek Vardiya", true, 1),
            Definition("DGM", "Doğum İzni", false, 2),
            Definition("EGT", "Eğitim", false, 3)
        };
        var summary = _service.Calculate(
            [Assignment(1, "DGM"), Assignment(2, "DGM"), Assignment(3, "EGT")], definitions);

        Assert.Equal(2, summary.LeaveDays);
        Assert.Contains(summary.Items, item => item.Key == "custom:DGM" && item.Title == "Doğum İzni" && item.Days == 2 && item.IsLeave);
        Assert.Contains(summary.Items, item => item.Key == "custom:EGT" && item.Days == 1 && !item.IsLeave);
    }

    [Fact]
    public void EmploymentEndedDayAndFollowingDaysAreExcludedFromEverySummary()
    {
        var definitions = new[]
        {
            Definition("A", "Vardiya A", true, 1),
            Definition("İA", "İşten Ayrıldı", false, 2)
        };
        var summary = _service.Calculate(
            [Assignment(1, "A"), Assignment(2, "İA"), Assignment(3, "A")], definitions);

        Assert.Equal(1, summary.WorkDays);
        Assert.Equal(0, summary.LeaveDays);
        Assert.Equal(1, summary.ValidDays);
        Assert.DoesNotContain(summary.Items, item => item.Title == "İşten Ayrıldı");
    }

    [Fact]
    public void EmptyMonthProducesNoCards()
    {
        var summary = _service.Calculate([], Definitions());
        Assert.Empty(summary.Items);
        Assert.Equal(0, summary.ValidDays);
    }

    [Fact]
    public void CardsKeepBusinessDisplayOrderWhenZeroCategoriesAreSkipped()
    {
        var summary = _service.Calculate(
            [Assignment(1, "A"), Assignment(2, "RP"), Assignment(3, "Yİ"), Assignment(4, "G")], Definitions());

        Assert.Equal(
            [MonthlySummaryKeys.Work, MonthlySummaryKeys.MedicalReport, MonthlySummaryKeys.AnnualLeave,
                MonthlySummaryKeys.Duty, MonthlySummaryKeys.TotalLeave, MonthlySummaryKeys.TotalValid],
            summary.Items.Select(item => item.Key));
    }

    [Fact]
    public void MonthlySummaryUiSourceUsesRightSideVerticalCardsWithoutLegacyFooter()
    {
        var root = FindProjectRoot();
        var panel = File.ReadAllText(Path.Combine(root, "src", "PuantajApp", "MonthlySummaryPanel.cs"));
        var card = File.ReadAllText(Path.Combine(root, "src", "PuantajApp", "PersonnelCardControl.cs"));
        Assert.Contains("AutoScroll = true", panel, StringComparison.Ordinal);
        Assert.Contains("top += CardHeight + PreferredGap", panel, StringComparison.Ordinal);
        Assert.Contains("BuildSummary", card, StringComparison.Ordinal);
        Assert.DoesNotContain("Toplam Çalışma Günü:", card, StringComparison.Ordinal);
        Assert.Contains("_monthlySummary.SetSummary", card, StringComparison.Ordinal);
    }

    private static int Days(MonthlySummary summary, string key) => summary.Items.Single(item => item.Key == key).Days;

    private static IReadOnlyList<AssignmentCodeDefinition> Definitions() =>
    [
        Definition("A", "Vardiya A", true, 1), Definition("HT", "Hafta Tatili", false, 2),
        Definition("RT", "Resmî Tatil", false, 3), Definition("RP", "Raporlu", false, 4),
        Definition("Üİ", "Ücretli İzin", false, 5), Definition("Yİ", "Yıllık İzin", false, 6),
        Definition("ÜZ", "Ücretsiz İzin", false, 7), Definition("Aİ", "Alacak İzin", false, 8),
        Definition("G", "Görevli", false, 9), Definition("İA", "İşten Ayrıldı", false, 10)
    ];

    private static Assignment Assignment(int day, string code) =>
        new(day, 1, new DateOnly(2026, 7, day), code, DateTimeOffset.UtcNow);

    private static AssignmentCodeDefinition Definition(string code, string description, bool work, int order) =>
        new(code, description, null, null, work, order);

    private static string FindProjectRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Puantaj.sln"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Proje kökü bulunamadı.");
    }
}
