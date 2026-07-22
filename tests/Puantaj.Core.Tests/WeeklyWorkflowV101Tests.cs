using Puantaj.Core.Data;
using Puantaj.Core.Planning;

namespace Puantaj.Core.Tests;

public sealed class WeeklyWorkflowV101Tests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"puantaj-v101-{Guid.NewGuid():N}");
    private readonly WeeklyPlanningService _planning = new();

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
