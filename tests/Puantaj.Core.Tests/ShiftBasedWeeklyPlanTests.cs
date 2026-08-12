using ClosedXML.Excel;
using Puantaj.Core.Data;
using Puantaj.Core.Excel;
using Puantaj.Core.Planning;

namespace Puantaj.Core.Tests;

public sealed class ShiftBasedWeeklyPlanTests
{
    private static readonly DateOnly Monday = new(2026, 8, 10);
    private static readonly IReadOnlyList<AssignmentCodeDefinition> Codes =
    [
        new("A", "A", TimeSpan.FromHours(9), TimeSpan.FromHours(17), true, 1),
        new("B", "B", TimeSpan.FromHours(16), TimeSpan.Zero, true, 2),
        new("C", "C", TimeSpan.Zero, TimeSpan.FromHours(8), true, 3),
        new("HT", "Hafta Tatili", null, null, false, 4),
        new("RT", "Resmi Tatil", null, null, false, 5),
        new("RP", "Raporlu", null, null, false, 6),
        new("İA", "İşten Ayrıldı", null, null, false, 7)
    ];

    [Theory]
    [InlineData("Housman")]
    [InlineData("HOUSMAN")]
    [InlineData("housman")]
    [InlineData("Senior Housman")]
    [InlineData("Laundry")]
    [InlineData("LAUNDRY")]
    [InlineData("Laundry Personeli")]
    [InlineData("Kat istekleri")]
    [InlineData("KAT İSTEKLERİ")]
    [InlineData("Kat İstekleri Personeli")]
    public void PositionMatcherUsesTurkishCaseInsensitiveContains(string position) =>
        Assert.True(ShiftBasedEmployeeFilter.IsShiftBasedPosition(position));

    [Fact]
    public void NormalPositionIsNotShiftBased() => Assert.False(ShiftBasedEmployeeFilter.IsShiftBasedPosition("Kat Şefi"));

    [Fact]
    public void WeeklyAndShiftBasedReportsAreExactOppositePositionPartitions()
    {
        var employees = new[] { Employee(1, "Housman"), Employee(2, "Laundry"), Employee(3, "Kat İstekleri Personeli"), Employee(4, "Resepsiyon") };
        using var normal = ExportNormal(employees, Assignments(employees)); using var shifted = ExportShifted(employees, Assignments(employees));
        var normalNames = normal.Workbook.Worksheets.SelectMany(sheet => sheet.Column(2).CellsUsed()).Select(cell => cell.GetString()).ToArray();
        var shiftedNames = shifted.Workbook.Worksheets.SelectMany(sheet => sheet.Column(2).CellsUsed()).Select(cell => cell.GetString()).ToArray();
        Assert.Contains("Personel 4", normalNames); Assert.DoesNotContain("Personel 4", shiftedNames);
        foreach (var id in new[] { 1, 2, 3 }) { Assert.DoesNotContain($"Personel {id}", normalNames); Assert.Contains($"Personel {id}", shiftedNames); }
        Assert.DoesNotContain(normalNames.Intersect(shiftedNames), name => name.StartsWith("Personel", StringComparison.Ordinal));
    }

    [Fact]
    public void WeeklyEmployeeNameAndPositionKeepTemplateFormattingWithoutFill()
    {
        var employee = Employee(1, "Resepsiyon");
        using var output = ExportNormal([employee], [new Assignment(1, 1, Monday, "A", DateTimeOffset.UtcNow)]);
        var sheet = output.Workbook.Worksheet(1);
        Assert.Equal(XLFillPatternValues.None, sheet.Cell(7, 2).Style.Fill.PatternType);
        Assert.Equal(XLFillPatternValues.None, sheet.Cell(7, 3).Style.Fill.PatternType);
        Assert.NotEqual(XLBorderStyleValues.None, sheet.Cell(7, 2).Style.Border.BottomBorder);
        Assert.NotEqual(XLBorderStyleValues.None, sheet.Cell(7, 3).Style.Border.BottomBorder);
    }

