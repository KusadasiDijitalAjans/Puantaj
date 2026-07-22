using System.Security.Cryptography;
using ClosedXML.Excel;
using Puantaj.Core.Data;
using Puantaj.Core.Excel;

namespace Puantaj.Core.Tests;

public sealed class OutputTests
{
    [Fact]
    public void Weekly_and_monthly_samples_are_created_reopenable_and_do_not_change_templates()
    {
        var root = FindProjectRoot();
        var templates = Path.Combine(root, "templates");
        var weeklyTemplate = WeeklyExcelExporter.FindWeeklyTemplate(templates);
        var monthlyTemplate = MonthlyExcelExporter.FindMonthlyTemplate(templates);
        var weeklyHash = Hash(weeklyTemplate);
        var monthlyHash = Hash(monthlyTemplate);
        var output = Path.Combine(root, "test-output");
        Directory.CreateDirectory(output);
        var weeklyOutput = Path.Combine(output, "Haftalik_Calisma_Plani_Test.xlsx");
        var monthlyOutput = Path.Combine(output, "Puantaj_Test.xlsx");
        var monday = new DateOnly(2026, 7, 20);
        var employees = Employees();
        var assignments = Assignments(monday);

        new WeeklyExcelExporter().Export(weeklyTemplate, weeklyOutput, "Test Otel", "Test Departman", monday, employees, assignments);
        new MonthlyExcelExporter().Export(monthlyTemplate, monthlyOutput, "Test Otel", "Test Departman", 2026, 7, employees, assignments);

        Assert.True(new FileInfo(weeklyOutput).Length > 0);
        Assert.True(new FileInfo(monthlyOutput).Length > 0);
        using (var weekly = new XLWorkbook(weeklyOutput))
        {
            var sheet = Assert.Single(weekly.Worksheets);
            Assert.Equal("A", sheet.Cell("D9").GetString());
            Assert.Equal("HT", sheet.Cell("F9").GetString());
            Assert.Equal(XLPaperSize.A4Paper, sheet.PageSetup.PaperSize);
            Assert.NotEmpty(sheet.PageSetup.PrintAreas);
            Assert.Equal(1, sheet.PageSetup.PagesWide);
            Assert.True(sheet.PageSetup.CenterHorizontally);
            Assert.Equal(new DateOnly(2026, 7, 20).ToDateTime(TimeOnly.MinValue), sheet.Cell("D6").GetDateTime());
            Assert.Equal(new DateOnly(2026, 7, 26).ToDateTime(TimeOnly.MinValue), sheet.Cell("P6").GetDateTime());
        }
        using (var monthly = new XLWorkbook(monthlyOutput))
        {
            var sheet = Assert.Single(monthly.Worksheets);
            Assert.Equal("X", sheet.Cell("X7").GetString()); // 20 Temmuz, kaynak A
            Assert.Equal("HT", sheet.Cell("Y7").GetString()); // 21 Temmuz
            Assert.Equal(XLPaperSize.A4Paper, sheet.PageSetup.PaperSize);
            Assert.NotEmpty(sheet.PageSetup.PrintAreas);
            Assert.Equal(1, sheet.PageSetup.PagesWide);
            Assert.True(sheet.PageSetup.CenterHorizontally);
        }
        Assert.Equal(weeklyHash, Hash(weeklyTemplate));
        Assert.Equal(monthlyHash, Hash(monthlyTemplate));
    }

    [Fact]
    public void TemplateSearchIgnoresExcelLockFiles()
    {
        var root = FindProjectRoot();
        var templates = Path.Combine(root, "templates");
        var weeklyTemplate = WeeklyExcelExporter.FindWeeklyTemplate(templates);
        var monthlyTemplate = MonthlyExcelExporter.FindMonthlyTemplate(templates);
        var weeklyLockFile = Path.Combine(templates, $"~${Path.GetFileName(weeklyTemplate)}");
        var monthlyLockFile = Path.Combine(templates, $"~${Path.GetFileName(monthlyTemplate)}");
        File.WriteAllBytes(weeklyLockFile, [0]);
        File.WriteAllBytes(monthlyLockFile, [0]);
        try
        {
            Assert.Equal(weeklyTemplate, WeeklyExcelExporter.FindWeeklyTemplate(templates));
            Assert.Equal(monthlyTemplate, MonthlyExcelExporter.FindMonthlyTemplate(templates));
        }
        finally
        {
            File.Delete(weeklyLockFile);
            File.Delete(monthlyLockFile);
        }
    }

