using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Puantaj.Core.Calendar;
using Puantaj.Core.Data;
using Puantaj.Core.Planning;

namespace Puantaj.Core.Excel;

public sealed class WeeklyExcelExporter
{
    public const int FirstEmployeeRow = 7;
    public const int LastEmployeeRow = 25;
    public const int EmployeesPerPage = LastEmployeeRow - FirstEmployeeRow + 1;
    private const string PreferredTemplateName = "Haftalik-Calisma_Plani_10-16_Agustos.xlsx";
    private static readonly int[] WorkTimeColumns = [4, 6, 8, 10, 12, 14, 16];
    private static readonly int[] SignatureColumns = [5, 7, 9, 11, 13, 15, 17];

    public string Export(string templatePath, string outputPath, string hotelName, string departmentName,
        DateOnly weekStart, IReadOnlyList<Employee> employees, IReadOnlyList<Assignment> assignments,
        IReadOnlyList<AssignmentCodeDefinition>? definitions = null, AppSettings? settings = null)
    {
        if (!File.Exists(templatePath)) throw new FileNotFoundException("Haftalık puantaj şablonu bulunamadı.", templatePath);
        var monday = CalendarHelper.StartOfWeek(weekStart);
        var sunday = monday.AddDays(6);
        var allDefinitions = definitions ?? LegacyDefinitions();
        var resolver = new AssignmentCodeResolver(allDefinitions);
        var ended = assignments.Where(item => resolver.Resolve(item.Code).IsEmploymentEnded)
            .GroupBy(item => item.EmployeeId).ToDictionary(group => group.Key, group => group.Min(item => item.WorkDate));
        var visible = employees.Where(employee => !ShiftBasedEmployeeFilter.IsShiftBasedEmployee(employee)
                && IntersectsWeek(employee, monday, sunday, assignments, ended))
            .OrderBy(employee => employee.DisplayOrder).ThenBy(employee => employee.FullName).ToArray();

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        using var workbook = OpenTemplate(templatePath);
        var template = SelectTemplateSheet(workbook);
        foreach (var sheet in workbook.Worksheets.Where(sheet => sheet != template).ToArray()) sheet.Delete();
        var pageCount = Math.Max(1, (int)Math.Ceiling((double)visible.Length / EmployeesPerPage));
        var pages = new List<IXLWorksheet> { template };
        for (var page = 2; page <= pageCount; page++) pages.Add(template.CopyTo($"Hafta {page}"));

        for (var page = 0; page < pageCount; page++)
        {
            var pageEmployees = visible.Skip(page * EmployeesPerPage).Take(EmployeesPerPage).ToArray();
            FillPage(pages[page], pageEmployees, assignments, allDefinitions, settings, hotelName,
                departmentName, monday, page * EmployeesPerPage, ended);
            pages[page].Name = pageCount == 1 ? "Haftalık Puantaj" : $"Haftalık Puantaj {page + 1}";
        }

        workbook.SaveAs(outputPath, new SaveOptions { GenerateCalculationChain = false });
        ExcelPageSetup.EnsureSavedA4(outputPath);
        return outputPath;
    }

    private static XLWorkbook OpenTemplate(string path)
    {
        using var source = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var memory = new MemoryStream();
        source.CopyTo(memory); memory.Position = 0;
        return new XLWorkbook(memory);
    }

    private static IXLWorksheet SelectTemplateSheet(XLWorkbook workbook) => workbook.Worksheets.Count == 1
        ? workbook.Worksheet(1)
        : workbook.Worksheets.FirstOrDefault(sheet => sheet.Name == "25.31.05.2026k") ?? workbook.Worksheet(1);

    private static bool IntersectsWeek(Employee employee, DateOnly monday, DateOnly sunday,
        IReadOnlyList<Assignment> assignments, IReadOnlyDictionary<long, DateOnly> ended)
    {
        if (employee.HireDate is { } hire && hire > sunday) return false;
        if (ended.TryGetValue(employee.Id, out var end)) return end >= monday;
        return employee.IsActive || assignments.Any(item => item.EmployeeId == employee.Id && item.WorkDate >= monday && item.WorkDate <= sunday);
    }

