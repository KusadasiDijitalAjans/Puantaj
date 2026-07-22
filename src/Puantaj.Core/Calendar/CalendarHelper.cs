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
}
