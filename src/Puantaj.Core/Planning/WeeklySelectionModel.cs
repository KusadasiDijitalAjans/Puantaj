namespace Puantaj.Core.Planning;

public sealed class WeeklySelectionModel
{
    private readonly Dictionary<DateOnly, string> _values = [];
    public IReadOnlyDictionary<DateOnly, string> Values => _values;
    public void Select(DateOnly date, string code) => _values[date] = string.IsNullOrWhiteSpace(code)
        ? throw new ArgumentException("Kod boş olamaz.") : code;
    public void Clear() => _values.Clear();
}
