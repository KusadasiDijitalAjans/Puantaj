using ClosedXML.Excel;
using Puantaj.Core.Calendar;
using Puantaj.Core.Data;
using Puantaj.Core.Excel;

namespace Puantaj.Core.Tests;

public sealed class WeeklyAttendanceExportTests
{
    private static readonly IReadOnlyList<AssignmentCodeDefinition> Codes =
    [
        new("A", "Vardiya A", null, null, true, 1),
        new("HT", "Hafta Tatili", null, null, false, 2),
        new("İA", "İşten Ayrıldı", null, null, false, 3)
    ];

    [Fact]
    public void MonthWeekListUsesWholeMondaySundayCalendarWeeksAcrossBothBoundaries()
    {
        var weeks = CalendarHelper.WeeksIntersectingMonth(2026, 9);
        Assert.Equal(new DateOnly(2026, 8, 31), weeks[0]);
        Assert.Equal(new DateOnly(2026, 9, 28), weeks[^1]);
        Assert.All(weeks, monday => Assert.Equal(DayOfWeek.Monday, monday.DayOfWeek));
        Assert.Equal("31 Ağustos – 06 Eylül", WeeklyExcelExporter.FormatWeekRange(weeks[0]));
        Assert.Equal("28 Eylül – 04 Ekim", WeeklyExcelExporter.FormatWeekRange(weeks[^1]));
    }

    [Fact]
    public void CrossMonthAssignmentsAreReadAndRenderedWithoutChangingLockedPeriodData()
    {
        var monday = new DateOnly(2026, 8, 31);
        var assignments = Enumerable.Range(0, 7).Select(day => new Assignment(day + 1, 1, monday.AddDays(day), day == 0 ? "HT" : "A", DateTimeOffset.UtcNow)).ToArray();
        using var result = Export(monday, [Employee(1)], assignments);
        var sheet = result.Workbook.Worksheet(1);
        Assert.Equal("HT", sheet.Cell("D7").GetString());
        Assert.Equal("A", sheet.Cell("P7").GetString());
        Assert.Equal("31.08.2026 - 06.09.2026", sheet.Cell("R4").GetString());
    }

    [Fact]
    public void PersonnelFilteringAndLifecycleColorsFollowActiveIntersection()
    {
        var monday = new DateOnly(2026, 8, 10);
        var employees = new[]
        {
            Employee(1) with { HireDate = monday.AddDays(7) },
            Employee(2) with { HireDate = monday.AddDays(2) },
            Employee(3) with { IsActive = false },
            Employee(4)
        };
        var assignments = new[]
        {
            new Assignment(1, 3, monday.AddDays(1), "İA", DateTimeOffset.UtcNow),
            new Assignment(2, 4, monday, "A", DateTimeOffset.UtcNow)
        };
        using var result = Export(monday, employees, assignments);
        var sheet = result.Workbook.Worksheet(1);
        Assert.DoesNotContain("Personel 1", sheet.Column(2).Cells().Select(cell => cell.GetString()));
        Assert.Equal("Personel 2", sheet.Cell("B7").GetString());
        Assert.Equal(System.Drawing.Color.LightPink.ToArgb(), sheet.Cell("D7").Style.Fill.BackgroundColor.Color.ToArgb());
        Assert.Equal(System.Drawing.Color.LightPink.ToArgb(), sheet.Cell("F7").Style.Fill.BackgroundColor.Color.ToArgb());
        Assert.Equal("Personel 3", sheet.Cell("B8").GetString());
        Assert.Equal(System.Drawing.Color.Black.ToArgb(), sheet.Cell("F8").Style.Fill.BackgroundColor.Color.ToArgb());
        Assert.DoesNotContain("İA", sheet.CellsUsed().Select(cell => cell.GetString()));
    }

    [Fact]
    public void WorkCodesUseCentralYellowRuleAndSignatureCellsStayEmpty()
    {
        var monday = new DateOnly(2026, 8, 10);
        using var result = Export(monday, [Employee(1)],
        [new Assignment(1, 1, monday, "A", DateTimeOffset.UtcNow), new Assignment(2, 1, monday.AddDays(1), "HT", DateTimeOffset.UtcNow)]);
        var sheet = result.Workbook.Worksheet(1);
        Assert.Equal("A", sheet.Cell("D7").GetString());
        Assert.Equal("HT", sheet.Cell("F7").GetString());
        Assert.Equal(System.Drawing.Color.Yellow.ToArgb(), sheet.Cell("F7").Style.Fill.BackgroundColor.Color.ToArgb());
        foreach (var column in new[] { 5, 7, 9, 11, 13, 15, 17 }) Assert.True(sheet.Cell(7, column).IsEmpty());
    }