    [Fact]
    public void CorruptLogoFileDoesNotAbortExport()
    {
        var root = FindProjectRoot();
        var templates = Path.Combine(root, "templates");
        var weeklyTemplate = WeeklyExcelExporter.FindWeeklyTemplate(templates);
        var monthlyTemplate = MonthlyExcelExporter.FindMonthlyTemplate(templates);
        var directory = Path.Combine(Path.GetTempPath(), $"puantaj-badlogo-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var corruptLogo = Path.Combine(directory, "logo.png");
            File.WriteAllBytes(corruptLogo, [1, 2, 3, 4, 5]); // not a valid image
            var settings = AppSettings.CreateDefault("Test Otel", "Test Departman") with { LogoPath = corruptLogo, PrintLogo = true };
            var monday = new DateOnly(2026, 7, 20);
            var employees = Employees();
            var assignments = Assignments(monday);

            var weeklyOutput = Path.Combine(directory, "weekly.xlsx");
            var monthlyOutput = Path.Combine(directory, "monthly.xlsx");
            new WeeklyExcelExporter().Export(weeklyTemplate, weeklyOutput, "Test Otel", "Test Departman", monday, employees, assignments, settings: settings);
            new MonthlyExcelExporter().Export(monthlyTemplate, monthlyOutput, "Test Otel", "Test Departman", 2026, 7, employees, assignments, settings: settings);

            Assert.True(new FileInfo(weeklyOutput).Length > 0);
            Assert.True(new FileInfo(monthlyOutput).Length > 0);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void February_2025_output_has_no_days_29_30_or_31()
    {
        var root = FindProjectRoot();
        var template = MonthlyExcelExporter.FindMonthlyTemplate(Path.Combine(root, "templates"));
        var output = Path.Combine(Path.GetTempPath(), $"puantaj-feb-{Guid.NewGuid():N}.xlsx");
        try
        {
            new MonthlyExcelExporter().Export(template, output, "Test Otel", "Test", 2025, 2, Employees(), []);
            using var workbook = new XLWorkbook(output);
            var sheet = workbook.Worksheets.Single();
            Assert.True(sheet.Cell(6, 33).IsEmpty()); // AG = 29
            Assert.True(sheet.Cell(6, 34).IsEmpty()); // AH = 30
            Assert.True(sheet.Cell(6, 35).IsEmpty()); // AI = 31
        }
        finally
        {
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Fact]
    public void Weekly_range_is_always_monday_through_sunday()
    {
        var monday = Puantaj.Core.Calendar.CalendarHelper.StartOfWeek(new DateOnly(2026, 7, 23));
        var week = Puantaj.Core.Calendar.CalendarHelper.Week(monday);
        Assert.Equal(DayOfWeek.Monday, week[0].DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, week[6].DayOfWeek);
        Assert.Equal(6, week[6].DayNumber - week[0].DayNumber);
    }

    [Fact]
    public void Employment_ended_scenarios_blackout_without_exposing_internal_code()
    {
        var root = FindProjectRoot();
        var templates = Path.Combine(root, "templates");
        var monthlyTemplate = MonthlyExcelExporter.FindMonthlyTemplate(templates);
        var weeklyTemplate = WeeklyExcelExporter.FindWeeklyTemplate(templates);
        var definitions = new[]
        {
            new AssignmentCodeDefinition("Z9", "Gece Vardiyası", TimeSpan.FromHours(22), TimeSpan.FromHours(6), true, 1),
            new AssignmentCodeDefinition("F", "İşten Ayrıldı", null, null, false, 2)
        };
        foreach (var endDay in new int?[] { 1, 9, 31, null })
        {
            var persistent = endDay == 9;
            var output = persistent
                ? Path.Combine(root, "test-output", "Puantaj_Isten_Ayrilma_9.xlsx")
                : Path.Combine(Path.GetTempPath(), $"ended-{endDay?.ToString() ?? "none"}-{Guid.NewGuid():N}.xlsx");
            var assignments = new List<Assignment> { new(1, 1, new DateOnly(2026, 7, 1), "Z9", DateTimeOffset.UtcNow) };
            if (endDay is not null) assignments.Add(new(2, 1, new DateOnly(2026, 7, endDay.Value), "F", DateTimeOffset.UtcNow));
            try
            {
                new MonthlyExcelExporter().Export(monthlyTemplate, output, "Otel", "Departman", 2026, 7, Employees().Take(1).ToArray(), assignments, definitions);
                using var workbook = new XLWorkbook(output);
                var sheet = workbook.Worksheets.Single();
                for (var day = 1; day <= 31; day++)
                {
                    var cell = sheet.Cell(7, 4 + day);
                    if (endDay is not null && day >= endDay)
                    {
                        Assert.True(cell.IsEmpty());
                        Assert.Equal(System.Drawing.Color.Black.ToArgb(), cell.Style.Fill.BackgroundColor.Color.ToArgb());
                    }
                    Assert.DoesNotContain("F", cell.GetString());
                    Assert.DoesNotContain("İşten Ayrıldı", cell.GetString());
                }
                if (endDay is null) Assert.Equal("X", sheet.Cell("E7").GetString());
            }
            finally { if (!persistent && File.Exists(output)) File.Delete(output); }
        }

        var weeklyOutput = Path.Combine(root, "test-output", "Haftalik_Isten_Ayrilma.xlsx");
        try
        {
            var monday = new DateOnly(2026, 7, 20);
            new WeeklyExcelExporter().Export(weeklyTemplate, weeklyOutput, "Otel", "Departman", monday, Employees().Take(1).ToArray(),
                [new Assignment(1, 1, monday.AddDays(2), "F", DateTimeOffset.UtcNow)], definitions);
            using var workbook = new XLWorkbook(weeklyOutput);
            var sheet = workbook.Worksheets.Single();
            Assert.True(sheet.Cell("H9").IsEmpty());
            Assert.Equal(System.Drawing.Color.Black.ToArgb(), sheet.Cell("H9").Style.Fill.BackgroundColor.Color.ToArgb());
            Assert.DoesNotContain("F", sheet.CellsUsed().Select(cell => cell.GetString()));
        }
        finally { }
    }

    private static IReadOnlyList<Employee> Employees() => Enumerable.Range(1, 5)
        .Select(index => new Employee(index, $"Test Personel {index}", true, index, DateTimeOffset.UtcNow)).ToArray();

    private static IReadOnlyList<Assignment> Assignments(DateOnly monday)
    {
        var codes = new[] { "A", "B", "C", "D", "E", "HT", "Yİ", "RP", "G" };
        var result = new List<Assignment>();
        long id = 1;
        for (var employee = 1; employee <= 5; employee++)
        for (var day = 0; day < 7; day++)
        {
            var code = employee == 1 && day == 1 ? "HT" : codes[(employee + day - 1) % codes.Length];
            result.Add(new Assignment(id++, employee, monday.AddDays(day), code, DateTimeOffset.UtcNow));
        }
        return result;
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Puantaj.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Proje kökü bulunamadı.");
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
