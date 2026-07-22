namespace Puantaj.Core.Data;

public sealed class AssignmentCodeResolver
{
    private readonly IReadOnlyDictionary<string, AssignmentCodeDefinition> _definitions;

    public AssignmentCodeResolver(IEnumerable<AssignmentCodeDefinition> definitions) =>
        _definitions = definitions.ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);

    public AssignmentCodeDefinition Resolve(string code) =>
        _definitions.TryGetValue(code.Trim(), out var definition)
            ? definition
            : throw new ArgumentException($"Tanımsız çalışma kodu: {code}", nameof(code));

    public string ToMonthlyValue(string code)
    {
        var definition = Resolve(code);
        return definition.IsWorkShift ? "X" : definition.Code;
    }
}
