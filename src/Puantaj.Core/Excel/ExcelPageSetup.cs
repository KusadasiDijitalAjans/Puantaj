using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Puantaj.Core.Excel;

internal static class ExcelPageSetup
{
    public static void ApplyA4(IXLWorksheet sheet, string printArea, XLPageOrientation orientation)
    {
        sheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
        sheet.PageSetup.PageOrientation = orientation;
        sheet.PageSetup.FitToPages(1, 1);
        sheet.PageSetup.CenterHorizontally = true;
        sheet.PageSetup.Margins.Left = 0.25;
        sheet.PageSetup.Margins.Right = 0.25;
        sheet.PageSetup.Margins.Top = 0.4;
        sheet.PageSetup.Margins.Bottom = 0.4;
        sheet.PageSetup.Margins.Header = 0.15;
        sheet.PageSetup.Margins.Footer = 0.15;
        sheet.PageSetup.PrintAreas.Clear();
        sheet.PageSetup.PrintAreas.Add(printArea);
    }

    public static void EnsureSavedA4(string path)
    {
        using var document = SpreadsheetDocument.Open(path, true);
        var workbookPart = document.WorkbookPart ?? throw new InvalidOperationException("Excel workbook bölümü bulunamadı.");
        var sheet = workbookPart.Workbook.Sheets?.Elements<Sheet>().Single()
                    ?? throw new InvalidOperationException("Excel sayfası bulunamadı.");
        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
        var worksheet = worksheetPart.Worksheet;
        var pageSetup = worksheet.Elements<PageSetup>().FirstOrDefault() ?? worksheet.AppendChild(new PageSetup());
        pageSetup.PaperSize = 9U; // A4
        pageSetup.Orientation = OrientationValues.Landscape;
        pageSetup.FitToWidth = 1U;
        pageSetup.FitToHeight = 1U;
        pageSetup.Scale = null;
        var properties = worksheet.GetFirstChild<SheetProperties>() ?? worksheet.PrependChild(new SheetProperties());
        properties.PageSetupProperties ??= new PageSetupProperties();
        properties.PageSetupProperties.FitToPage = true;
        worksheet.Save();
    }
}
