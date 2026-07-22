using Puantaj.Core.Data;

namespace Puantaj.Core.Excel;

public sealed class AttendanceExcelCodeMapper
{
    private readonly AssignmentCodeResolver _resolver;

    public AttendanceExcelCodeMapper(IEnumerable<AssignmentCodeDefinition> definitions) =>
        _resolver = new AssignmentCodeResolver(definitions);

    public string Map(string code)
    {
        var definition = _resolver.Resolve(code);
        if (definition.IsWorkShift) return "X";
        if (definition.IsEmploymentEnded) throw new InvalidOperationException("İşten ayrılma kodu Excel hücresine yazılamaz.");
        var description = definition.Description.Trim();
        if (description.Contains("Mazeret", StringComparison.CurrentCultureIgnoreCase) || code.Equals("Aİ", StringComparison.OrdinalIgnoreCase)) return "Mİ";
        if (description.Contains("Devamsız", StringComparison.CurrentCultureIgnoreCase) || code.Equals("DZ", StringComparison.OrdinalIgnoreCase)) return "DZ";
        if (description.Contains("Görevli", StringComparison.CurrentCultureIgnoreCase) || code.Equals("G", StringComparison.OrdinalIgnoreCase)) return "GR";
        return code.Trim().ToUpperInvariant() switch
        {
            "HT" or "RT" or "Üİ" or "RP" or "Yİ" or "ÜZ" => code.Trim().ToUpperInvariant(),
            _ => throw new InvalidOperationException($"'{code}' puantaj kodunun resmi Excel şablonunda karşılığı tanımlı değil.")
        };
    }
}
