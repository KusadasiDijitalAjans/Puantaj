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
        _create.Click += async (_, _) => await RunExclusive(SaveExcelAsync);
        _pdf.Click += async (_, _) => await RunExclusive(SavePdfAsync);
        _print.Click += async (_, _) => await RunExclusive(PrintAsync);
        var panel = new FlowLayoutPanel { Dock = DockStyle.Top, Padding = new Padding(20), Height = 100 };
        panel.Controls.Add(_selection);
        panel.Controls.Add(_create);
        panel.Controls.Add(_pdf);
        panel.Controls.Add(_print);
        Controls.Add(panel);
        UpdateSelection();
    }

    private async Task RunExclusive(Func<Task> action)
    {
        _create.Enabled = _pdf.Enabled = _print.Enabled = false;
        try { await action(); }
        finally { _create.Enabled = _pdf.Enabled = _print.Enabled = true; }
    }

    public void UpdateSelection() =>
        _selection.Text = $"{_year()} yılı, {_month():00}. ay için aylık puantaj oluştur:  ";

    private Task<string> CreateExcelAsync(string outputPath)
    {
        var year = _year();
        var month = _month();
        var templatesDirectory = Path.Combine(AppContext.BaseDirectory, "templates");
        var template = MonthlyExcelExporter.FindMonthlyTemplate(templatesDirectory);
        var from = new DateOnly(year, month, 1);
        var to = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var employees = _database.GetEmployeesForPeriod(from, to);
        var assignments = _database.GetAssignments(from, to);
        var codes = _database.GetAssignmentCodes();
        var settings = _database.GetSettings();
        return Task.Run(() => new MonthlyExcelExporter().Export(template, outputPath, _hotelName, _departmentName,
            year, month, employees, assignments, codes, settings));
    }

    private async Task SaveExcelAsync()
    {
        try
        {
            var year = _year();
            var month = _month();
            var fileName = MonthlyExcelExporter.CreateOutputFileName(_departmentName, year, month);
            using var dialog = new SaveFileDialog { Filter = "Excel dosyası (*.xlsx)|*.xlsx", FileName = fileName };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            await CreateExcelAsync(dialog.FileName);
            MessageBox.Show($"Excel oluşturuldu:\n{dialog.FileName}", "Puantaj", MessageBoxButtons.OK, MessageBoxIcon.Information);
            AskToLockMonth();
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Excel oluşturulamadı", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task SavePdfAsync()
    {
        var pdfName = Path.ChangeExtension(MonthlyExcelExporter.CreateOutputFileName(_departmentName, _year(), _month()), ".pdf");
        using var dialog = new SaveFileDialog { Filter = "PDF dosyası (*.pdf)|*.pdf", FileName = pdfName };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var temporary = Path.Combine(Path.GetTempPath(), $"puantaj-monthly-{Guid.NewGuid():N}.xlsx");
        try
        {
            await CreateExcelAsync(temporary);
            await ExcelInteropService.RunStaAsync(() => new ExcelInteropService().ExportPdf(temporary, dialog.FileName));
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

    private async Task PrintAsync()
    {
        var temporary = Path.Combine(Path.GetTempPath(), $"puantaj-monthly-{Guid.NewGuid():N}.xlsx");
        try
        {
            await CreateExcelAsync(temporary);
            await ExcelInteropService.RunStaAsync(() => new ExcelInteropService().PrintWithDialog(temporary));
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
        var result = _database.LockMonthIfComplete(_year(), _month());
        if (result.Missing.Count > 0)
        {
            var details = string.Join("\n", result.Missing.Take(20).Select(item => $"• {item.EmployeeName} — {item.WorkDate:dd.MM.yyyy}"));
            MessageBox.Show("Ay kilitlenemedi. Eksik kayıtlar:\n\n" + details, "Eksik Puantaj", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        MessageBox.Show("Ay kilitlendi.", "Puantaj", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
