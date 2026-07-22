using Puantaj.Core.Data;
using Puantaj.Core.Planning;

namespace Puantaj.Core.Tests;

public sealed class WeeklyWorkflowV101Tests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"puantaj-v101-{Guid.NewGuid():N}");
    private readonly WeeklyPlanningService _planning = new();

    public static IEnumerable<object[]> WeeklyCopyCases()
    {
        foreach (var (year, month) in new[] { (2024, 2), (2025, 2), (2026, 4), (2026, 7), (2026, 8), (2026, 11) })
            foreach (var week in new WeeklyPlanningService().GetMonthWeeks(year, month)) yield return [year, month, week.Number];
    }

    [Fact]
    public void CopyTargetsAreSafeForEmptySingleAndMultipleWeekLists()
    {
        var source = new MonthWeek(1, new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 9), new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 9));
        Assert.Empty(_planning.GetCopyTargets([], source));
        Assert.Empty(_planning.GetCopyTargets([source], source));
        var second = source with { Number = 2, Monday = source.Monday.AddDays(7), Sunday = source.Sunday.AddDays(7), ActiveFrom = source.ActiveFrom.AddDays(7), ActiveTo = source.ActiveTo.AddDays(7) };
        Assert.Equal([second], _planning.GetCopyTargets([source, second], source));
    }

    [Fact]
    public void WeekCanBeCreatedOnceAndSecondCreateIsRejected()
    {
        var database = CreateDatabase(); var employee = database.AddEmployee("Personel"); var week = _planning.GetMonthWeeks(2026, 8)[1];
        var values = _planning.BuildWeek(week, "A", new Dictionary<DateOnly, string>());
        database.SaveWeekAssignments(employee, week.ActiveFrom, week.ActiveTo, values, false);
        var exception = Assert.Throws<InvalidOperationException>(() => database.SaveWeekAssignments(employee, week.ActiveFrom, week.ActiveTo, values, false));
        Assert.Equal("Bu hafta daha önce oluşturuldu. Değişiklik yapmak için Düzenle butonunu kullanın.", exception.Message);
        Assert.Equal(values.Count, database.GetAssignments(week.ActiveFrom, week.ActiveTo).Count);
    }

    [Fact]
    public void EditLoadsExistingValuesMovesLeaveAndUpdatesWithoutDuplicates()
    {
        var database = CreateDatabase(); var employee = database.AddEmployee("Personel"); var week = _planning.GetMonthWeeks(2026, 8)[1];
        var original = _planning.BuildWeek(week, "A", new Dictionary<DateOnly, string> { [week.ActiveFrom.AddDays(1)] = "Yİ" });
        database.SaveWeekAssignments(employee, week.ActiveFrom, week.ActiveTo, original, false);
        var loaded = database.GetAssignments(week.ActiveFrom, week.ActiveTo).ToDictionary(item => item.WorkDate, item => item.Code);
        Assert.Equal("Yİ", loaded[week.ActiveFrom.AddDays(1)]);
        var edited = _planning.BuildWeek(week, "A", new Dictionary<DateOnly, string> { [week.ActiveFrom.AddDays(2)] = "Yİ" });
        database.SaveWeekAssignments(employee, week.ActiveFrom, week.ActiveTo, edited, true);
        var result = database.GetAssignments(week.ActiveFrom, week.ActiveTo);
        Assert.Equal(edited.Count, result.Count); Assert.Equal("A", result.Single(item => item.WorkDate == week.ActiveFrom.AddDays(1)).Code);
        Assert.Equal("Yİ", result.Single(item => item.WorkDate == week.ActiveFrom.AddDays(2)).Code);
    }

    [Fact]
    public void CopyMonthReplacesTargetAssignmentsWithoutDuplicates()
    {
        var database = CreateDatabase();
        var source = database.AddEmployee("Kaynak");
        var target = database.AddEmployee("Hedef");
        database.Assign(source, new DateOnly(2026, 8, 1), "A");
        database.Assign(source, new DateOnly(2026, 8, 2), "HT");
        database.Assign(source, new DateOnly(2026, 8, 3), "RP");
        database.Assign(target, new DateOnly(2026, 8, 1), "Yİ");
        database.Assign(target, new DateOnly(2026, 8, 4), "RT");

        database.CopyMonthAssignments(source, target, 2026, 8);
        database.CopyMonthAssignments(source, target, 2026, 8);

        var copied = database.GetAssignments(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31))
            .Where(item => item.EmployeeId == target).OrderBy(item => item.WorkDate).ToList();
        Assert.Equal(3, copied.Count);
        Assert.Equal(["A", "HT", "RP"], copied.Select(item => item.Code));
        Assert.DoesNotContain(copied, item => item.WorkDate.Day == 4);
    }

    [Theory]
    [MemberData(nameof(WeeklyCopyCases))]
    public void WeeklyPersonnelCopyKeepsExactCalendarWeekAcrossMonthShapes(int year, int month, int weekNumber)
    {
        var database = CreateDatabase(); var sourceEmployee = database.AddEmployee("Kaynak"); var targetEmployee = database.AddEmployee("Hedef");
        database.SaveAssignmentCode("F", "Yeni Vardiya", TimeSpan.FromHours(10), TimeSpan.FromHours(18), true);
        var week = _planning.GetMonthWeeks(year, month).Single(item => item.Number == weekNumber);
        var codes = new[] { "F", "HT", "RT", "RP", "Yİ", "Üİ", "G" };
        var sourceValues = Enumerable.Range(0, week.ActiveTo.DayNumber - week.ActiveFrom.DayNumber + 1)
            .ToDictionary(offset => week.ActiveFrom.AddDays(offset), offset => codes[(week.ActiveFrom.AddDays(offset).DayNumber - week.Monday.DayNumber) % 7]);
        database.SaveWeekAssignments(sourceEmployee, week.ActiveFrom, week.ActiveTo, sourceValues, false);
        database.Assign(targetEmployee, week.ActiveFrom, "A");

        database.CopyWeekAssignments(sourceEmployee, targetEmployee, week.ActiveFrom, week.ActiveTo);
        database.CopyWeekAssignments(sourceEmployee, targetEmployee, week.ActiveFrom, week.ActiveTo);

        var copied = database.GetAssignments(week.ActiveFrom, week.ActiveTo).Where(item => item.EmployeeId == targetEmployee).ToList();
        Assert.Equal(sourceValues.Count, copied.Count);
        Assert.Equal(sourceValues, copied.ToDictionary(item => item.WorkDate, item => item.Code));
        Assert.All(copied, item => Assert.Equal(month, item.WorkDate.Month));
    }

    [Fact]
    public void EmptyWeeklySourceDoesNotAlterExistingTarget()
    {
        var database = CreateDatabase(); var source = database.AddEmployee("Kaynak"); var target = database.AddEmployee("Hedef");
        var week = _planning.GetMonthWeeks(2026, 7)[2]; database.Assign(target, week.ActiveFrom, "A");
        Assert.Throws<InvalidOperationException>(() => database.CopyWeekAssignments(source, target, week.ActiveFrom, week.ActiveTo));
        Assert.Equal("A", Assert.Single(database.GetAssignments(week.ActiveFrom, week.ActiveTo)).Code);
    }

    [Fact]
    public void WeeklyCopyRejectsSameOrInactivePersonnelWithoutPartialChanges()
    {
        var database = CreateDatabase(); var source = database.AddEmployee("Kaynak"); var target = database.AddEmployee("Hedef");
        var week = _planning.GetMonthWeeks(2026, 7)[1]; database.Assign(source, week.ActiveFrom, "A"); database.Assign(target, week.ActiveFrom, "HT");
        Assert.Throws<ArgumentException>(() => database.CopyWeekAssignments(source, source, week.ActiveFrom, week.ActiveTo));
        database.SetEmployeeActive(target, false);
        Assert.Throws<InvalidOperationException>(() => database.CopyWeekAssignments(source, target, week.ActiveFrom, week.ActiveTo));
        Assert.Equal("HT", database.GetAssignments(week.ActiveFrom, week.ActiveTo).Single(item => item.EmployeeId == target).Code);
    }

    [Theory]
    [InlineData(2025, 2, 28)]
    [InlineData(2024, 2, 29)]
    [InlineData(2026, 4, 30)]
    [InlineData(2026, 7, 31)]
    public void EmploymentEndedExpandsFromSelectedDayThroughRealMonthEnd(int year, int month, int days)
    {
        var definitions = Definitions(); var selected = new DateOnly(year, month, 5);
        var result = _planning.ExpandEmploymentEndedToMonthEnd(new Dictionary<DateOnly, string> { [selected] = "İA" }, definitions);
        Assert.Equal(days - 4, result.Count); Assert.All(result, item => Assert.Equal("İA", item.Value));
        Assert.Equal(selected, result.Keys.Min()); Assert.Equal(new DateOnly(year, month, days), result.Keys.Max());
    }

    [Fact]
    public void EditingEmploymentEndedSelectionRemovesOldAutomaticFutureValues()
    {
        var database = CreateDatabase(); var employee = database.AddEmployee("Personel"); var week = _planning.GetMonthWeeks(2026, 7)[0];
        var ended = _planning.ExpandEmploymentEndedToMonthEnd(new Dictionary<DateOnly, string> { [new DateOnly(2026, 7, 5)] = "İA" }, database.GetAssignmentCodes());
        database.SaveWeekAssignments(employee, week.ActiveFrom, week.ActiveTo, ended, false);
        var regular = _planning.BuildWeek(week, "A", new Dictionary<DateOnly, string>());
        database.SaveWeekAssignments(employee, week.ActiveFrom, week.ActiveTo, regular, true);
        var month = database.GetAssignments(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));
        Assert.DoesNotContain(month, item => item.Code == "İA"); Assert.Equal(week.ActiveTo.DayNumber - week.ActiveFrom.DayNumber + 1, month.Count);
    }

    [Fact]
    public void ClearingTemporarySelectionDoesNotDeleteSavedAssignments()
    {
        var database = CreateDatabase(); var employee = database.AddEmployee("Personel"); var date = new DateOnly(2026, 8, 3); database.Assign(employee, date, "A");
        var selection = new WeeklySelectionModel(); selection.Select(date, "Yİ"); selection.Clear();
        Assert.Empty(selection.Values); Assert.Equal("A", database.GetAssignments(date, date).Single().Code);
    }

    [Fact]
    public void ClearingWeekImmediatelyRemovesOnlyThatWeeksPersistedAssignments()
    {
        var database = CreateDatabase(); var employee = database.AddEmployee("Personel");
        var weeks = _planning.GetMonthWeeks(2026, 7); var first = weeks[0]; var second = weeks[1];
        database.SaveWeekAssignments(employee, first.ActiveFrom, first.ActiveTo, _planning.BuildWeek(first, "A", new Dictionary<DateOnly, string>()), false);
        database.SaveWeekAssignments(employee, second.ActiveFrom, second.ActiveTo, _planning.BuildWeek(second, "B", new Dictionary<DateOnly, string>()), false);

        database.ClearWeekAssignments(employee, first.ActiveFrom, first.ActiveTo);

        var month = database.GetAssignments(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));
        Assert.DoesNotContain(month, item => item.WorkDate >= first.ActiveFrom && item.WorkDate <= first.ActiveTo);
        Assert.Contains(month, item => item.WorkDate >= second.ActiveFrom && item.WorkDate <= second.ActiveTo);
    }

    [Theory]
    [InlineData(2024, 2, 29)]
    [InlineData(2025, 2, 28)]
    [InlineData(2026, 4, 30)]
    [InlineData(2026, 7, 31)]
    public void WeekPatternAtomicallyFillsEveryValidMonthDayWithoutDuplicates(int year, int month, int days)
    {
        var database = CreateDatabase(); var employee = database.AddEmployee("Personel");
        var source = _planning.GetMonthWeeks(year, month).First(week => week.ActiveTo.DayNumber - week.ActiveFrom.DayNumber == 6);
        var values = _planning.BuildWeek(source, "A", new Dictionary<DateOnly, string> { [source.Monday.AddDays(5)] = "HT", [source.Monday.AddDays(6)] = "RT" });
        database.SaveWeekAssignments(employee, source.ActiveFrom, source.ActiveTo, values, false);

        database.ApplyWeekPatternToMonth(employee, source.Monday, source.ActiveFrom, source.ActiveTo, year, month);
        database.ApplyWeekPatternToMonth(employee, source.Monday, source.ActiveFrom, source.ActiveTo, year, month);

        var result = database.GetAssignments(new DateOnly(year, month, 1), new DateOnly(year, month, days));
        Assert.Equal(days, result.Count); Assert.Equal(days, result.Select(item => item.WorkDate).Distinct().Count());
        Assert.All(result.Where(item => item.WorkDate.DayOfWeek == DayOfWeek.Saturday), item => Assert.Equal("HT", item.Code));
        Assert.All(result.Where(item => item.WorkDate.DayOfWeek == DayOfWeek.Sunday), item => Assert.Equal("RT", item.Code));
    }

    [Theory]
    [InlineData(2026, 6)]
    [InlineData(2026, 9)]
    [InlineData(2026, 7)]
    [InlineData(2026, 10)]
    [InlineData(2026, 5)]
    [InlineData(2026, 8)]
    [InlineData(2026, 11)]
    public void IncompleteFirstWeekUsesDayOfWeekPatternAndDominantShiftForMissingDays(int year, int month)
    {
        var database = CreateDatabase(); var employee = database.AddEmployee("Personel");
        var first = _planning.GetMonthWeeks(year, month)[0];
        var values = _planning.BuildWeek(first, "A", new Dictionary<DateOnly, string>
        {
            [first.ActiveFrom.AddDays(Math.Min(1, first.ActiveTo.DayNumber - first.ActiveFrom.DayNumber))] = "HT"
        });
        database.SaveWeekAssignments(employee, first.ActiveFrom, first.ActiveTo, values, false);
        database.ApplyWeekPatternToMonth(employee, first.Monday, first.ActiveFrom, first.ActiveTo, year, month);

        var result = database.GetAssignments(new DateOnly(year, month, 1), new DateOnly(year, month, DateTime.DaysInMonth(year, month)));
        Assert.Equal(DateTime.DaysInMonth(year, month), result.Count);
        Assert.All(result, item => Assert.Equal(month, item.WorkDate.Month));
        var leaveDay = first.ActiveFrom.AddDays(Math.Min(1, first.ActiveTo.DayNumber - first.ActiveFrom.DayNumber)).DayOfWeek;
        Assert.All(result.Where(item => item.WorkDate.DayOfWeek == leaveDay), item => Assert.Equal("HT", item.Code));
    }

    [Fact]
    public void DeletedCustomShiftIsHiddenButHistoricalAssignmentsRemainResolvable()
    {
        var database = CreateDatabase(); database.SaveAssignmentCode("F", "Vardiya F", TimeSpan.FromHours(7), TimeSpan.FromHours(15), true);
        var employee = database.AddEmployee("Personel"); var historicalDate = new DateOnly(2025, 6, 10);
        database.Assign(employee, historicalDate, "F");
        database.SynchronizeAssignmentCodes(database.GetAssignmentCodes().Where(item => item.Code != "F").ToList());

        Assert.DoesNotContain(database.GetAssignmentCodes(), item => item.Code == "F");
        Assert.Contains(database.GetAssignmentCodes(false), item => item.Code == "F");
        Assert.Equal("F", Assert.Single(database.GetAssignments(historicalDate, historicalDate)).Code);
        database.SaveAssignmentCode("F", "Vardiya F Yeni", TimeSpan.FromHours(8), TimeSpan.FromHours(16), true);
        Assert.Contains(database.GetAssignmentCodes(), item => item.Code == "F" && item.Description == "Vardiya F Yeni");
    }

    [Fact]
    public void CustomShiftDeletionIsPersistentAndReferencedShiftIsArchived()
    {
        var database = CreateDatabase(); database.SaveAssignmentCode("F", "Vardiya F", TimeSpan.FromHours(7), TimeSpan.FromHours(15), true);
        var definitions = database.GetAssignmentCodes().Where(item => item.Code != "F").ToList();
        database.SynchronizeAssignmentCodes(definitions);
        Assert.DoesNotContain(database.GetAssignmentCodes(), item => item.Code == "F");
        database.SaveAssignmentCode("F", "Vardiya F", TimeSpan.FromHours(7), TimeSpan.FromHours(15), true);
        var employee = database.AddEmployee("Personel"); database.Assign(employee, new DateOnly(2026, 7, 1), "F");
        database.SynchronizeAssignmentCodes(definitions);
        Assert.DoesNotContain(database.GetAssignmentCodes(), item => item.Code == "F");
        Assert.Contains(database.GetAssignmentCodes(false), item => item.Code == "F");
    }

    [Fact]
    public void WeekendDetectionRemainsCorrectAcrossMonthBoundary()
    {
        var week = _planning.GetMonthWeeks(2026, 8)[0];
        Assert.Equal(DayOfWeek.Saturday, week.Monday.AddDays(5).DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, week.Monday.AddDays(6).DayOfWeek);
        Assert.NotEqual(week.Monday.Month, week.Monday.AddDays(6).Month);
    }

    [Fact]
    public void WeeklyUiSourceKeepsRequiredButtonOrderAndHasNoResultPanel()
    {
        var root = FindProjectRoot(); var source = File.ReadAllText(Path.Combine(root, "src", "PuantajApp", "PersonnelCardControl.cs"));
        Assert.DoesNotContain("HAFTALIK SONUÇ", source, StringComparison.Ordinal);
        var edit = source.IndexOf("_editButton = Button", StringComparison.Ordinal);
        var clear = source.IndexOf("_clearButton = Button", StringComparison.Ordinal);
        var create = source.IndexOf("_generateButton = Button", StringComparison.Ordinal);
        Assert.True(edit >= 0 && edit < clear && clear < create);
        Assert.Contains("Vardiyalar / Günler", source, StringComparison.Ordinal);
        Assert.Contains("DayOfWeek.Saturday or DayOfWeek.Sunday", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PublishProjectCopiesTemplatesToInstallationOutput()
    {
        var root = FindProjectRoot(); var project = File.ReadAllText(Path.Combine(root, "src", "PuantajApp", "PuantajApp.csproj"));
        Assert.Contains("templates\\%(Filename)%(Extension)", project, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>", project, StringComparison.Ordinal);
        Assert.Contains("<ExcludeFromSingleFile>true</ExcludeFromSingleFile>", project, StringComparison.Ordinal);
    }

    [Fact]
    public void EmployeeEditorOnlyRequestsNameAndPosition()
    {
        var root = FindProjectRoot(); var source = File.ReadAllText(Path.Combine(root, "src", "PuantajApp", "EmployeesControl.cs"));
        Assert.Contains("Ad Soyad:", source, StringComparison.Ordinal);
        Assert.Contains("Görevi:", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Çalışma Şekli", source, StringComparison.Ordinal);
        Assert.DoesNotContain("İşe giriş tarihi:", source, StringComparison.Ordinal);
    }

    private PuantajDatabase CreateDatabase()
    {
        Directory.CreateDirectory(_directory); var database = new PuantajDatabase(Path.Combine(_directory, $"{Guid.NewGuid():N}.db")); database.Initialize(); return database;
    }

    private static IReadOnlyList<AssignmentCodeDefinition> Definitions() =>
        [new("A", "Vardiya A", null, null, true, 1), new("İA", "İşten Ayrıldı", null, null, false, 2)];

    private static string FindProjectRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null) { if (File.Exists(Path.Combine(current.FullName, "Puantaj.sln"))) return current.FullName; current = current.Parent; }
        throw new DirectoryNotFoundException("Proje kökü bulunamadı.");
    }

    public void Dispose() { try { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); } catch { } }
}
