using ClosedXML.Excel;
using Puantaj.Core.Calendar;

namespace Puantaj.Core.Excel;

internal static class WeeklyPlanDateWriter
{
    private static readonly int[] DateColumns = [4, 6, 8, 10, 12, 14, 16];

    public static void Write(IXLWorksheet sheet, DateOnly weekStart)
    {
        var monday = CalendarHelper.StartOfWeek(weekStart);
        for (var day = 0; day < DateColumns.Length; day++)
            sheet.Cell(4, DateColumns[day]).Value = monday.AddDays(day).ToDateTime(TimeOnly.MinValue);

        sheet.Cell("R4").Value = $"{monday:dd.MM.yyyy} - {monday.AddDays(6):dd.MM.yyyy}";
    }
}