    [Fact]
    public void SettingsDoNotInjectLogoOrApprovalDataAndTemplateFooterStaysUnchanged()
    {
        var settings = AppSettings.CreateDefault("Otel", "Departman") with
        {
            DepartmentManager = "Departman Müdürü", DepartmentManagerTitle = "Departman",
            HumanResourcesManager = "İK Müdürü", HumanResourcesTitle = "İnsan Kaynakları",
            GeneralManager = "Genel Müdür", GeneralManagerTitle = "Genel Müdürlük"
        };
        using var result = Export(new DateOnly(2026, 8, 10), [Employee(1)], [], settings);
        var sheet = result.Workbook.Worksheet(1);
        Assert.Equal("Otel", sheet.Cell("C3").GetString()); Assert.Equal("Departman", sheet.Cell("C4").GetString());
        Assert.Empty(sheet.Pictures);
        Assert.DoesNotContain("Departman Müdürü", sheet.CellsUsed().Select(cell => cell.GetString()));
        Assert.DoesNotContain("İK Müdürü", sheet.CellsUsed().Select(cell => cell.GetString()));
        Assert.DoesNotContain("Genel Müdür", sheet.CellsUsed().Select(cell => cell.GetString()));
        AssertFooterMatchesTemplate(result.Workbook, WeeklyExcelExporter.FindWeeklyTemplate(Path.Combine(FindProjectRoot(), "templates")));
    }

    [Fact]
    public void CapacityCreatesRepeatedCompleteA4PagesWithoutSplittingFooter()
    {
        var employees = Enumerable.Range(1, WeeklyExcelExporter.EmployeesPerPage + 1).Select(Employee).ToArray();
        using var result = Export(new DateOnly(2026, 8, 10), employees, []);
        Assert.Equal(19, WeeklyExcelExporter.EmployeesPerPage); Assert.Equal(2, result.Workbook.Worksheets.Count);
        foreach (var sheet in result.Workbook.Worksheets)
        {
            Assert.Equal(XLPaperSize.A4Paper, sheet.PageSetup.PaperSize);
            Assert.Equal(XLPageOrientation.Landscape, sheet.PageSetup.PageOrientation);
            Assert.Equal(1, sheet.PageSetup.PagesWide); Assert.Equal(1, sheet.PageSetup.PagesTall);
            Assert.Equal("Work Time", sheet.Cell("D6").GetString()); Assert.Equal("Signature", sheet.Cell("E6").GetString());
            Assert.NotEmpty(sheet.Range("A28:S36").CellsUsed());
        }
        Assert.Equal("Personel 20", result.Workbook.Worksheet(2).Cell("B7").GetString());
    }

    [Theory]
    [InlineData("xlsx", "Haftalik_Puantaj_10-16_Agustos_2026.xlsx")]
    [InlineData("pdf", "Haftalik_Puantaj_10-16_Agustos_2026.pdf")]
    public void FileNamesIdentifyWeekAndFormat(string extension, string expected) =>
        Assert.Equal(expected, WeeklyExcelExporter.CreateOutputFileName(new DateOnly(2026, 8, 12), extension));

    [Fact]
    public void CrossMonthFileNameIncludesBothMonths() =>
        Assert.Equal("Haftalik_Puantaj_31_Agustos-06_Eylul_2026.xlsx", WeeklyExcelExporter.CreateOutputFileName(new DateOnly(2026, 9, 1)));

    private static Employee Employee(int id) => new(id, $"Personel {id}", true, id, DateTimeOffset.UtcNow, $"Görev {id}");

    private static ExportResult Export(DateOnly monday, IReadOnlyList<Employee> employees, IReadOnlyList<Assignment> assignments, AppSettings? settings = null)
    {
        var root = FindProjectRoot(); var output = Path.Combine(Path.GetTempPath(), $"weekly-attendance-{Guid.NewGuid():N}.xlsx");
        var template = WeeklyExcelExporter.FindWeeklyTemplate(Path.Combine(root, "templates"));
        new WeeklyExcelExporter().Export(template, output, "Otel", "Departman", monday, employees, assignments, Codes, settings);
        return new ExportResult(output, new XLWorkbook(output));
    }

    private static string FindProjectRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null) { if (File.Exists(Path.Combine(current.FullName, "Puantaj.sln"))) return current.FullName; current = current.Parent; }
        throw new DirectoryNotFoundException();
    }

    private static void AssertFooterMatchesTemplate(XLWorkbook output, string templatePath)
    {
        using var template = new XLWorkbook(templatePath);
        var expected = template.Worksheet(1); var actual = output.Worksheet(1);
        for (var row = 28; row <= 36; row++)
            for (var column = 1; column <= 19; column++)
                Assert.Equal(expected.Cell(row, column).GetString(), actual.Cell(row, column).GetString());
        Assert.Contains(actual.CellsUsed(), cell => cell.GetString() == "Department Head");
        Assert.Contains(actual.CellsUsed(), cell => cell.GetString().Contains("Signature", StringComparison.Ordinal));
    }

    private sealed record ExportResult(string Path, XLWorkbook Workbook) : IDisposable
    {
        public void Dispose() { Workbook.Dispose(); try { File.Delete(Path); } catch { } }
    }
}
