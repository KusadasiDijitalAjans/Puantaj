using Puantaj.Core.Data;
using Puantaj.Core.Licensing;
using Puantaj.Core.Planning;
using Puantaj.Core.Excel;
using ClosedXML.Excel;

namespace Puantaj.Core.Tests;

public sealed class PersonnelCardTests : IDisposable
{
    private readonly WeeklyPlanningService _service = new();
    private readonly string _temp = Path.Combine(Path.GetTempPath(), $"puantaj-card-{Guid.NewGuid():N}");

    [Fact]
    public void DefaultShiftAndYiUiExceptionsBuildCompleteWeek()
    {
        var week = _service.GetMonthWeeks(2026, 7)[1];
        var values = _service.BuildWeek(week, "A", new Dictionary<DateOnly, string> { [new(2026, 7, 8)] = "Yİ", [new(2026, 7, 9)] = "Üİ" });
        Assert.Equal("A", values[new(2026, 7, 6)]); Assert.Equal("Yİ", values[new(2026, 7, 8)]); Assert.Equal("Üİ", values[new(2026, 7, 9)]);
        Assert.Equal(WeekCompletionStatus.Completed, _service.GetStatus(week, values));
    }

    [Fact]
    public void SelectingSecondCodeForSameDayReplacesFirst()
    {
        var model = new WeeklySelectionModel(); var date = new DateOnly(2026, 7, 22);
        model.Select(date, "Yİ"); model.Select(date, "Üİ");
        Assert.Single(model.Values); Assert.Equal("Üİ", model.Values[date]);
    }

    [Fact]
    public void WeekMayHaveZeroOrTwoHtAndRtRp()
    {
        var week = _service.GetMonthWeeks(2026, 7)[1];
        Assert.DoesNotContain(_service.BuildWeek(week, "F", new Dictionary<DateOnly, string>()).Values, item => item == "HT");
        var special = _service.BuildWeek(week, "F", new Dictionary<DateOnly, string> { [week.ActiveFrom] = "HT", [week.ActiveFrom.AddDays(1)] = "HT", [week.ActiveFrom.AddDays(2)] = "RT", [week.ActiveFrom.AddDays(3)] = "RP" });
        Assert.Equal(2, special.Values.Count(item => item == "HT")); Assert.Contains("RT", special.Values); Assert.Contains("RP", special.Values);
    }

    [Fact]
    public void FirstAndLastWeeksContainOnlyMonthDays()
    {
        var weeks = _service.GetMonthWeeks(2025, 6);
        var first = _service.BuildWeek(weeks[0], "A", new Dictionary<DateOnly, string>());
        var last = _service.BuildWeek(weeks[^1], "A", new Dictionary<DateOnly, string>());
        Assert.All(first.Keys, date => Assert.Equal(6, date.Month)); Assert.All(last.Keys, date => Assert.Equal(6, date.Month));
        Assert.Single(first); Assert.Single(last);
    }

    [Fact]
    public void PartialWeekIsMissingAndEmptyWeekIsWaiting()
    {
        var week = _service.GetMonthWeeks(2026, 7)[1];
        Assert.Equal(WeekCompletionStatus.Waiting, _service.GetStatus(week, new Dictionary<DateOnly, string>()));
        Assert.Equal(WeekCompletionStatus.Missing, _service.GetStatus(week, new Dictionary<DateOnly, string> { [week.ActiveFrom] = "A" }));
    }

    [Fact]
    public void CopyWeekAdaptsDatesAndExcludesOutsideMonth()
    {
        var weeks = _service.GetMonthWeeks(2025, 9); var source = weeks[^2]; var target = weeks[^1];
        var sourceValues = _service.BuildWeek(source, "B", new Dictionary<DateOnly, string>());
        var copied = _service.CopyToWeek(source, target, sourceValues);
        Assert.Equal(2, copied.Count); Assert.All(copied.Keys, date => Assert.Equal(9, date.Month)); Assert.All(copied.Values, code => Assert.Equal("B", code));
    }

    [Fact]
    public void MonthlyTotalsExcludeEmploymentEndedDayAndAfter()
    {
        var definitions = new[] { new AssignmentCodeDefinition("F", "Vardiya", null, null, true, 1), new AssignmentCodeDefinition("X1", "İşten Ayrıldı", null, null, false, 2) };
        var assignments = new[] { Assignment(1, "F"), Assignment(2, "X1"), Assignment(3, "F") };
        var totals = _service.CalculateTotals(assignments, definitions);
        Assert.Equal(1, totals.WorkDays); Assert.Equal(1, totals.ValidDays); Assert.Equal(0, totals.LeaveDays);
    }

