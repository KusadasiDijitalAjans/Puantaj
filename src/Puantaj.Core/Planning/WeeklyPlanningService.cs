using Puantaj.Core.Data;

namespace Puantaj.Core.Planning;

public enum WeekCompletionStatus { Waiting, Missing, Completed }

public sealed record MonthWeek(int Number, DateOnly Monday, DateOnly Sunday, DateOnly ActiveFrom, DateOnly ActiveTo);

public sealed record PreviewTotals(int WorkDays, int LeaveDays, int ValidDays);

public sealed class WeeklyPlanningService
{
    public IReadOnlyList<MonthWeek> GetMonthWeeks(int year, int month)
    {
        var first = new DateOnly(year, month, 1);
        var last = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var monday = first.AddDays(-(((int)first.DayOfWeek + 6) % 7));
        var result = new List<MonthWeek>();
        for (var number = 1; monday <= last; number++, monday = monday.AddDays(7))
            result.Add(new(number, monday, monday.AddDays(6), Max(first, monday), Min(last, monday.AddDays(6))));
        return result;
    }

    public IReadOnlyDictionary<DateOnly, string> BuildWeek(
        MonthWeek week, string? defaultWorkCode, IReadOnlyDictionary<DateOnly, string> exceptions)
    {
        var result = new Dictionary<DateOnly, string>();
        for (var date = week.ActiveFrom; date <= week.ActiveTo; date = date.AddDays(1))
        {
            if (exceptions.TryGetValue(date, out var code)) result[date] = code;
            else if (!string.IsNullOrWhiteSpace(defaultWorkCode)) result[date] = defaultWorkCode;
            else throw new InvalidOperationException("Boş günler için varsayılan çalışma vardiyası seçin veya her güne özel bir kod atayın.");
        }
        return result;
    }

    public WeekCompletionStatus GetStatus(MonthWeek week, IReadOnlyDictionary<DateOnly, string> assignments)
    {
        var assigned = 0;
        for (var date = week.ActiveFrom; date <= week.ActiveTo; date = date.AddDays(1))
            if (assignments.ContainsKey(date)) assigned++;
        if (assigned == 0) return WeekCompletionStatus.Waiting;
        return assigned == week.ActiveTo.DayNumber - week.ActiveFrom.DayNumber + 1
            ? WeekCompletionStatus.Completed : WeekCompletionStatus.Missing;
    }

    public IReadOnlyDictionary<DateOnly, string> CopyToWeek(
        MonthWeek source, MonthWeek target, IReadOnlyDictionary<DateOnly, string> sourceAssignments)
    {
        var result = new Dictionary<DateOnly, string>();
        for (var offset = 0; offset < 7; offset++)
        {
            var sourceDate = source.Monday.AddDays(offset); var targetDate = target.Monday.AddDays(offset);
            if (targetDate < target.ActiveFrom || targetDate > target.ActiveTo) continue;
            if (sourceAssignments.TryGetValue(sourceDate, out var code)) result[targetDate] = code;
        }
        return result;
    }

    public PreviewTotals CalculateTotals(IEnumerable<Assignment> assignments, IReadOnlyList<AssignmentCodeDefinition> definitions)
    {
        var resolver = new AssignmentCodeResolver(definitions);
        var work = 0; var leave = 0; var valid = 0; var ended = false;
        foreach (var assignment in assignments.OrderBy(item => item.WorkDate))
        {
            var definition = resolver.Resolve(assignment.Code);
            if (definition.IsEmploymentEnded) ended = true;
            if (ended) continue;
            valid++;
            if (definition.IsWorkShift) work++; else leave++;
        }
        return new(work, leave, valid);
    }

    private static DateOnly Min(DateOnly left, DateOnly right) => left < right ? left : right;
    private static DateOnly Max(DateOnly left, DateOnly right) => left > right ? left : right;
}
