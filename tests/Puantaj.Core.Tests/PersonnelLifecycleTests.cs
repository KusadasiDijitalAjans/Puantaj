using ClosedXML.Excel;
using Puantaj.Core.Data;
using Puantaj.Core.Excel;

namespace Puantaj.Core.Tests;

public sealed class PersonnelLifecycleTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"puantaj-lifecycle-{Guid.NewGuid():N}");

    [Fact]
    public void DeactivatedEmployeeLeavesActiveListAndSearchButRemainsInHistoricalPeriod()
    {
        var database = CreateDatabase(); var employeeId = database.AddEmployee("Erhan Durgun");
        database.Assign(employeeId, new DateOnly(2025, 6, 4), "A");
        Assert.Contains(database.GetEmployees(), item => item.Id == employeeId);

        database.SetEmployeeActive(employeeId, false);

        Assert.DoesNotContain(database.GetEmployees(), item => item.Id == employeeId);
        Assert.DoesNotContain(database.GetEmployees().Where(item => item.FullName.Contains("Erhan", StringComparison.CurrentCultureIgnoreCase)), item => item.Id == employeeId);
        Assert.Contains(database.GetEmployeesForPeriod(new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 30)), item => item.Id == employeeId);
        Assert.DoesNotContain(database.GetEmployeesForPeriod(new DateOnly(2025, 7, 1), new DateOnly(2025, 7, 31)), item => item.Id == employeeId);
    }

    [Fact]
    public void HistoricalExcelContainsInactiveEmployeeAndReactivationReturnsEmployeeToActiveList()
    {
        var database = CreateDatabase(); database.EnsureSettings("Test Otel", "Teknik Servis");
        var employeeId = database.AddEmployee("Erhan Durgun"); var date = new DateOnly(2025, 6, 4);
        database.Assign(employeeId, date, "A"); database.SetEmployeeActive(employeeId, false);
        var from = new DateOnly(2025, 6, 1); var to = new DateOnly(2025, 6, 30); var output = Path.Combine(_directory, "historical.xlsx");
        var root = FindProjectRoot();
        new MonthlyExcelExporter().Export(MonthlyExcelExporter.FindMonthlyTemplate(Path.Combine(root, "templates")), output,
            "Test Otel", "Teknik Servis", 2025, 6, database.GetEmployeesForPeriod(from, to), database.GetAssignments(from, to),
            database.GetAssignmentCodes(), database.GetSettings());

        using (var workbook = new XLWorkbook(output)) Assert.Equal("Erhan Durgun", workbook.Worksheet(1).Cell("C7").GetString());
        Assert.False(database.GetEmployees(false).Single(item => item.Id == employeeId).IsActive);

        database.SetEmployeeActive(employeeId, true);
        Assert.Contains(database.GetEmployees(), item => item.Id == employeeId);
    }

    [Fact]
    public void UpdateEmployeeDetailsPersistsHireDateAndDoesNotResetItWhenReSubmitted()
    {
        var database = CreateDatabase();
        var employeeId = database.AddEmployee("Ayşe Yıldız");

        database.UpdateEmployeeDetails(employeeId, "Kat Şefi", "Gündüz", new DateOnly(2024, 3, 15));
        var stored = database.GetEmployees(false).Single(item => item.Id == employeeId);
        Assert.Equal(new DateOnly(2024, 3, 15), stored.HireDate);
        Assert.Equal("Kat Şefi", stored.Position);
        Assert.Equal("Gündüz", stored.WorkPattern);

        // Re-editing another field (name) while passing the existing HireDate through must not wipe it out,
        // guarding against the previous UI bug where HireDate was always sent as a literal null.
        database.UpdateEmployee(employeeId, "Ayşe Yılmaz");
        database.UpdateEmployeeDetails(employeeId, stored.Position, stored.WorkPattern, stored.HireDate);
        var reloaded = database.GetEmployees(false).Single(item => item.Id == employeeId);
        Assert.Equal(new DateOnly(2024, 3, 15), reloaded.HireDate);
        Assert.Equal("Ayşe Yılmaz", reloaded.FullName);
    }

    private PuantajDatabase CreateDatabase()
    {
        Directory.CreateDirectory(_directory); var database = new PuantajDatabase(Path.Combine(_directory, "puantaj.db")); database.Initialize(); return database;
    }

    private static string FindProjectRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null) { if (File.Exists(Path.Combine(current.FullName, "Puantaj.sln"))) return current.FullName; current = current.Parent; }
        throw new DirectoryNotFoundException("Proje kökü bulunamadı.");
    }

    public void Dispose() { try { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); } catch { } }
}
