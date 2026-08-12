using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Puantaj.Core.Calendar;
using Puantaj.Core.Data;
using Puantaj.Core.Planning;

namespace Puantaj.Core.Excel;

public sealed class ShiftBasedWeeklyExcelExporter
{
    public const int FirstDataRow = 7;
    public const int LastDataRow = 25;
    public const int AvailableRowsPerPage = LastDataRow - FirstDataRow + 1;
    private const string TemplateName = "Vardiyali-Calisma_Plani_10-16_Agustos.xlsx";
    private static readonly int[] WorkColumns = [4, 6, 8, 10, 12, 14, 16];
    private static readonly int[] SignatureColumns = [5, 7, 9, 11, 13, 15, 17];

    public string Export(string templatePath, string outputPath, string hotelName, string departmentName,
        DateOnly weekStart, IReadOnlyList<Employee> employees, IReadOnlyList<Assignment> assignments,
        IReadOnlyList<AssignmentCodeDefinition> definitions, AppSettings? settings = null,
        IReadOnlyList<AssignmentCodeDefinition>? activeDefinitions = null)
    {
        if (!File.Exists(templatePath)) throw new FileNotFoundException("Vardiyalı çalışma planı şablonu bulunamadı.", templatePath);
        var monday = CalendarHelper.StartOfWeek(weekStart); var sunday = monday.AddDays(6);
        var resolver = new AssignmentCodeResolver(definitions);
        var ended = assignments.Where(item => resolver.Resolve(item.Code).IsEmploymentEnded)
            .GroupBy(item => item.EmployeeId).ToDictionary(group => group.Key, group => group.Min(item => item.WorkDate));
        var visible = employees.Where(employee => ShiftBasedEmployeeFilter.IsShiftBasedEmployee(employee)
                && IntersectsWeek(employee, monday, sunday, assignments, ended))
            .OrderBy(employee => employee.DisplayOrder).ThenBy(employee => employee.FullName).ToArray();
        var groups = BuildGroups(visible, assignments, definitions, monday, sunday);
        var pages = Paginate(groups);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        using var source = File.Open(templatePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var memory = new MemoryStream(); source.CopyTo(memory); memory.Position = 0;
        using var workbook = new XLWorkbook(memory);
        var template = workbook.Worksheet(1);
        foreach (var extra in workbook.Worksheets.Skip(1).ToArray()) extra.Delete();
        var sheets = new List<IXLWorksheet> { template };
        for (var page = 2; page <= pages.Count; page++) sheets.Add(template.CopyTo($"Vardiyalı Plan {page}"));
        for (var page = 0; page < pages.Count; page++)
        {
            FillPage(sheets[page], pages[page], assignments, definitions, settings, hotelName, departmentName,
                monday, ended, page * AvailableRowsPerPage, activeDefinitions ?? definitions);
            sheets[page].Name = pages.Count == 1 ? "Vardiyalı Çalışma Planı" : $"Vardiyalı Plan {page + 1}";
        }
        workbook.SaveAs(outputPath, new SaveOptions { GenerateCalculationChain = false });
        ExcelPageSetup.EnsureSavedA4(outputPath);
        return outputPath;
    }

    public static IReadOnlyList<ShiftGroup> BuildGroups(IReadOnlyList<Employee> employees,
        IReadOnlyList<Assignment> assignments, IReadOnlyList<AssignmentCodeDefinition> definitions,
        DateOnly monday, DateOnly sunday)
    {
        var shifts = definitions.Where(item => item.IsWorkShift).OrderBy(item => item.DisplayOrder).ToArray();
        var shiftMap = shifts.ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
        var grouped = employees.GroupBy(employee =>
        {
            var dominant = assignments.Where(item => item.EmployeeId == employee.Id && item.WorkDate >= monday && item.WorkDate <= sunday)
                .Where(item => shiftMap.ContainsKey(item.Code)).GroupBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count()).ThenBy(group => shiftMap[group.Key].DisplayOrder).FirstOrDefault()?.Key;
            return dominant is null ? null : shiftMap[dominant];
        }, ShiftDefinitionComparer.Instance).OrderBy(group => group.Key?.DisplayOrder ?? int.MaxValue)
            .Select(group => new ShiftGroup(group.Key, group.OrderBy(item => item.DisplayOrder).ThenBy(item => item.FullName).ToArray())).ToArray();
        return grouped;
    }

