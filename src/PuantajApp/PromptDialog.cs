namespace PuantajApp;

internal static class PromptDialog
{
    public static string? Show(string title, string label, string initialValue = "")
    {
        using var form = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(420, 140)
        };
        var text = new TextBox { Text = initialValue, Left = 16, Top = 48, Width = 385 };
        var ok = new Button { Text = "Tamam", DialogResult = DialogResult.OK, Left = 245, Top = 92, Width = 75 };
        var cancel = new Button { Text = "İptal", DialogResult = DialogResult.Cancel, Left = 326, Top = 92, Width = 75 };
        form.Controls.Add(new Label { Text = label, AutoSize = true, Left = 16, Top = 18 });
        form.Controls.AddRange([text, ok, cancel]);
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        return form.ShowDialog() == DialogResult.OK ? text.Text.Trim() : null;
    }
}
