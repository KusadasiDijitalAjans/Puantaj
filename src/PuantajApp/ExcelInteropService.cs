using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PuantajApp;

internal sealed class ExcelNotInstalledException : Exception
{
    public ExcelNotInstalledException() : base("Microsoft Excel bulunamadı.") { }
}

[SupportedOSPlatform("windows")]
internal sealed class ExcelInteropService
{
    public static Task RunStaAsync(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { action(); completion.SetResult(); }
            catch (Exception exception) { completion.SetException(exception); }
        }) { IsBackground = true, Name = "Puantaj Excel" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    public void ExportPdf(string excelPath, string pdfPath)
    {
        dynamic? application = null;
        dynamic? workbooks = null;
        dynamic? workbook = null;
        try
        {
            application = CreateExcel();
            application.Visible = false;
            application.DisplayAlerts = false;
            workbooks = application.Workbooks;
            workbook = workbooks.Open(Path.GetFullPath(excelPath), ReadOnly: true);
            workbook.ExportAsFixedFormat(0, Path.GetFullPath(pdfPath));
        }
        finally
        {
            CloseExcel(application, workbooks, workbook);
        }
    }

    public void PrintWithDialog(string excelPath)
    {
        dynamic? application = null;
        dynamic? workbooks = null;
        dynamic? workbook = null;
        dynamic? dialogs = null;
        dynamic? printDialog = null;
        try
        {
            application = CreateExcel();
            application.Visible = false;
            application.DisplayAlerts = false;
            workbooks = application.Workbooks;
            workbook = workbooks.Open(Path.GetFullPath(excelPath), ReadOnly: true);
            workbook.Activate();
            dialogs = application.Dialogs;
            printDialog = dialogs[8]; // Excel XlBuiltInDialog.xlDialogPrint
            printDialog.Show();
        }
        finally
        {
            Release(printDialog);
            Release(dialogs);
            CloseExcel(application, workbooks, workbook);
        }
    }

    private static dynamic CreateExcel()
    {
        var type = Type.GetTypeFromProgID("Excel.Application");
        if (type is null) throw new ExcelNotInstalledException();
        return Activator.CreateInstance(type) ?? throw new ExcelNotInstalledException();
    }

    private static void CloseExcel(dynamic? application, dynamic? workbooks, dynamic? workbook)
    {
        try { workbook?.Close(false); } catch { }
        try { application?.Quit(); } catch { }
        Release(workbook);
        Release(workbooks);
        Release(application);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    private static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
    }
}
