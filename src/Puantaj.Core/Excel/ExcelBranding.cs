using ClosedXML.Excel;
using Puantaj.Core.Data;

namespace Puantaj.Core.Excel;

internal static class ExcelBranding
{
    public static void ApplyWeekly(IXLWorksheet sheet, AppSettings? settings)
    {
        if (settings is null) return;
        AddLogo(sheet, settings, "A1");
        sheet.Cell("B33").Value = settings.DepartmentManager;
        sheet.Cell("B34").Value = settings.DepartmentManagerTitle;
        sheet.Cell("J33").Value = settings.HumanResourcesManager;
        sheet.Cell("J34").Value = settings.HumanResourcesTitle;
    }

    public static void ApplyMonthly(IXLWorksheet sheet, AppSettings? settings)
    {
        if (settings is null) return;
        AddLogo(sheet, settings, "B2");
        WriteSignature(sheet, "E52", "E53", settings.HumanResourcesManager, settings.HumanResourcesTitle);
        WriteSignature(sheet, "AC52", "AC53", settings.DepartmentManager, settings.DepartmentManagerTitle);
        WriteSignature(sheet, "AS52", "AS53", settings.GeneralManager, settings.GeneralManagerTitle);
    }

    private static void AddLogo(IXLWorksheet sheet, AppSettings settings, string cell)
    {
        if (!settings.PrintLogo || string.IsNullOrWhiteSpace(settings.LogoPath) || !File.Exists(settings.LogoPath)) return;
        try
        {
            var picture = sheet.AddPicture(settings.LogoPath).MoveTo(sheet.Cell(cell));
            var width = Math.Max(30, (int)(settings.LogoSizeCm * 38));
            var ratio = picture.OriginalWidth == 0 ? 1d : (double)picture.OriginalHeight / picture.OriginalWidth;
            picture.WithSize(width, Math.Max(20, (int)(width * ratio)));
        }
        catch (Exception)
        {
            // Bozuk/desteklenmeyen bir logo dosyası tüm dışa aktarımı durdurmamalı; logo atlanır.
        }
    }

    private static void WriteSignature(IXLWorksheet sheet, string nameCell, string titleCell, string name, string title)
    {
        if (!string.IsNullOrWhiteSpace(name)) sheet.Cell(nameCell).Value = name;
        if (!string.IsNullOrWhiteSpace(title)) sheet.Cell(titleCell).Value = title;
        sheet.Cell(nameCell).Style.Alignment.WrapText = true; sheet.Cell(titleCell).Style.Alignment.WrapText = true;
    }
}