    public static IReadOnlyList<IReadOnlyList<ShiftGroup>> Paginate(IReadOnlyList<ShiftGroup> groups)
    {
        var pages = new List<IReadOnlyList<ShiftGroup>>(); var current = new List<ShiftGroup>(); var used = 0;
        foreach (var group in groups)
        {
            var remaining = group.Employees.ToList();
            while (remaining.Count > 0)
            {
                var needed = remaining.Count + 1;
                if (used > 0 && needed <= AvailableRowsPerPage && used + needed > AvailableRowsPerPage)
                { pages.Add(current.ToArray()); current = []; used = 0; }
                var take = Math.Min(remaining.Count, AvailableRowsPerPage - used - 1);
                if (take <= 0) { pages.Add(current.ToArray()); current = []; used = 0; continue; }
                current.Add(new ShiftGroup(group.Definition, remaining.Take(take).ToArray(), remaining.Count != group.Employees.Count));
                remaining.RemoveRange(0, take); used += take + 1;
                if (remaining.Count > 0) { pages.Add(current.ToArray()); current = []; used = 0; }
            }
        }
        if (current.Count > 0 || pages.Count == 0) pages.Add(current.ToArray());
        return pages;
    }

    private static void FillPage(IXLWorksheet sheet, IReadOnlyList<ShiftGroup> groups,
        IReadOnlyList<Assignment> assignments, IReadOnlyList<AssignmentCodeDefinition> definitions, AppSettings? settings,
        string hotelName, string departmentName, DateOnly monday, IReadOnlyDictionary<long, DateOnly> ended, int offset,
        IReadOnlyList<AssignmentCodeDefinition> activeDefinitions)
    {
        sheet.Cell("C3").Value = hotelName; sheet.Cell("C4").Value = departmentName;
        sheet.Cell("R4").Value = $"{monday:dd.MM.yyyy} - {monday.AddDays(6):dd.MM.yyyy}";
        var footerStart = FindFooterStart(sheet);
        var usedDataRows = groups.Sum(group => group.Employees.Count + 1);
        var desiredFooterStart = FirstDataRow + usedDataRows + 1;
        footerStart = MoveFooter(sheet, footerStart, desiredFooterStart);
        for (var row = FirstDataRow; row < footerStart; row++)
        {
            sheet.Range(row, 1, row, 17).Clear(XLClearOptions.Contents);
            for (var day = 0; day < 7; day++) AttendanceExcelStyle.Clear(sheet.Cell(row, WorkColumns[day]));
        }
        var resolver = new AssignmentCodeResolver(definitions); var rowNumber = FirstDataRow; var sequence = offset;
        foreach (var group in groups)
        {
            WriteGroupHeader(sheet, rowNumber++, GroupTitle(group));
            foreach (var employee in group.Employees)
            {
                var row = rowNumber++; sheet.Cell(row, 1).Value = ++sequence; sheet.Cell(row, 2).Value = employee.FullName;
                sheet.Cell(row, 3).Value = employee.Position;
                foreach (var column in SignatureColumns) sheet.Cell(row, column).Clear(XLClearOptions.Contents);
                for (var day = 0; day < 7; day++) if (!employee.IsEmployedOn(monday.AddDays(day))) AttendanceExcelStyle.BeforeHire(sheet.Cell(row, WorkColumns[day]));
                foreach (var assignment in assignments.Where(item => item.EmployeeId == employee.Id))
                {
                    var day = assignment.WorkDate.DayNumber - monday.DayNumber;
                    if (day is < 0 or > 6 || !employee.IsEmployedOn(assignment.WorkDate)) continue;
                    var definition = resolver.Resolve(assignment.Code);
                    if (definition.IsEmploymentEnded || ended.TryGetValue(employee.Id, out var end) && assignment.WorkDate >= end) continue;
                    var cell = sheet.Cell(row, WorkColumns[day]); cell.Value = definition.Code; AttendanceExcelStyle.ApplyCode(cell, definition);
                }
                if (ended.TryGetValue(employee.Id, out var endDate))
                    for (var day = Math.Max(0, endDate.DayNumber - monday.DayNumber); day < 7; day++) AttendanceExcelStyle.Blackout(sheet.Cell(row, WorkColumns[day]));
            }
        }
        WriteShiftLegend(sheet, footerStart, activeDefinitions);
        var footerEnd = sheet.LastRowUsed()?.RowNumber() ?? footerStart;
        ExcelPageSetup.ApplyA4(sheet, $"A1:S{footerEnd}", XLPageOrientation.Landscape);
    }