    private static void FillPage(IXLWorksheet sheet, IReadOnlyList<Employee> employees,
        IReadOnlyList<Assignment> assignments, IReadOnlyList<AssignmentCodeDefinition> definitions,
        AppSettings? settings, string hotelName, string departmentName, DateOnly monday, int offset,
        IReadOnlyDictionary<long, DateOnly> ended)
    {
        var week = CalendarHelper.Week(monday);
        sheet.Cell("C3").Value = hotelName;
        sheet.Cell("C4").Value = departmentName;
        sheet.Cell("R4").Value = $"{monday:dd.MM.yyyy} - {week[6]:dd.MM.yyyy}";

        for (var row = FirstEmployeeRow; row <= LastEmployeeRow; row++)
        {
            sheet.Range(row, 1, row, 17).Clear(XLClearOptions.Contents);
            for (var day = 0; day < 7; day++) AttendanceExcelStyle.Clear(sheet.Cell(row, WorkTimeColumns[day]));
        }

        var rows = new Dictionary<long, int>();
        for (var index = 0; index < employees.Count; index++)
        {
            var employee = employees[index]; var row = FirstEmployeeRow + index;
            rows[employee.Id] = row;
            sheet.Cell(row, 1).Value = offset + index + 1;
            sheet.Cell(row, 2).Value = employee.FullName;
            sheet.Cell(row, 3).Value = employee.Position;
            foreach (var signatureColumn in SignatureColumns) sheet.Cell(row, signatureColumn).Clear(XLClearOptions.Contents);
            for (var day = 0; day < 7; day++)
                if (!employee.IsEmployedOn(week[day])) AttendanceExcelStyle.BeforeHire(sheet.Cell(row, WorkTimeColumns[day]));
        }

        var resolver = new AssignmentCodeResolver(definitions);
        foreach (var assignment in assignments)
        {
            var day = assignment.WorkDate.DayNumber - monday.DayNumber;
            if (day is < 0 or > 6 || !rows.TryGetValue(assignment.EmployeeId, out var row)) continue;
            var employee = employees.First(item => item.Id == assignment.EmployeeId);
            var definition = resolver.Resolve(assignment.Code);
            if (!employee.IsEmployedOn(assignment.WorkDate) || definition.IsEmploymentEnded ||
                ended.TryGetValue(employee.Id, out var end) && assignment.WorkDate >= end) continue;
            var cell = sheet.Cell(row, WorkTimeColumns[day]); cell.Value = definition.Code;
            AttendanceExcelStyle.ApplyCode(cell, definition);
        }
        foreach (var pair in ended.Where(pair => rows.ContainsKey(pair.Key)))
            for (var day = Math.Max(0, pair.Value.DayNumber - monday.DayNumber); day < 7; day++)
                AttendanceExcelStyle.Blackout(sheet.Cell(rows[pair.Key], WorkTimeColumns[day]));

        ExcelPageSetup.ApplyA4(sheet, "A1:S36", XLPageOrientation.Landscape);
    }

    private static IReadOnlyList<AssignmentCodeDefinition> LegacyDefinitions() => AttendanceCodes.All
        .Select((code, index) => new AssignmentCodeDefinition(code, code, null, null,
            AttendanceCodes.WorkShifts.Contains(code, StringComparer.Ordinal), index)).ToArray();

    public static string FindWeeklyTemplate(string templatesDirectory)
    {
        if (!Directory.Exists(templatesDirectory)) throw new DirectoryNotFoundException($"Şablon klasörü bulunamadı: {templatesDirectory}");
        var preferred = Path.Combine(templatesDirectory, PreferredTemplateName);
        if (File.Exists(preferred)) return preferred;
        var matches = Directory.EnumerateFiles(templatesDirectory, "*.xlsx")
            .Where(path => !Path.GetFileName(path).StartsWith("~$", StringComparison.Ordinal)
                && Path.GetFileName(path).Contains("HAFTALIK", StringComparison.OrdinalIgnoreCase)).ToArray();
        return matches.Length == 1 ? matches[0] : throw new InvalidOperationException("Haftalık puantaj şablonu bulunamadı.");
    }

    public static string CreateOutputFileName(DateOnly weekStart, string extension = ".xlsx")
    {
        var monday = CalendarHelper.StartOfWeek(weekStart); var sunday = monday.AddDays(6);
        var start = monday.Month == sunday.Month ? $"{monday:dd}" : $"{monday:dd}_{AsciiMonth(monday.Month)}";
        var end = $"{sunday:dd}_{AsciiMonth(sunday.Month)}";
        var year = monday.Year == sunday.Year ? monday.Year.ToString(CultureInfo.InvariantCulture) : $"{monday.Year}-{sunday.Year}";
        var safeExtension = extension.StartsWith('.') ? extension : $".{extension}";
        return $"Haftalik_Puantaj_{start}-{end}_{year}{safeExtension}";
    }

    public static string CreateOutputFileName(string departmentName, DateOnly weekStart)
    {
        var monday = CalendarHelper.StartOfWeek(weekStart);
        var safe = Regex.Replace(departmentName.Trim(), @"[^\p{L}\p{N}]+", "_").Trim('_');
        return $"Haftalik_Calisma_Plani_{safe}_{monday:yyyy-MM-dd}_{monday.AddDays(6):yyyy-MM-dd}.xlsx";
    }

    public static string FormatWeekRange(DateOnly weekStart)
    {
        var monday = CalendarHelper.StartOfWeek(weekStart); var sunday = monday.AddDays(6);
        var culture = CultureInfo.GetCultureInfo("tr-TR");
        return monday.Month == sunday.Month
            ? $"{monday:dd}–{sunday:dd} {culture.DateTimeFormat.GetMonthName(monday.Month)}"
            : $"{monday:dd} {culture.DateTimeFormat.GetMonthName(monday.Month)} – {sunday:dd} {culture.DateTimeFormat.GetMonthName(sunday.Month)}";
    }

    private static string AsciiMonth(int month)
    {
        var value = CultureInfo.GetCultureInfo("tr-TR").DateTimeFormat.GetMonthName(month);
        var replacements = new Dictionary<char, char> { ['ç']='c', ['ğ']='g', ['ı']='i', ['ö']='o', ['ş']='s', ['ü']='u', ['İ']='I' };
        var builder = new StringBuilder(value.Length);
        foreach (var character in value) builder.Append(replacements.GetValueOrDefault(character, character));
        var title = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(builder.ToString().ToLowerInvariant());
        return Regex.Replace(title, @"[^A-Za-z0-9]", string.Empty);
    }
}
