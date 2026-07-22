using System.Drawing.Drawing2D;
using Puantaj.Core.Planning;

namespace PuantajApp;

internal sealed class MonthlySummaryPanel : Panel
{
    internal const int PreferredCardWidth = 146;
    internal const int MinimumCardWidth = 76;
    internal const int CardHeight = 66;
    internal const int PreferredGap = 8;

    public MonthlySummaryPanel()
    {
        Dock = DockStyle.Bottom;
        Height = 0;
        Padding = new Padding(8, 5, 8, 5);
        BackColor = Color.White;
        AutoScroll = false;
        DoubleBuffered = true;
        Resize += (_, _) => LayoutCards();
    }

    public void SetSummary(MonthlySummary summary)
    {
        SuspendLayout();
        foreach (Control control in Controls.Cast<Control>().ToArray()) control.Dispose();
        Controls.Clear();
        foreach (var item in summary.Items.Where(item => item.Days > 0))
            Controls.Add(new MonthlySummaryCard(item, StyleFor(item)));
        Height = Controls.Count == 0 ? 0 : CardHeight + Padding.Vertical;
        Visible = Controls.Count > 0;
        LayoutCards();
        ResumeLayout();
    }

    internal static (int CardWidth, int Gap) CalculateLayout(int availableWidth, int count)
    {
        if (availableWidth <= 0 || count <= 0) return (0, 0);
        var gap = count == 1 ? 0 : PreferredGap;
        var width = Math.Min(PreferredCardWidth, (availableWidth - gap * (count - 1)) / count);
        if (width >= MinimumCardWidth) return (width, gap);
        gap = count == 1 ? 0 : Math.Max(2, Math.Min(PreferredGap, (availableWidth - MinimumCardWidth * count) / (count - 1)));
        width = Math.Max(1, (availableWidth - gap * (count - 1)) / count);
        return (width, gap);
    }

    private void LayoutCards()
    {
        if (Controls.Count == 0) return;
        var available = Math.Max(0, ClientSize.Width - Padding.Horizontal);
        var (width, gap) = CalculateLayout(available, Controls.Count);
        var left = Padding.Left;
        foreach (Control control in Controls)
        {
            control.Bounds = new Rectangle(left, Padding.Top, width, CardHeight);
            left += width + gap;
        }
    }

    private static SummaryCardStyle StyleFor(MonthlySummaryItem item) => item.Key switch
    {
        MonthlySummaryKeys.Work => Style("▰", 13, 104, 220, 238, 246, 255),
        MonthlySummaryKeys.WeeklyRest => Style("▦", 10, 163, 181, 235, 251, 252),
        MonthlySummaryKeys.OfficialHoliday => Style("⚑", 24, 158, 88, 238, 250, 241),
        MonthlySummaryKeys.MedicalReport => Style("▤", 238, 112, 28, 255, 246, 238),
        MonthlySummaryKeys.PaidLeave => Style("✓", 42, 157, 94, 240, 250, 242),
        MonthlySummaryKeys.AnnualLeave => Style("☂", 224, 158, 0, 255, 249, 230),
        MonthlySummaryKeys.UnpaidLeave => Style("●", 48, 160, 216, 240, 249, 255),
        MonthlySummaryKeys.CompensatoryLeave => Style("◷", 214, 91, 137, 253, 241, 246),
        MonthlySummaryKeys.Duty => Style("◆", 127, 87, 188, 247, 241, 252),
        MonthlySummaryKeys.TotalLeave => Style("◔", 168, 94, 190, 249, 241, 251),
        MonthlySummaryKeys.TotalValid => new("▦", Color.FromArgb(22, 48, 82), Color.FromArgb(179, 193, 211), Color.FromArgb(245, 248, 252), true),
        _ => Style("●", 104, 116, 136, 246, 248, 251)
    };

    private static SummaryCardStyle Style(string icon, int red, int green, int blue, int backRed, int backGreen, int backBlue) =>
        new(icon, Color.FromArgb(red, green, blue), Color.FromArgb(red, green, blue), Color.FromArgb(backRed, backGreen, backBlue), false);
}

internal sealed record SummaryCardStyle(string Icon, Color Accent, Color Border, Color Background, bool Emphasized);

internal sealed class MonthlySummaryCard : Control
{
    private readonly MonthlySummaryItem _item;
    private readonly SummaryCardStyle _style;

    public MonthlySummaryCard(MonthlySummaryItem item, SummaryCardStyle style)
    {
        _item = item;
        _style = style;
        DoubleBuffered = true;
        TabStop = false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
        using var path = RoundedRectangle(bounds, 8);
        using var background = new SolidBrush(_style.Background);
        using var border = new Pen(_style.Border, _style.Emphasized ? 1.5f : 1f);
        e.Graphics.FillPath(background, path);
        e.Graphics.DrawPath(border, path);

        var compact = Width < 105;
        var iconWidth = compact ? 22 : 30;
        using var iconFont = new Font("Segoe UI Symbol", compact ? 13f : 17f, FontStyle.Bold, GraphicsUnit.Point);
        using var titleFont = new Font("Segoe UI", compact ? 6.6f : 7.5f, FontStyle.Bold, GraphicsUnit.Point);
        using var numberFont = new Font("Segoe UI", compact ? 13f : 16f, FontStyle.Bold, GraphicsUnit.Point);
        using var accent = new SolidBrush(_style.Accent);
        using var titleFormat = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
        using var centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        e.Graphics.DrawString(_style.Icon, iconFont, accent, new RectangleF(5, 8, iconWidth, Height - 16), centered);
        var textLeft = 7 + iconWidth;
        e.Graphics.DrawString(_item.Title, titleFont, accent, new RectangleF(textLeft, 5, Math.Max(1, Width - textLeft - 4), 29), titleFormat);
        e.Graphics.DrawString(_item.Days.ToString(), numberFont, accent, new RectangleF(textLeft, 31, Math.Max(1, Width - textLeft - 4), 28), titleFormat);
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
