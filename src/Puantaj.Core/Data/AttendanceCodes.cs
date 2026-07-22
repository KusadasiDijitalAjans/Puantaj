namespace Puantaj.Core.Data;

public static class AttendanceCodes
{
    public static readonly string[] WorkShifts = ["A", "B", "C", "D", "E"];
    public static readonly string[] All = ["A", "B", "C", "D", "E", "HT", "RT", "RP", "ÜZ", "Üİ", "Yİ", "Aİ", "G"];

    public static bool IsValid(string? code) =>
        !string.IsNullOrWhiteSpace(code) && All.Contains(code.Trim().ToUpperInvariant(), StringComparer.Ordinal);

    public static string Normalize(string code)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return IsValid(normalized)
            ? normalized
            : throw new ArgumentException($"Geçersiz puantaj kodu: {code}", nameof(code));
    }

    public static string ToMonthlyCode(string code)
    {
        var normalized = Normalize(code);
        return WorkShifts.Contains(normalized, StringComparer.Ordinal) ? "X" : normalized;
    }
}