    private static int FindFooterStart(IXLWorksheet sheet)
    {
        var marker = sheet.Column(3).CellsUsed().FirstOrDefault(cell =>
            cell.GetString().TrimStart().StartsWith("HT :", StringComparison.OrdinalIgnoreCase));
        return marker?.Address.RowNumber ?? throw new InvalidDataException("Vardiya açıklama alanı şablonda bulunamadı.");
    }

    private static int MoveFooter(IXLWorksheet sheet, int currentStart, int desiredStart)
    {
        if (desiredStart < currentStart)
            sheet.Rows(desiredStart, currentStart - 1).Delete();
        else if (desiredStart > currentStart)
            sheet.Row(currentStart).InsertRowsAbove(desiredStart - currentStart);
        return desiredStart;
    }

    private static void WriteShiftLegend(IXLWorksheet sheet, int footerStart,
        IReadOnlyList<AssignmentCodeDefinition> activeDefinitions)
    {
        var shifts = activeDefinitions.Where(item => item.IsWorkShift)
            .OrderBy(item => item.DisplayOrder).ThenBy(item => item.Code, StringComparer.OrdinalIgnoreCase).ToArray();
        var noteRow = sheet.Column(1).CellsUsed().FirstOrDefault(cell =>
            cell.GetString().StartsWith("Yukarıdaki çalışma saatleri", StringComparison.OrdinalIgnoreCase))?.Address.RowNumber
            ?? footerStart + Math.Max(7, shifts.Length + 1);
        if (footerStart + shifts.Length >= noteRow)
        {
            var extraRows = footerStart + shifts.Length - noteRow + 1;
            sheet.Row(noteRow).InsertRowsAbove(extraRows);
            noteRow += extraRows;
        }
        var codeStyle = sheet.Cell(footerStart, 1).Style;
        var hoursStyle = sheet.Cell(footerStart, 2).Style;
        for (var row = footerStart; row < noteRow; row++)
        {
            sheet.Cell(row, 1).Clear(XLClearOptions.Contents);
            sheet.Cell(row, 2).Clear(XLClearOptions.Contents);
        }
        for (var index = 0; index < shifts.Length; index++)
        {
            var definition = shifts[index]; var row = footerStart + index;
            sheet.Cell(row, 1).Style = codeStyle;
            sheet.Cell(row, 2).Style = hoursStyle;
            sheet.Cell(row, 1).Value = definition.Code;
            sheet.Cell(row, 2).Value = FormatLegendHours(definition);
        }
    }

    private static string FormatLegendHours(AssignmentCodeDefinition definition) =>
        definition.StartTime is { } start && definition.EndTime is { } end
            ? $"{start:hh\\.mm} / {end:hh\\.mm}"
            : string.Empty;

