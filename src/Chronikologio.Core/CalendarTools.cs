using System.Globalization;

namespace Chronikologio.Core;

/// <summary>Proleptic calendar conversions, not historical adoption dates.</summary>
public static class CalendarTools
{
    public sealed record CalendarDate(string System, int Year, int Month, int Day);
    public sealed record Interval(int TotalDays, int Years, int Months, int Days);
    private static Calendar Create(string system) => system switch
    {
        "gregorian" => new GregorianCalendar(),
        "julian" => new JulianCalendar(),
        "hebrew" => new HebrewCalendar(),
        "hijri" => new HijriCalendar { HijriAdjustment = 0 },
        "persian" => new PersianCalendar(),
        _ => throw new ArgumentOutOfRangeException(nameof(system))
    };
    public static readonly IReadOnlyList<string> Systems = Array.AsReadOnly(new[] { "gregorian", "julian", "hebrew", "hijri", "persian" });
    public static CalendarDate? Convert(DateOnly date, string system)
    {
        var c = Create(system);
        var d = date.ToDateTime(TimeOnly.MinValue);
        if (d < c.MinSupportedDateTime || d > c.MaxSupportedDateTime) return null;
        return new(system, c.GetYear(d), c.GetMonth(d), c.GetDayOfMonth(d));
    }
    public static DateOnly ToGregorian(string system, int year, int month, int day)
        => DateOnly.FromDateTime(Create(system).ToDateTime(year, month, day, 0, 0, 0, 0));
    public static Interval Between(DateOnly start, DateOnly end)
    {
        var total = end.DayNumber - start.DayNumber;
        if (total < 0) (start, end) = (end, start);
        var years = end.Year - start.Year;
        if (start.AddYears(years) > end) years--;
        var anchor = start.AddYears(years);
        var months = (end.Year - anchor.Year) * 12 + end.Month - anchor.Month;
        if (anchor.AddMonths(months) > end) months--;
        return new(total, years, months, end.DayNumber - anchor.AddMonths(months).DayNumber);
    }
    public static DateOnly NextAnniversary(DateOnly original, DateOnly reference)
    {
        return TryNextAnniversary(original, reference) ?? throw new ArgumentOutOfRangeException(nameof(reference), "No future anniversary in the supported date range.");
    }
    public static DateOnly? TryNextAnniversary(DateOnly original, DateOnly reference)
    {
        var year = Math.Max(original.Year, reference.Year);
        DateOnly At(int y) => new(y, original.Month, Math.Min(original.Day, DateTime.DaysInMonth(y, original.Month)));
        var result = At(year);
        return result >= reference ? result : year == 9999 ? null : At(year + 1);
    }
    public static (int Year, int Week) IsoWeek(DateOnly date)
    {
        var d = date.ToDateTime(TimeOnly.MinValue);
        return (ISOWeek.GetYear(d), ISOWeek.GetWeekOfYear(d));
    }
    public static int Weekdays(DateOnly start, DateOnly end)
    {
        if (end < start) (start, end) = (end, start);
        // Inclusive; weekdays only, not statutory holidays.
        var count = end.DayNumber - start.DayNumber + 1;
        var days = count / 7 * 5;
        for (var i = 0; i < count % 7; i++)
            if (start.AddDays(i).DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) days++;
        return days;
    }
}
