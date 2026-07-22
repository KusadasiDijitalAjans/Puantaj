using Puantaj.Core.Data;
using Puantaj.Core.Excel;

namespace PuantajApp;

internal sealed class MonthlyExportControl : UserControl
{
    private readonly PuantajDatabase _database;
    private readonly Func<int> _year;
    private readonly Func<int> _month;
    private readonly string _hotelName;
    private readonly string _departmentName;
    private readonly Label _selection = new() { AutoSize = true, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold) };
    private readonly Button _create = new() { Text = "Excel Olarak Kaydet", AutoSize = true };
    private readonly Button _pdf = new() { Text = "PDF Olarak Kaydet", AutoSize = true };
    private readonly Button _print = new() { Text = "Yazdır", AutoSize = true };

    public MonthlyExportControl(PuantajDatabase database, Func<int> year, Func<int> month, string hotelName, string departmentName)
    {
        _database = database;
        _year = year;
        _month = month;
        _hotelName = hotelName;
        _departmentName = departmentName;
        _create.Click += (_, _) => RunExclusive(SaveExcel);
        _pdf.Click += (_, _) => RunExclusive(SavePdf);
        _print.Click += (_, _) => RunExclusive(Print);
        var panel = new FlowLayoutPanel { Dock = DockStyle.Top, Padding = new Padding(20), Height = 100 };
        panel.Controls.Add(_selection);
        panel.Controls.Add(_create);
        panel.Controls.Add(_pdf);
        panel.Controls.Add(_print);
        Controls.Add(panel);
        UpdateSelection();
    }

    private void RunExclusive(Action action)
    {
        _create.Enabled = _pdf.Enabled = _print.Enabled = false;
        try { action(); }
        finally { _create.Enabled = _pdf.Enabled = _print.Enabled = true; }
    }

    public void UpdateSelection() =>
        _selection.Text = $"{_year()} yılı, {_month():00}. ay için aylık puantaj oluştur:  ";

    private string CreateExcel(string outputPath)
    {
        var year = _year();
        var month = _month();
        var templatesDirectory = Path.Combine(AppContext.BaseDirectory, "templates");
        var template = MonthlyExcelExporter.FindMonthlyTemplate(templatesDirectory);
        var from = new DateOnly(year, month, 1);
        var to = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        return new MonthlyExcelExporter().Export(template, outputPath, _hotelName, _departmentName, year, month,
            _database.GetEmployeesForPeriod(from, to), _database.GetAssignments(from, to), _database.GetAssignmentCodes(), _database.GetSettings());
    }

    private void SaveExcel()
    {
        try
        {
            var year = _year();
            var month = _month();
            var fileName = MonthlyExcelExporter.CreateOutputFileName(_departmentName, year, month);
            using var dialog = new SaveFileDialog { Filter = "Excel dosyası (*.xlsx)|*.xlsx", FileName = fileName };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            CreateExcel(dialog.FileName);
            MessageBox.Show($"Excel oluşturuldu:\n{dialog.FileName}", "Puantaj", MessageBoxButtons.OK, MessageBoxIcon.Information);
            AskToLockMonth();
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Excel oluşturulamadı", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SavePdf()
    {
        var pdfName = Path.ChangeExtension(MonthlyExcelExporter.CreateOutputFileName(_departmentName, _year(), _month()), ".pdf");
        using var dialog = new SaveFileDialog { Filter = "PDF dosyası (*.pdf)|*.pdf", FileName = pdfName };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var temporary = Path.Combine(Path.GetTempPath(), $"puantaj-monthly-{Guid.NewGuid():N}.xlsx");
        try
        {
            CreateExcel(temporary);
            new ExcelInteropService().ExportPdf(temporary, dialog.FileName);
            MessageBox.Show($"PDF oluşturuldu:\n{dialog.FileName}", "Puantaj", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (ExcelNotInstalledException)
        {
            var fallback = Path.ChangeExtension(dialog.FileName, ".xlsx");
            File.Move(temporary, fallback, true);
            MessageBox.Show("PDF oluşturmak için bu bilgisayarda Microsoft Excel kurulu olmalıdır. Excel dosyanız başarıyla oluşturuldu."
                + $"\n\n{fallback}", "Puantaj", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception exception) { MessageBox.Show(exception.Message, "PDF oluşturulamadı", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { TryDelete(temporary); }
    }

    private void Print()
    {
        var temporary = Path.Combine(Path.GetTempPath(), $"puantaj-monthly-{Guid.NewGuid():N}.xlsx");
        try
        {
            CreateExcel(temporary);
            new ExcelInteropService().PrintWithDialog(temporary);
        }
        catch (ExcelNotInstalledException)
        {
            MessageBox.Show("Yazdırmak için bu bilgisayarda Microsoft Excel kurulu olmalıdır.", "Puantaj", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception exception) { MessageBox.Show(exception.Message, "Yazdırma hatası", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { TryDelete(temporary); }
    }

    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }

    private void AskToLockMonth()
    {
        if (_database.IsMonthLocked(_year(), _month())) return;
        if (MessageBox.Show("Bu ay kilitlensin mi?\n\nKilitlenen ayın planı ve çıktıları, kilit kaldırılana kadar değiştirilemez.",
            "Ay kilitleme", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        _database.LockMonth(_year(), _month());
        MessageBox.Show("Ay kilitlendi.", "Puantaj", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
