using ClosedXML.Excel;
using Puantaj.Core.Data;

namespace Puantaj.Core.Excel;

internal static class AttendanceExcelStyle
{
    public static void Clear(IXLCell cell)
    {
        cell.Style.Fill.PatternType = XLFillPatternValues.None;
        cell.Style.Fill.BackgroundColor = XLColor.NoColor;
        cell.Style.Fill.PatternColor = XLColor.NoColor;
        cell.Style.Font.FontColor = XLColor.Black;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    public static void ApplyCode(IXLCell cell, AssignmentCodeDefinition definition)
    {
        Clear(cell);
        if (!definition.IsWorkShift && !definition.IsEmploymentEnded)
        {
            cell.Style.Fill.PatternType = XLFillPatternValues.Solid;
            cell.Style.Fill.BackgroundColor = XLColor.Yellow;
        }
    }

    public static void BeforeHire(IXLCell cell)
    {
        cell.Clear(XLClearOptions.Contents);
        cell.Style.Fill.PatternType = XLFillPatternValues.Solid;
        cell.Style.Fill.BackgroundColor = XLColor.LightPink;
        cell.Style.Fill.PatternColor = XLColor.LightPink;
        cell.Style.Font.FontColor = XLColor.Black;
    }

    public static void Blackout(IXLCell cell)
    {
        cell.Clear(XLClearOptions.Contents);
        cell.Style.Fill.PatternType = XLFillPatternValues.Solid;
        cell.Style.Fill.BackgroundColor = XLColor.Black;
        cell.Style.Fill.PatternColor = XLColor.Black;
        cell.Style.Font.FontColor = XLColor.White;
    }
}
