using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Puantaj.Core.Data;

namespace Puantaj.Core.Excel;

public sealed class MonthlyExcelExporter
{
    private const int FirstEmployeeRow = 7;
    private const int LastEmployeeRow = 38;
    private const int FirstDayColumn = 5; // E

    public string Export(
        string templatePath,
        string outputPath,
        string hotelName,
        string departmentName,
        int year,
        int month,
        IReadOnlyList<Employee> employees,
        IReadOnlyList<Assignment> assignments,
        IReadOnlyList<AssignmentCodeDefinition>? definitions = null,
        AppSettings? settings = null)
    {
        if (!File.Exists(templatePath)) throw new FileNotFoundException("Aylık puantaj şablonu bulunamadı.", templatePath);
        if (employees.Count > LastEmployeeRow - FirstEmployeeRow + 1)
            throw new InvalidOperationException("Aylık şablon en fazla 32 personel destekliyor.");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        using var workbook = new XLWorkbook(templatePath);
        var sheet = workbook.Worksheets.Single();
        var culture = CultureInfo.GetCultureInfo("tr-TR");
        var monthName = culture.DateTimeFormat.GetMonthName(month).ToUpper(culture);
        sheet.Name = $"{monthName} {year} PUANTAJ";
        sheet.Cell("E5").Value = $"{monthName}   {year}   PUANTAJ";
        sheet.Cell("AP2").Value = departmentName;
        sheet.Cell("AP3").Value = hotelName;

        var days = DateTime.DaysInMonth(year, month);
        for (var day = 1; day <= 31; day++)
        {
            var column = FirstDayColumn + day - 1;
            if (day <= days) sheet.Cell(6, column).Value = day;
            else sheet.Cell(6, column).Clear(XLClearOptions.Contents);
            for (var row = FirstEmployeeRow; row <= LastEmployeeRow; row++)
                sheet.Cell(row, column).Clear(XLClearOptions.Contents);
        }

        for (var row = FirstEmployeeRow; row <= LastEmployeeRow; row++)
        {
            sheet.Cell(row, 2).Clear(XLClearOptions.Contents);
            sheet.Cell(row, 3).Clear(XLClearOptions.Contents);
            sheet.Cell(row, 4).Clear(XLClearOptions.Contents);
            sheet.Range(row, 36, row, 45).Clear(XLClearOptions.Contents);
        }

        var employeeRows = new Dictionary<long, int>();
        for (var index = 0; index < employees.Count; index++)
        {
            var row = FirstEmployeeRow + index;
            var employee = employees[index];
            employeeRows[employee.Id] = row;
            sheet.Cell(row, 2).Value = index + 1;
            sheet.Cell(row, 3).Value = employee.FullName;
            sheet.Cell(row, 4).Value = employee.Position;
            WriteEmployeeFormulas(sheet, row);
        }

        var allDefinitions = definitions ?? LegacyDefinitions();
        var resolver = new AssignmentCodeResolver(allDefinitions);
        var mapper = new AttendanceExcelCodeMapper(allDefinitions);
        var ended = assignments.Where(a => resolver.Resolve(a.Code).IsEmploymentEnded)
            .GroupBy(a => a.EmployeeId).ToDictionary(g => g.Key, g => g.Min(a => a.WorkDate));
        foreach (var assignment in assignments)
        {
            if (assignment.WorkDate.Year != year || assignment.WorkDate.Month != month ||
                !employeeRows.TryGetValue(assignment.EmployeeId, out var row)) continue;
            if (ended.TryGetValue(assignment.EmployeeId, out var endDate) && assignment.WorkDate >= endDate) continue;
            var cell = sheet.Cell(row, FirstDayColumn + assignment.WorkDate.Day - 1);
            var value = mapper.Map(assignment.Code); cell.Value = value;
            if (value == "HT") cell.Style.Fill.BackgroundColor = XLColor.Yellow;
        }
        foreach (var pair in ended.Where(pair => pair.Value.Year == year && pair.Value.Month == month && employeeRows.ContainsKey(pair.Key)))
            for (var day = pair.Value.Day; day <= days; day++) Blackout(sheet.Cell(employeeRows[pair.Key], FirstDayColumn + day - 1));

        WriteSummaryHeaders(sheet);
        WriteDailySummaryFormulas(sheet, days);
        ExcelBranding.ApplyMonthly(sheet, settings);
        ExcelPageSetup.ApplyA4(sheet, "B2:AT53", XLPageOrientation.Landscape);
        workbook.SaveAs(outputPath, new SaveOptions
        {
            GenerateCalculationChain = false,
            ConsolidateConditionalFormatRanges = false,
            ConsolidateDataValidationRanges = false
        });
        ExcelPageSetup.EnsureSavedA4(outputPath);
        return outputPath;
    }

