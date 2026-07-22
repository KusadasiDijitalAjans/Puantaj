using Puantaj.Core.Calendar;
using Puantaj.Core.Data;

namespace Puantaj.Core.Tests;

public sealed class MvpTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"puantaj-test-{Guid.NewGuid():N}.db");

    [Theory]
    [InlineData("A", "X")]
    [InlineData("E", "X")]
    [InlineData("HT", "HT")]
    [InlineData("Yİ", "Yİ")]
    public void Codes_are_converted_for_monthly_excel(string source, string expected) =>
        Assert.Equal(expected, AttendanceCodes.ToMonthlyCode(source));

    [Fact]
    public void Si_is_not_accepted() =>
        Assert.Throws<ArgumentException>(() => AttendanceCodes.Normalize("Sİ"));

    [Theory]
    [InlineData(2024, 2, 29)]
    [InlineData(2025, 2, 28)]
    [InlineData(2026, 4, 30)]
    [InlineData(2026, 7, 31)]
    public void Month_lengths_are_calculated(int year, int month, int expected) =>
        Assert.Equal(expected, CalendarHelper.DaysInMonth(year, month));

    [Fact]
    public void Same_employee_and_date_is_updated_without_second_row()
    {
        var database = CreateDatabase();
        var employeeId = database.AddEmployee("Test Personel");
        var date = new DateOnly(2026, 7, 20);

        database.Assign(employeeId, date, "A");
        database.Assign(employeeId, date, "HT");

        var assignments = database.GetAssignments(date, date);
        var assignment = Assert.Single(assignments);
        Assert.Equal("HT", assignment.Code);
    }

    [Fact]
    public void Bulk_assignment_applies_to_every_selected_employee_and_day()
    {
        var database = CreateDatabase();
        var employees = Enumerable.Range(1, 20).Select(index => database.AddEmployee($"Personel {index}")).ToArray();
        var dates = new[] { new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 21), new DateOnly(2026, 7, 22) };

        database.AssignMany(employees, dates, "A");

        var assignments = database.GetAssignments(dates[0], dates[^1]);
        Assert.Equal(60, assignments.Count);
        Assert.All(assignments, assignment => Assert.Equal("A", assignment.Code));
    }

    [Fact]
    public void Arbitrary_work_shift_code_can_be_defined_and_assigned()
    {
        var database = CreateDatabase();
        database.SaveAssignmentCode("Z9", "Gece Ekibi", TimeSpan.FromHours(22), TimeSpan.FromHours(6), true);
        var employee = database.AddEmployee("Farklı Vardiya Personeli");
        var date = new DateOnly(2026, 7, 1);
        database.Assign(employee, date, "Z9");
        Assert.Equal("Z9", Assert.Single(database.GetAssignments(date, date)).Code);
    }

    private PuantajDatabase CreateDatabase()
    {
        var database = new PuantajDatabase(_databasePath);
        database.Initialize();
        return database;
    }

    public void Dispose()
    {
        if (File.Exists(_databasePath)) File.Delete(_databasePath);
        if (File.Exists(_databasePath + "-wal")) File.Delete(_databasePath + "-wal");
        if (File.Exists(_databasePath + "-shm")) File.Delete(_databasePath + "-shm");
    }
}