    [Fact]
    public void GroupsUseDominantRealWeeklyShiftAndConfiguredHours()
    {
        var employees = new[] { Employee(1, "Housman"), Employee(2, "Laundry"), Employee(3, "Kat İstekleri") };
        var assignments = new List<Assignment>(); long id = 1;
        for (var day = 0; day < 7; day++)
        {
            assignments.Add(new(id++, 1, Monday.AddDays(day), day == 6 ? "B" : "A", DateTimeOffset.UtcNow));
            assignments.Add(new(id++, 2, Monday.AddDays(day), "B", DateTimeOffset.UtcNow));
            assignments.Add(new(id++, 3, Monday.AddDays(day), "C", DateTimeOffset.UtcNow));
        }
        var groups = ShiftBasedWeeklyExcelExporter.BuildGroups(employees, assignments, Codes, Monday, Monday.AddDays(6));
        Assert.Equal(["A", "B", "C"], groups.Select(group => group.Definition!.Code));
        Assert.Equal(1, Assert.Single(groups[0].Employees).Id);
        using var output = ExportShifted(employees, assignments); var text = output.Workbook.Worksheet(1).Column(2).CellsUsed().Select(cell => cell.GetString()).ToArray();
        Assert.Contains("09/00-17/00 VARDİYASI", text); Assert.Contains("16/00-00/00 VARDİYASI", text); Assert.Contains("00/00-08/00 VARDİYASI", text);
    }

    [Fact]
    public void GroupHeadersMoveWithEmployeeCountsWithoutLargeGaps()
    {
        var employees = Enumerable.Range(1, 8).Select(id => Employee(id, id <= 6 ? "Housman" : "Laundry")).ToArray();
        var assignments = employees.Select((employee, index) => new Assignment(index + 1, employee.Id, Monday, employee.Id <= 6 ? "A" : "B", DateTimeOffset.UtcNow)).ToArray();
        using var output = ExportShifted(employees, assignments); var sheet = output.Workbook.Worksheet(1);
        var headers = sheet.Column(2).CellsUsed().Where(cell => cell.GetString().Contains("VARDİYASI", StringComparison.Ordinal)).Select(cell => cell.Address.RowNumber).ToArray();
        Assert.Equal([7, 14], headers);
    }

    [Fact]
    public void EmptyWorkCellsStayUnfilledWhileSpecialCodesAreYellow()
    {
        var employee = Employee(1, "Housman");
        var assignments = new[]
        {
            new Assignment(1, 1, Monday, "A", DateTimeOffset.UtcNow),
            new Assignment(2, 1, Monday.AddDays(1), "HT", DateTimeOffset.UtcNow),
            new Assignment(3, 1, Monday.AddDays(2), "RT", DateTimeOffset.UtcNow),
            new Assignment(4, 1, Monday.AddDays(3), "RP", DateTimeOffset.UtcNow)
        };
        using var output = ExportShifted([employee], assignments); var sheet = output.Workbook.Worksheet(1);
        var row = FindEmployeeRow(sheet, 1);
        Assert.Equal(XLFillPatternValues.None, sheet.Cell(row, 12).Style.Fill.PatternType);
        foreach (var column in new[] { 6, 8, 10 })
            Assert.Equal(System.Drawing.Color.Yellow.ToArgb(), sheet.Cell(row, column).Style.Fill.BackgroundColor.Color.ToArgb());
    }

    [Fact]
    public void ActiveShiftLegendUsesConfiguredOrderAndHoursAndMovesWithData()
    {
        var active = new AssignmentCodeDefinition[]
        {
            new("D", "D", TimeSpan.FromHours(23), TimeSpan.FromHours(7), true, 4),
            Codes[2], Codes[0], Codes[1], Codes[3], Codes[4], Codes[5]
        };
        var all = active.Append(new AssignmentCodeDefinition("F", "F", TimeSpan.FromHours(5), TimeSpan.FromHours(13), true, 5)).ToArray();
        var employees = Enumerable.Range(1, 6).Select(id => Employee(id, "Housman")).ToArray();
        using var output = ExportShifted(employees, Assignments(employees), definitions: all, activeDefinitions: active);
        var sheet = output.Workbook.Worksheet(1);
        var htRow = sheet.Column(3).CellsUsed().Single(cell => cell.GetString().StartsWith("HT :", StringComparison.Ordinal)).Address.RowNumber;
        Assert.Equal(17, htRow);
        Assert.Equal(new[] { "A", "B", "C", "D" }, Enumerable.Range(htRow, 4).Select(row => sheet.Cell(row, 1).GetString()));
        Assert.Equal("09.00 / 17.00", sheet.Cell(htRow, 2).GetString());
        Assert.Equal("23.00 / 07.00", sheet.Cell(htRow + 3, 2).GetString());
        Assert.DoesNotContain("F", sheet.Column(1).CellsUsed().Select(cell => cell.GetString()));
        Assert.Contains("Department Head", sheet.CellsUsed().Select(cell => cell.GetString()));
        Assert.Contains("Yİ : Yıllık İzin/ Annual Leave", sheet.CellsUsed().Select(cell => cell.GetString()));
    }

