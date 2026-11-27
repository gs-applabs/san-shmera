namespace Chronikologio.Core;

/// <summary>Scoped calendar profiles, not individual payroll or employment advice.
/// Dates are factual rules and selected year lists, not reproduced source text.
/// Outside reviewed coverage no workday total is returned.</summary>
public static class WorkdayCalendar
{
    public const string GreeceSource = "https://ypergasias.gov.gr/ergasiakes-scheseis/atomikes-ergasiakes-sxeseis/chronika-oria-ergasias/";
    public const string CyprusSource = "https://www.centralbank.cy/el/the-bank/working-hours-bank-holidays";
    public sealed record Result(int Weekdays, int ExcludedHolidays, int Workdays);
    public static IReadOnlyList<DateOnly>? Dates(int year, string region)
    {
        if (region == "gr" && year == 2026)
        {
            // Nine nationwide compulsory holidays under article 60 of Law 4808/2021.
            // May 1 is Friday, outside Holy Week, in this reviewed year.
            return new[] { new DateOnly(year,1,1), new(year,1,6), new(year,3,25),
                Paschalion.OrthodoxEaster(year).AddDays(1), new(year,5,1), new(year,8,15),
                new(year,10,28), new(year,12,25), new(year,12,26) };
        }
        if (region == "cy" && year == 2026)
        {
            // Central Bank of Cyprus published dates (14 August 2025).
            return new[] { new DateOnly(year,1,1), new(year,1,6), new(year,2,23),
                new(year,3,25), new(year,4,1), new(year,4,10), new(year,4,13), new(year,4,14),
                new(year,5,1), new(year,6,1), new(year,8,15), new(year,10,1), new(year,10,28),
                new(year,12,25), new(year,12,26) };
        }
        if (region == "cy" && year == 2027)
        {
            // Central Bank of Cyprus published dates (6 April 2026).
            return new[] { new DateOnly(year,1,1), new(year,1,6), new(year,3,15),
                new(year,3,25), new(year,4,1), new(year,4,30), new(year,5,1), new(year,5,3),
                new(year,5,4), new(year,6,21), new(year,8,15), new(year,10,1), new(year,10,28),
                new(year,12,25), new(year,12,26) };
        }
        return null;
    }
    public static Result? Between(DateOnly start, DateOnly end, string region)
    {
        if (end < start) (start,end) = (end,start);
        // Current source coverage is at most two years, so never scan huge intervals.
        if (start.Year < 2026 || end.Year > (region == "cy" ? 2027 : 2026)) return null;
        var excluded = 0;
        for (var year = start.Year; year <= end.Year; year++)
        {
            var holidays = Dates(year, region);
            if (holidays is null) return null;
            excluded += holidays.Distinct().Count(d => d >= start && d <= end &&
                d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday));
        }
        var weekdays = CalendarTools.Weekdays(start,end);
        return new(weekdays, excluded, weekdays-excluded);
    }
}
