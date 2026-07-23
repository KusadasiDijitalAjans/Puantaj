using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Puantaj.Core.Calendar;
using Puantaj.Core.Data;

namespace Puantaj.Core.Excel;

public sealed class WeeklyExcelExporter
{
    public const string ReferenceSheetName = "25.31.05.2026k";
    private const int FirstEmployeeRow = 9;
    private const int LastEmployeeRow = 25;
    private static readonly int[] DayColumns = [4, 6, 8, 10, 12, 14, 16];

    public string Export(
        string templatePath,
        string outputPath,
        string hotelName,
        string departmentName,
        DateOnly weekStart,
        IReadOnlyList<Employee> employees,
        IReadOnlyList<Assignment> assignments,
        IReadOnlyList<AssignmentCodeDefinition>? definitions = null,
        AppSettings? settings = null)
    {
        if (!File.Exists(templatePath)) throw new FileNotFoundException("Haftalık çalışma planı şablonu bulunamadı.", templatePath);
        if (employees.Count > LastEmployeeRow - FirstEmployeeRow + 1)
            throw new InvalidOperationException("Haftalık referans şablon en fazla 17 personel destekliyor.");
        var monday = CalendarHelper.StartOfWeek(weekStart);
        var week = CalendarHelper.Week(monday);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);

        using var workbook = new XLWorkbook(templatePath);
        if (!workbook.Worksheets.TryGetWorksheet(ReferenceSheetName, out var sheet))
            throw new InvalidOperationException($"Haftalık referans sayfa bulunamadı: {ReferenceSheetName}");
        foreach (var name in workbook.Worksheets.Select(item => item.Name).Where(name => name != ReferenceSheetName).ToArray())
            workbook.Worksheets.Delete(name);

        sheet.Name = $"{monday:yyyy-MM-dd}_{week[6]:yyyy-MM-dd}";
        sheet.Cell("C5").Value = hotelName;
        sheet.Cell("C6").Value = departmentName;
        sheet.Cell("R6").Value = $"{monday:dd.MM.yyyy} - {week[6]:dd.MM.yyyy}";
        for (var index = 0; index < 7; index++) sheet.Cell(6, DayColumns[index]).Value = week[index].ToDateTime(TimeOnly.MinValue);

        for (var row = FirstEmployeeRow; row <= LastEmployeeRow; row++)
        {
            sheet.Range(row, 1, row, 17).Clear(XLClearOptions.Contents);
        }

        var employeeRows = new Dictionary<long, int>();
        for (var index = 0; index < employees.Count; index++)
        {
            var row = FirstEmployeeRow + index;
            employeeRows[employees[index].Id] = row;
            sheet.Cell(row, 1).Value = index + 1;
            sheet.Cell(row, 2).Value = employees[index].FullName;
            for (var day = 0; day < 7; day++)
                if (!employees[index].IsEmployedOn(week[day]))
                {
                    var cell = sheet.Cell(row, DayColumns[day]);
                    cell.Clear(XLClearOptions.Contents);
                    cell.Style.Fill.PatternType = XLFillPatternValues.Solid;
                    cell.Style.Fill.BackgroundColor = XLColor.LightPink;
                    cell.Style.Fill.PatternColor = XLColor.LightPink;
                }
        }

        var resolver = new AssignmentCodeResolver(definitions ?? LegacyDefinitions());
        var ended = assignments.Where(a => resolver.Resolve(a.Code).IsEmploymentEnded)
            .GroupBy(a => a.EmployeeId).ToDictionary(g => g.Key, g => g.Min(a => a.WorkDate));
        foreach (var assignment in assignments)
        {
            var dayIndex = assignment.WorkDate.DayNumber - monday.DayNumber;
            if (dayIndex is < 0 or > 6 || !employeeRows.TryGetValue(assignment.EmployeeId, out var row)) continue;
            if (!employees.First(item => item.Id == assignment.EmployeeId).IsEmployedOn(assignment.WorkDate)) continue;
            if (ended.TryGetValue(assignment.EmployeeId, out var endDate) && assignment.WorkDate >= endDate) continue;
            sheet.Cell(row, DayColumns[dayIndex]).Value = resolver.Resolve(assignment.Code).Code;
        }
        foreach (var pair in ended.Where(pair => employeeRows.ContainsKey(pair.Key)))
            for (var day = Math.Max(0, pair.Value.DayNumber - monday.DayNumber); day < 7; day++)
            {
                var cell = sheet.Cell(employeeRows[pair.Key], DayColumns[day]);
                cell.Clear(XLClearOptions.Contents);
                cell.Style.Fill.PatternType = XLFillPatternValues.Solid;
                cell.Style.Fill.BackgroundColor = XLColor.Black;
                cell.Style.Fill.PatternColor = XLColor.Black;
            }

        ExcelBranding.ApplyWeekly(sheet, settings);
        ExcelPageSetup.ApplyA4(sheet, "A1:R36", XLPageOrientation.Landscape);
        workbook.SaveAs(outputPath);
        ExcelPageSetup.EnsureSavedA4(outputPath);
        return outputPath;
    }

    private static IReadOnlyList<AssignmentCodeDefinition> LegacyDefinitions() => AttendanceCodes.All
        .Select((code, index) => new AssignmentCodeDefinition(code, code, null, null,
            AttendanceCodes.WorkShifts.Contains(code, StringComparer.Ordinal), index)).ToArray();

    public static string FindWeeklyTemplate(string templatesDirectory)
    {
        if (!Directory.Exists(templatesDirectory))
            throw new DirectoryNotFoundException($"Şablon klasörü bulunamadı: {templatesDirectory}");
        var matches = Directory.EnumerateFiles(templatesDirectory, "*.xlsx")
            .Where(path => !Path.GetFileName(path).StartsWith("~$", StringComparison.Ordinal)
                && Path.GetFileName(path).Contains("HAFTALIK", StringComparison.OrdinalIgnoreCase)).ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidOperationException("templates klasöründe tek bir HAFTALIK şablon bulunmalıdır.");
    }

    public static string CreateOutputFileName(string departmentName, DateOnly weekStart)
    {
        var monday = CalendarHelper.StartOfWeek(weekStart);
        var safe = Regex.Replace(departmentName.Trim(), @"[^\p{L}\p{N}]+", "_").Trim('_');
        return $"Haftalik_Calisma_Plani_{safe}_{monday:yyyy-MM-dd}_{monday.AddDays(6):yyyy-MM-dd}.xlsx";
    }
}