    [Fact]
    public void LifecycleColorsSpecialCodesSignaturesAndCrossMonthArePreserved()
    {
        var monday = new DateOnly(2026, 8, 31);
        var employee = Employee(1, "Housman") with { HireDate = monday.AddDays(2) };
        var assignments = new[] { new Assignment(1, 1, monday.AddDays(2), "A", DateTimeOffset.UtcNow), new Assignment(2, 1, monday.AddDays(3), "HT", DateTimeOffset.UtcNow) };
        using var output = ExportShifted([employee], assignments, monday); var sheet = output.Workbook.Worksheet(1); var row = FindEmployeeRow(sheet, 1);
        Assert.Equal(System.Drawing.Color.LightPink.ToArgb(), sheet.Cell(row, 4).Style.Fill.BackgroundColor.Color.ToArgb());
        Assert.Equal(System.Drawing.Color.Yellow.ToArgb(), sheet.Cell(row, 10).Style.Fill.BackgroundColor.Color.ToArgb());
        Assert.Equal("31.08.2026 - 06.09.2026", sheet.Cell("R4").GetString());
        foreach (var column in new[] { 5, 7, 9, 11, 13, 15, 17 }) Assert.True(sheet.Cell(row, column).IsEmpty());
    }

    [Fact]
    public void EndedAndFutureEmployeesAreHandledWithoutSettingsBranding()
    {
        var future = Employee(1, "Housman") with { HireDate = Monday.AddDays(7) };
        var ended = Employee(2, "Laundry") with { IsActive = false };
        var active = Employee(3, "Kat İstekleri");
        var assignments = new[] { new Assignment(1, 2, Monday.AddDays(1), "İA", DateTimeOffset.UtcNow), new Assignment(2, 3, Monday, "A", DateTimeOffset.UtcNow) };
        var settings = AppSettings.CreateDefault("Otel", "Departman") with { DepartmentManager = "Müdür", HumanResourcesManager = "İK", GeneralManager = "GM" };
        using var output = ExportShifted([future, ended, active], assignments, Monday, settings); var sheet = output.Workbook.Worksheet(1);
        Assert.DoesNotContain("Personel 1", sheet.Column(2).CellsUsed().Select(cell => cell.GetString()));
        var endedRow = FindEmployeeRow(sheet, 2); Assert.Equal(System.Drawing.Color.Black.ToArgb(), sheet.Cell(endedRow, 6).Style.Fill.BackgroundColor.Color.ToArgb());
        Assert.DoesNotContain("İA", sheet.CellsUsed().Select(cell => cell.GetString()));
        Assert.Empty(sheet.Pictures);
        Assert.DoesNotContain("Müdür", sheet.CellsUsed().Select(cell => cell.GetString()));
        Assert.DoesNotContain("İK", sheet.CellsUsed().Select(cell => cell.GetString()));
        Assert.DoesNotContain("GM", sheet.CellsUsed().Select(cell => cell.GetString()));
        using var templateStream = File.Open(ShiftBasedWeeklyExcelExporter.FindTemplate(Path.Combine(Root(), "templates")), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var template = new XLWorkbook(templateStream);
        var expectedFooterTexts = template.Worksheet(1).RangeUsed()!.CellsUsed()
            .Where(cell => cell.Address.RowNumber >= 25 && cell.Address.ColumnNumber >= 3)
            .Select(cell => cell.GetString()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        var actualTexts = sheet.CellsUsed().Select(cell => cell.GetString()).ToArray();
        Assert.All(expectedFooterTexts, value => Assert.Contains(value, actualTexts));
    }

    [Fact]
    public void LargeGroupsCreateMultipleCompleteA4Pages()
    {
        var employees = Enumerable.Range(1, 40).Select(id => Employee(id, "Housman")).ToArray();
        using var output = ExportShifted(employees, Assignments(employees));
        Assert.True(output.Workbook.Worksheets.Count >= 3);
        Assert.All(output.Workbook.Worksheets, sheet => { Assert.Equal(XLPaperSize.A4Paper, sheet.PageSetup.PaperSize); Assert.Equal(1, sheet.PageSetup.PagesWide); Assert.Equal("Otel", sheet.Cell("C3").GetString()); });
    }

    [Theory]
    [InlineData("xlsx", "Vardiyali_Calisma_Plani_10-16_Agustos_2026.xlsx")]
    [InlineData("pdf", "Vardiyali_Calisma_Plani_10-16_Agustos_2026.pdf")]
    public void FileNamesAreSafeAndExplicit(string extension, string expected) => Assert.Equal(expected, ShiftBasedWeeklyExcelExporter.CreateOutputFileName(Monday, extension));

    [Fact]
    public void MonthlyAttendanceIsNotAffectedByWeeklyPositionPartition()
    {
        var root = Root(); var path = Path.Combine(Path.GetTempPath(), $"monthly-shifted-{Guid.NewGuid():N}.xlsx");
        try
        {
            var employee = Employee(1, "Senior Housman");
            new MonthlyExcelExporter().Export(MonthlyExcelExporter.FindMonthlyTemplate(Path.Combine(root, "templates")), path,
                "Otel", "Departman", 2026, 8, [employee], [new Assignment(1, 1, new DateOnly(2026, 8, 10), "A", DateTimeOffset.UtcNow)], Codes);
            using var workbook = new XLWorkbook(path); Assert.Equal("Personel 1", workbook.Worksheet(1).Cell("C7").GetString());
        }
        finally { try { File.Delete(path); } catch { } }
    }

    private static Employee Employee(int id, string position) => new(id, $"Personel {id}", true, id, DateTimeOffset.UtcNow, position);
    private static IReadOnlyList<Assignment> Assignments(IEnumerable<Employee> employees) => employees.Select((employee, index) => new Assignment(index + 1, employee.Id, Monday, index % 3 == 0 ? "A" : index % 3 == 1 ? "B" : "C", DateTimeOffset.UtcNow)).ToArray();
    private static int FindEmployeeRow(IXLWorksheet sheet, int id) => sheet.Column(2).CellsUsed().Single(cell => cell.GetString() == $"Personel {id}").Address.RowNumber;

    private static Result ExportNormal(IReadOnlyList<Employee> employees, IReadOnlyList<Assignment> assignments)
    {
        var root = Root(); var path = Path.Combine(Path.GetTempPath(), $"normal-{Guid.NewGuid():N}.xlsx");
        new WeeklyExcelExporter().Export(WeeklyExcelExporter.FindWeeklyTemplate(Path.Combine(root, "templates")), path, "Otel", "Departman", Monday, employees, assignments, Codes);
        return new(path);
    }
    private static Result ExportShifted(IReadOnlyList<Employee> employees, IReadOnlyList<Assignment> assignments,
        DateOnly? monday = null, AppSettings? settings = null, IReadOnlyList<AssignmentCodeDefinition>? definitions = null,
        IReadOnlyList<AssignmentCodeDefinition>? activeDefinitions = null)
    {
        var root = Root(); var path = Path.Combine(Path.GetTempPath(), $"shifted-{Guid.NewGuid():N}.xlsx");
        new ShiftBasedWeeklyExcelExporter().Export(ShiftBasedWeeklyExcelExporter.FindTemplate(Path.Combine(root, "templates")), path,
            "Otel", "Departman", monday ?? Monday, employees, assignments, definitions ?? Codes, settings, activeDefinitions);
        return new(path);
    }
    private static string Root() { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory is not null) { if (File.Exists(Path.Combine(directory.FullName, "Puantaj.sln"))) return directory.FullName; directory = directory.Parent; } throw new DirectoryNotFoundException(); }
    private sealed class Result : IDisposable { public string Path { get; } public XLWorkbook Workbook { get; } public Result(string path) { Path = path; Workbook = new(path); } public void Dispose() { Workbook.Dispose(); try { File.Delete(Path); } catch { } } }
}