    [Fact]
    public void OneYearLicenseIsValidBeforeAndExpiredAfterExactAnniversary()
    {
        var issued = new DateTimeOffset(2026, 7, 22, 0, 0, 0, TimeSpan.Zero); var expires = issued.AddYears(1);
        Assert.True(issued.AddMonths(11) <= expires); Assert.True(issued.AddYears(1).AddTicks(1) > expires);
    }

    [Fact]
    public void ClockGuardDetectsRollbackAndAcceptsForwardTime()
    {
        Directory.CreateDirectory(_temp); var path = Path.Combine(_temp, "clock.dat"); var guard = new LicenseClockGuard(); var id = Guid.NewGuid();
        Assert.True(guard.ValidateAndRecord(path, id, "DEVICE", new DateTimeOffset(2026, 7, 22, 12, 0, 0, TimeSpan.Zero)));
        Assert.True(guard.ValidateAndRecord(path, id, "DEVICE", new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero)));
        Assert.False(guard.ValidateAndRecord(path, id, "DEVICE", new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void DatabaseListsOneHundredEmployeesInDisplayOrder()
    {
        Directory.CreateDirectory(_temp); var database = new PuantajDatabase(Path.Combine(_temp, "hundred.db")); database.Initialize();
        for (var index = 1; index <= 100; index++) database.AddEmployee($"Personel {index:000}");
        var employees = database.GetEmployees();
        Assert.Equal(100, employees.Count); Assert.Equal(Enumerable.Range(1, 100), employees.Select(item => item.DisplayOrder));
    }

    [Fact]
    public void ReferenceErhanDatasetProducesReopenableWeeklyAndMonthlyFiles()
    {
        var root = FindProjectRoot(); var output = Path.Combine(root, "test-output"); Directory.CreateDirectory(output);
        var employee = new Employee(1, "Erhan Durgun", true, 1, DateTimeOffset.UtcNow);
        var definitions = new[] { Definition("A", true, 1), Definition("B", true, 2), Definition("HT", false, 3), Definition("Yİ", false, 4), Definition("Üİ", false, 5), Definition("RP", false, 6), Definition("RT", false, 7) };
        var assignments = new List<Assignment>(); long id = 1;
        for (var day = 1; day <= 30; day++) { var code = day <= 7 ? "A" : day <= 14 ? "B" : day <= 21 ? "A" : day <= 28 ? "B" : "A"; assignments.Add(new(id++, 1, new DateOnly(2025, 6, day), code, DateTimeOffset.UtcNow)); }
        Replace(4, "Yİ"); Replace(5, "Üİ"); Replace(13, "HT"); Replace(14, "HT"); Replace(17, "RP"); Replace(20, "RT");
        var weekly = Path.Combine(output, "Gorev6_Haftalik_Erhan_Durgun.xlsx"); var monthly = Path.Combine(output, "Gorev6_Aylik_Erhan_Durgun_Haziran_2025.xlsx");
        new WeeklyExcelExporter().Export(WeeklyExcelExporter.FindWeeklyTemplate(Path.Combine(root, "templates")), weekly, "Test Otel", "Teknik Servis", new DateOnly(2025, 6, 2), [employee], assignments, definitions);
        new MonthlyExcelExporter().Export(MonthlyExcelExporter.FindMonthlyTemplate(Path.Combine(root, "templates")), monthly, "Test Otel", "Teknik Servis", 2025, 6, [employee], assignments, definitions);
        using var weeklyBook = new XLWorkbook(weekly); using var monthlyBook = new XLWorkbook(monthly);
        Assert.Equal("Yİ", weeklyBook.Worksheet(1).Cell(9, 8).GetString());
        Assert.Equal("X", monthlyBook.Worksheet(1).Cell(7, 5).GetString()); Assert.Equal("Yİ", monthlyBook.Worksheet(1).Cell(7, 8).GetString());
        Assert.False(string.IsNullOrWhiteSpace(weeklyBook.Worksheet(1).PageSetup.PrintAreas.First().RangeAddress.ToString()));
        Assert.False(string.IsNullOrWhiteSpace(monthlyBook.Worksheet(1).PageSetup.PrintAreas.First().RangeAddress.ToString()));
        void Replace(int day, string code) { var index = assignments.FindIndex(item => item.WorkDate.Day == day); assignments[index] = assignments[index] with { Code = code }; }
    }

    private static Assignment Assignment(int day, string code) => new(day, 1, new DateOnly(2026, 7, day), code, DateTimeOffset.UtcNow);
    private static AssignmentCodeDefinition Definition(string code, bool work, int order) => new(code, code, null, null, work, order);
    private static string FindProjectRoot() { var current = new DirectoryInfo(AppContext.BaseDirectory); while (current is not null) { if (File.Exists(Path.Combine(current.FullName, "Puantaj.sln"))) return current.FullName; current = current.Parent; } throw new DirectoryNotFoundException(); }
    public void Dispose() { try { if (Directory.Exists(_temp)) Directory.Delete(_temp, true); } catch { } }
}
