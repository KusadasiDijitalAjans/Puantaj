namespace Puantaj.Core.Calendar;

public static class CalendarHelper
{
    public static int DaysInMonth(int year, int month) => DateTime.DaysInMonth(year, month);

    public static DateOnly StartOfWeek(DateOnly date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday);
    }

    public static IReadOnlyList<DateOnly> Week(DateOnly monday) =>
        Enumerable.Range(0, 7).Select(monday.AddDays).ToArray();

    public static IReadOnlyList<DateOnly> WeeksIntersectingMonth(int year, int month)
    {
        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var firstMonday = StartOfWeek(first);
        var lastMonday = StartOfWeek(last);
        return Enumerable.Range(0, (lastMonday.DayNumber - firstMonday.DayNumber) / 7 + 1)
            .Select(index => firstMonday.AddDays(index * 7)).ToArray();
    }
}