    private static void Blackout(IXLCell cell)
    {
        cell.Clear(XLClearOptions.Contents);
        cell.Style.Fill.PatternType = XLFillPatternValues.Solid;
        cell.Style.Fill.BackgroundColor = XLColor.Black;
        cell.Style.Fill.PatternColor = XLColor.Black;
    }

    private static IReadOnlyList<AssignmentCodeDefinition> LegacyDefinitions() => AttendanceCodes.All
        .Select((code, index) => new AssignmentCodeDefinition(code, code, null, null,
            AttendanceCodes.WorkShifts.Contains(code, StringComparer.Ordinal), index)).ToArray();

    public static string FindMonthlyTemplate(string templatesDirectory)
    {
        if (!Directory.Exists(templatesDirectory))
            throw new DirectoryNotFoundException($"Şablon klasörü bulunamadı: {templatesDirectory}");
        var matches = Directory.EnumerateFiles(templatesDirectory, "*.xlsx")
            .Where(path => !Path.GetFileName(path).StartsWith("~$", StringComparison.Ordinal)
                && Path.GetFileName(path).Contains("PUANTAJ", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidOperationException("templates klasöründe tek bir aylık PUANTAJ şablonu bulunmalıdır.");
    }

    public static string CreateOutputFileName(string departmentName, int year, int month)
    {
        var safeDepartment = Regex.Replace(departmentName.Trim(), @"[^\p{L}\p{N}]+", "_").Trim('_');
        return $"Puantaj_{safeDepartment}_{year}_{month:00}.xlsx";
    }

    private static void WriteEmployeeFormulas(IXLWorksheet sheet, int row)
    {
        sheet.Cell(row, 36).FormulaA1 = $"COUNTIF(E{row}:AI{row},\"X\")";
        var totals = new[] { (37, "HT"), (38, "RT"), (39, "Mİ"), (40, "Üİ"), (41, "RP"), (42, "Yİ"), (43, "ÜZ"), (44, "DZ"), (45, "GR") };
        foreach (var (column, code) in totals)
            sheet.Cell(row, column).FormulaA1 = $"COUNTIF(E{row}:AI{row},\"{code}\")";
    }

    private static void WriteSummaryHeaders(IXLWorksheet sheet)
    {
        sheet.Cell("AJ6").Value = "X";
        sheet.Cell("AK6").Value = "HT";
        sheet.Cell("AL6").Value = "RT";
        sheet.Cell("AM6").Value = "Mİ";
        sheet.Cell("AN6").Value = "Üİ";
        sheet.Cell("AO6").Value = "RP";
        sheet.Cell("AP6").Value = "Yİ";
        sheet.Cell("AQ6").Value = "ÜZ";
        sheet.Cell("AR6").Value = "DZ";
        sheet.Cell("AS6").Value = "GR";

        var labels = new[]
        {
            (41, "X-ÇALIŞAN"), (42, "HT-HAFTA TATİLİ"), (43, "RT-RESMİ TATİL"),
            (44, "Mİ-MAZERET İZNİ"), (45, "Üİ-ÜCRETLİ İZİN"), (46, "RP-RAPOR"),
            (47, "Yİ-YILLIK İZİN"), (48, "ÜZ-ÜCRETSİZ İZİN"), (49, "DZ-DEVAMSIZLIK"), (50, "GR-GÖREVLİ")
        };
        foreach (var (row, label) in labels) sheet.Cell(row, 4).Value = label;
    }

    private static void WriteDailySummaryFormulas(IXLWorksheet sheet, int days)
    {
        var summary = new[] { (41, "X"), (42, "HT"), (43, "RT"), (44, "Mİ"), (45, "Üİ"), (46, "RP"), (47, "Yİ"), (48, "ÜZ"), (49, "DZ"), (50, "GR") };
        for (var day = 1; day <= 31; day++)
        {
            var column = FirstDayColumn + day - 1;
            var letter = XLHelper.GetColumnLetterFromNumber(column);
            for (var row = 41; row <= 50; row++) sheet.Cell(row, column).Clear(XLClearOptions.Contents);
            sheet.Cell(40, column).Clear(XLClearOptions.Contents);
            if (day > days) continue;
            foreach (var (row, code) in summary)
                sheet.Cell(row, column).FormulaA1 = $"COUNTIF({letter}$7:{letter}$38,\"{code}\")";
            sheet.Cell(40, column).FormulaA1 = $"SUM({letter}41:{letter}50)";
        }
        sheet.Cell("AJ40").FormulaA1 = "SUM(AJ7:AJ38)";
        for (var column = 37; column <= 45; column++)
        {
            var letter = XLHelper.GetColumnLetterFromNumber(column);
            sheet.Cell(40, column).FormulaA1 = $"SUM({letter}7:{letter}38)";
        }
    }
}
