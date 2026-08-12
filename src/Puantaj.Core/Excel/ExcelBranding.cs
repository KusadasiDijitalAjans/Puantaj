using ClosedXML.Excel;
using Puantaj.Core.Data;

namespace Puantaj.Core.Excel;

internal static class ExcelBranding
{
    public static void ApplyMonthly(IXLWorksheet sheet, AppSettings? settings)
    {
        if (settings is null) return;
        KeepTemplateLogoInRange(sheet, "C1:C4");
        WriteSignature(sheet, "E52", "E53", settings.HumanResourcesManager, settings.HumanResourcesTitle);
        WriteSignature(sheet, "AC52", "AC53", settings.DepartmentManager, settings.DepartmentManagerTitle);
        WriteSignature(sheet, "AS52", "AS53", settings.GeneralManager, settings.GeneralManagerTitle);
    }

    private static void KeepTemplateLogoInRange(IXLWorksheet sheet, string rangeAddress)
    {
        try
        {
            var pictures = sheet.Pictures.OrderByDescending(picture => picture.OriginalWidth * picture.OriginalHeight).ToList();
            if (pictures.Count == 0) return;
            var picture = pictures[0];
            foreach (var duplicate in pictures.Skip(1)) duplicate.Delete();
            var range = sheet.Range(rangeAddress); var first = range.FirstCell();
            var width = ColumnWidthToPixels(sheet.Column(first.Address.ColumnNumber).Width);
            var height = Enumerable.Range(first.Address.RowNumber, range.RowCount())
                .Sum(row => (int)Math.Round(sheet.Row(row).Height * 96d / 72d));
            const int padding = 4;
            var scale = Math.Min((width - padding * 2d) / picture.OriginalWidth, (height - padding * 2d) / picture.OriginalHeight);
            var targetWidth = Math.Max(1, (int)Math.Floor(picture.OriginalWidth * scale));
            var targetHeight = Math.Max(1, (int)Math.Floor(picture.OriginalHeight * scale));
            picture.WithSize(targetWidth, targetHeight)
                .MoveTo(first, Math.Max(0, (width - targetWidth) / 2), Math.Max(0, (height - targetHeight) / 2));
        }
        catch (Exception)
        {
            // Bozuk/desteklenmeyen logo dışa aktarımı durdurmamalı.
        }
    }

    private static int ColumnWidthToPixels(double width) =>
        (int)Math.Truncate(((256d * width + Math.Truncate(128d / 7d)) / 256d) * 7d);

    private static void WriteSignature(IXLWorksheet sheet, string nameCell, string titleCell, string name, string title)
    {
        if (!string.IsNullOrWhiteSpace(name)) sheet.Cell(nameCell).Value = name;
        if (!string.IsNullOrWhiteSpace(title)) sheet.Cell(titleCell).Value = title;
        sheet.Cell(nameCell).Style.Alignment.WrapText = false; sheet.Cell(titleCell).Style.Alignment.WrapText = false;
        sheet.Cell(nameCell).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        sheet.Cell(titleCell).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
    }
}