    private static void WriteGroupHeader(IXLWorksheet sheet, int row, string title)
    {
        sheet.Range(row, 1, row, 19).Clear(XLClearOptions.Contents);
        sheet.Range(row, 2, row, 19).Style.Fill.PatternType = XLFillPatternValues.Solid;
        sheet.Range(row, 2, row, 19).Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");
        sheet.Range(row, 2, row, 19).Style.Font.FontColor = XLColor.White;
        sheet.Range(row, 2, row, 19).Style.Font.Bold = true;
        sheet.Range(row, 2, row, 19).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        sheet.Cell(row, 2).Value = title;
    }

    private static string GroupTitle(ShiftGroup group)
    {
        if (group.Definition is null) return group.IsContinuation ? "DİĞER (DEVAM)" : "DİĞER";
        var shift = group.Definition;
        var hours = shift.StartTime is not null && shift.EndTime is not null ? $"{shift.StartTime:hh\\/mm}-{shift.EndTime:hh\\/mm}" : shift.Code;
        return $"{hours} VARDİYASI{(group.IsContinuation ? " (DEVAM)" : string.Empty)}";
    }

    private static bool IntersectsWeek(Employee employee, DateOnly monday, DateOnly sunday,
        IReadOnlyList<Assignment> assignments, IReadOnlyDictionary<long, DateOnly> ended)
    {
        if (employee.HireDate is { } hire && hire > sunday) return false;
        if (ended.TryGetValue(employee.Id, out var end)) return end >= monday;
        return employee.IsActive || assignments.Any(item => item.EmployeeId == employee.Id && item.WorkDate >= monday && item.WorkDate <= sunday);
    }

    public static string FindTemplate(string templatesDirectory)
    {
        var path = Path.Combine(templatesDirectory, TemplateName);
        return File.Exists(path) ? path : throw new FileNotFoundException("Vardiyalı çalışma planı şablonu bulunamadı.", path);
    }

    public static string CreateOutputFileName(DateOnly weekStart, string extension = ".xlsx")
    {
        var monday = CalendarHelper.StartOfWeek(weekStart); var sunday = monday.AddDays(6);
        var start = monday.Month == sunday.Month ? $"{monday:dd}" : $"{monday:dd}_{Month(monday.Month)}";
        var end = $"{sunday:dd}_{Month(sunday.Month)}"; var suffix = extension.StartsWith('.') ? extension : $".{extension}";
        return $"Vardiyali_Calisma_Plani_{start}-{end}_{monday.Year}{suffix}";
    }

    private static string Month(int month)
    {
        var value = CultureInfo.GetCultureInfo("tr-TR").DateTimeFormat.GetMonthName(month);
        var map = new Dictionary<char, char> { ['ç']='c', ['ğ']='g', ['ı']='i', ['ö']='o', ['ş']='s', ['ü']='u', ['İ']='I' };
        var builder = new StringBuilder(); foreach (var character in value) builder.Append(map.GetValueOrDefault(character, character));
        return Regex.Replace(CultureInfo.InvariantCulture.TextInfo.ToTitleCase(builder.ToString().ToLowerInvariant()), "[^A-Za-z0-9]", string.Empty);
    }

    public sealed record ShiftGroup(AssignmentCodeDefinition? Definition, IReadOnlyList<Employee> Employees, bool IsContinuation = false);
    private sealed class ShiftDefinitionComparer : IEqualityComparer<AssignmentCodeDefinition?>
    {
        public static ShiftDefinitionComparer Instance { get; } = new();
        public bool Equals(AssignmentCodeDefinition? x, AssignmentCodeDefinition? y) => string.Equals(x?.Code, y?.Code, StringComparison.OrdinalIgnoreCase);
        public int GetHashCode(AssignmentCodeDefinition? value) => value?.Code is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(value.Code);
    }
}
