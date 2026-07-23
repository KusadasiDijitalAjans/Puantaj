using ClosedXML.Excel;
using Puantaj.Core.Data;
using Puantaj.Core.Excel;
using Puantaj.Core.Planning;

namespace Puantaj.Core.Tests;

public sealed class EmployeeStartDateTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"puantaj-start-date-{Guid.NewGuid():N}");
    private static readonly IReadOnlyList<AssignmentCodeDefinition> CopyDefinitions =
    [
        new("A", "Vardiya A", null, null, true, 1),
        new("İA", "İşten Ayrıldı", null, null, false, 2)
    ];

    [Fact]
    public void FullMonthAndMidMonthEmployeesAreNotCopyMatches()
    {
        var source = Employee(1, null);
        var target = Employee(2, new DateOnly(2026, 7, 8));

        Assert.False(Matches(source, target, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31)));
    }

    [Fact]
    public void EmployeesWithSameActiveRangeInMonthAreCopyMatches()
    {
        var source = Employee(1, new DateOnly(2026, 7, 8));
        var target = Employee(2, new DateOnly(2026, 7, 8));

        Assert.True(Matches(source, target, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31)));
    }

    [Fact]
    public void NewEmployeeActiveForWholeSelectedWeekIsCopyMatch()
    {
        var source = Employee(1, null);
        var target = Employee(2, new DateOnly(2026, 7, 1));

        Assert.True(Matches(source, target, new DateOnly(2026, 7, 8), new DateOnly(2026, 7, 14)));
    }

    [Fact]
    public void EmployeeStartingMidWeekIsNotCopyMatchForFullWeek()
    {
        var source = Employee(1, null);
        var target = Employee(2, new DateOnly(2026, 7, 10));

        Assert.False(Matches(source, target, new DateOnly(2026, 7, 8), new DateOnly(2026, 7, 14)));
    }

    [Fact]
    public void SameActiveDayCountWithDifferentDatesIsNotCopyMatch()
    {
        var source = Employee(1, null);
        var target = Employee(2, new DateOnly(2026, 7, 6));
        var assignments = new[]
        {
            Assignment(1, new DateOnly(2026, 7, 6), "İA"),
            Assignment(2, new DateOnly(2026, 7, 11), "İA")
        };

        Assert.False(Matches(source, target, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 10), assignments));
    }

    [Fact]
    public void EmploymentEndDateParticipatesInCopyEligibility()
    {
        var source = Employee(1, null);
        var target = Employee(2, null);
        var matchingEnds = new[]
        {
            Assignment(1, new DateOnly(2026, 7, 20), "İA"),
            Assignment(2, new DateOnly(2026, 7, 20), "İA")
        };
        var differentEnds = new[]
        {
            Assignment(1, new DateOnly(2026, 7, 20), "İA"),
            Assignment(2, new DateOnly(2026, 7, 21), "İA")
        };

        Assert.True(Matches(source, target, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), matchingEnds));
        Assert.False(Matches(source, target, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), differentEnds));
    }

    [Theory]
    [InlineData(1, 31)]
    [InlineData(8, 24)]
    [InlineData(31, 1)]
    public void MonthCompletionRequiresOnlyDaysOnOrAfterHireDate(int hireDay, int expectedDays)
    {
        var database = CreateDatabase();
        var employee = database.AddEmployee("Erhan Durgun");
        database.UpdateEmployeeDetails(employee, "", "", new DateOnly(2026, 7, hireDay));
        for (var day = hireDay; day <= 31; day++) database.Assign(employee, new DateOnly(2026, 7, day), "A");

        var completion = database.EvaluateMonthCompletion(2026, 7);

        Assert.Empty(completion.Missing);
        Assert.Contains(employee, completion.CompletedEmployeeIds);
        Assert.Equal(expectedDays, database.GetAssignments(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31))
            .Count(item => item.EmployeeId == employee));
    }

    [Fact]
    public void EmployeeWithoutHireDateKeepsLegacyFullMonthBehavior()
    {
        var database = CreateDatabase();
        var employee = database.AddEmployee("Mevcut Personel");

        var completion = database.EvaluateMonthCompletion(2026, 7);

        Assert.Equal(31, completion.Missing.Count(item => item.EmployeeId == employee));
    }

    [Fact]
    public void AssignmentBeforeHireDateIsRejectedAndCopiesAreClipped()
    {
        var database = CreateDatabase();
        var source = database.AddEmployee("Kaynak");
        var target = database.AddEmployee("Hedef");
        database.UpdateEmployeeDetails(target, "", "", new DateOnly(2026, 7, 8));
        for (var day = 1; day <= 10; day++) database.Assign(source, new DateOnly(2026, 7, day), "A");

        Assert.Throws<InvalidOperationException>(() => database.Assign(target, new DateOnly(2026, 7, 7), "HT"));
        database.CopyMonthAssignments(source, target, 2026, 7);

        var copied = database.GetAssignments(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31))
            .Where(item => item.EmployeeId == target).ToArray();
        Assert.Equal([8, 9, 10], copied.Select(item => item.WorkDate.Day));
    }

    [Fact]
    public void HireAndEmploymentEndInSameMonthBoundCompletionRange()
    {
        var database = CreateDatabase();
        var employee = database.AddEmployee("Kısa Süreli Personel");
        database.UpdateEmployeeDetails(employee, "", "", new DateOnly(2026, 7, 8));
        for (var day = 8; day < 20; day++) database.Assign(employee, new DateOnly(2026, 7, day), "A");
        database.Assign(employee, new DateOnly(2026, 7, 20), "İA");

        var completion = database.EvaluateMonthCompletion(2026, 7);

        Assert.Empty(completion.Missing);
        Assert.Contains(employee, completion.CompletedEmployeeIds);
    }

    [Fact]
    public void LockedPastMonthDoesNotGainEmployeeHiredLater()
    {
        var database = CreateDatabase();
        database.LockMonth(2026, 6);
        var employee = database.AddEmployee("Temmuz Personeli");
        database.UpdateEmployeeDetails(employee, "", "", new DateOnly(2026, 7, 8));

        Assert.DoesNotContain(database.GetEmployeesForPeriod(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30)),
            item => item.Id == employee);
        Assert.Contains(database.GetEmployeesForPeriod(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31)),
            item => item.Id == employee);
        Assert.True(database.IsMonthLocked(2026, 6));
    }

    [Fact]
    public void MonthlyExcelColorsPreHireDaysAndKeepsPostTerminationBlack()
    {
        var root = FindProjectRoot();
        var template = MonthlyExcelExporter.FindMonthlyTemplate(Path.Combine(root, "templates"));
        var output = Path.Combine(_directory, "start-date.xlsx");
        var employee = new Employee(1, "Erhan Durgun", true, 1, DateTimeOffset.UtcNow, HireDate: new DateOnly(2026, 7, 8));
        var definitions = new[]
        {
            new AssignmentCodeDefinition("A", "Vardiya A", null, null, true, 1),
            new AssignmentCodeDefinition("İA", "İşten Ayrıldı", null, null, false, 2)
        };
        var assignments = Enumerable.Range(1, 19)
            .Select(day => new Assignment(day, 1, new DateOnly(2026, 7, day), "A", DateTimeOffset.UtcNow)).ToList();
        assignments.Add(new Assignment(20, 1, new DateOnly(2026, 7, 20), "İA", DateTimeOffset.UtcNow));

        new MonthlyExcelExporter().Export(template, output, "Otel", "Departman", 2026, 7, [employee], assignments, definitions);

        using var workbook = new XLWorkbook(output);
        var sheet = workbook.Worksheets.Single();
        for (var day = 1; day < 8; day++)
        {
            var cell = sheet.Cell(7, day + 4);
            Assert.True(cell.IsEmpty());
            Assert.Equal(System.Drawing.Color.LightPink.ToArgb(), cell.Style.Fill.BackgroundColor.Color.ToArgb());
        }
        Assert.Equal("X", sheet.Cell(7, 12).GetString());
        Assert.True(sheet.Cell(7, 24).IsEmpty());
        Assert.Equal(System.Drawing.Color.Black.ToArgb(), sheet.Cell(7, 25).Style.Fill.BackgroundColor.Color.ToArgb());
        Assert.DoesNotContain("İA", sheet.CellsUsed().Select(cell => cell.GetString()));
    }

    private PuantajDatabase CreateDatabase()
    {
        Directory.CreateDirectory(_directory);
        var database = new PuantajDatabase(Path.Combine(_directory, $"{Guid.NewGuid():N}.db"));
        database.Initialize();
        return database;
    }

    private static bool Matches(Employee source, Employee target, DateOnly from, DateOnly to,
        IReadOnlyList<Assignment>? assignments = null) =>
        CopyCandidateEligibility.HasMatchingActiveDates(source, target, from, to, assignments ?? [], CopyDefinitions);

    private static Employee Employee(long id, DateOnly? hireDate) =>
        new(id, $"Personel {id}", true, (int)id, DateTimeOffset.UtcNow, HireDate: hireDate);

    private static Assignment Assignment(long employeeId, DateOnly date, string code) =>
        new(employeeId, employeeId, date, code, DateTimeOffset.UtcNow);

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Puantaj.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); } catch { }
    }
}
