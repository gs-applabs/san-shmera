using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public sealed class TimeToolsTests
{
    [Theory]
    [InlineData("julian", 2026, 3, 25, 2026, 3, 12)]
    [InlineData("hebrew", 2024, 10, 3, 5785, 1, 1)]
    [InlineData("persian", 2024, 3, 20, 1403, 1, 1)]
    public void KnownConversionsRoundTrip(string system, int y, int m, int d, int cy, int cm, int cd)
    {
        var date = new DateOnly(y,m,d);
        Assert.Equal(new CalendarTools.CalendarDate(system,cy,cm,cd), CalendarTools.Convert(date,system));
        Assert.Equal(date, CalendarTools.ToGregorian(system,cy,cm,cd));
    }

    [Fact]
    public void DateIntervalsAndLeapAnniversaries()
    {
        var first = new DateOnly(2024,2,29);
        var last = new DateOnly(2025,2,28);
        Assert.Equal(new CalendarTools.Interval(365,1,0,0), CalendarTools.Between(first,last));
        Assert.Equal(-365, CalendarTools.Between(last,first).TotalDays);
        Assert.Equal(last,CalendarTools.NextAnniversary(first,last));
        Assert.Equal(new DateOnly(2026,2,28),CalendarTools.NextAnniversary(first,last.AddDays(1)));
        Assert.Equal((2020,53), CalendarTools.IsoWeek(new DateOnly(2021,1,1)));
        Assert.Equal(5,CalendarTools.Weekdays(new DateOnly(2024,1,1),new DateOnly(2024,1,7)));
    }

    [Fact]
    public void AnniversaryBeyondDateOnlyMaximumReturnsNoResult()
    {
        Assert.Null(CalendarTools.TryNextAnniversary(new DateOnly(2000,1,1),DateOnly.MaxValue));
        Assert.Equal(DateOnly.MaxValue,CalendarTools.TryNextAnniversary(new DateOnly(2000,12,31),DateOnly.MaxValue));
        Assert.Equal(new DateOnly(9999,2,28),CalendarTools.TryNextAnniversary(new DateOnly(2000,2,29),new DateOnly(9999,1,1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => CalendarTools.NextAnniversary(new DateOnly(2000,1,1),DateOnly.MaxValue));
    }

    [Fact]
    public void AstronomyMatchesIndependentReferenceEvents()
    {
        // USNO seasons 2024, USNO primary phases Apr 2024, NASA eclipse Besselian elements.
        var events = SkyCalendar.ForYear(2024);
        var equinox = Assert.Single(events.Where(e => e.Kind == "march"));
        Assert.InRange(Math.Abs((equinox.Utc - new DateTime(2024,3,20,3,6,0,DateTimeKind.Utc)).TotalMinutes),0,3);
        var eclipse = Assert.Single(events.Where(e => e.Kind == "solar-eclipse" && e.Utc.Month == 4));
        Assert.Equal("Total", eclipse.Detail);
        Assert.InRange(Math.Abs((eclipse.Utc - new DateTime(2024,4,8,18,17,15,DateTimeKind.Utc)).TotalMinutes),0,4);
        var moon = Assert.Single(events.Where(e => e.Kind == "moon-0" && e.Utc.Month == 4));
        Assert.InRange(Math.Abs((moon.Utc - new DateTime(2024,4,8,18,21,0,DateTimeKind.Utc)).TotalMinutes),0,4);
        Assert.InRange(events.Count,65,110);
        Assert.Contains(events, e => e.Kind == "opposition" && e.Detail == "Jupiter");
        Assert.Contains(events, e => e.Kind == "inferior-conjunction" && e.Detail == "Mercury");
        Assert.Equal(events.OrderBy(e=>e.Utc).ToArray(),events.ToArray());
    }

    [Fact]
    public void AthensSummerHasLongerDayAndLocalTimesStayWithinDate()
    {
        var site = SkyCalendar.Sites.Single(s => s.Id == "athens");
        var summer = new DateOnly(2024,6,21); var winter = new DateOnly(2024,12,21);
        var a = SkyCalendar.ForDay(summer,site); var b = SkyCalendar.ForDay(winter,site);
        Assert.NotNull(a.SunriseUtc); Assert.NotNull(a.SunsetUtc);
        Assert.Equal(summer,DateOnly.FromDateTime(SkyCalendar.Local(a.SunriseUtc!.Value,site)));
        Assert.Equal(summer,DateOnly.FromDateTime(SkyCalendar.Local(a.SunsetUtc!.Value,site)));
        Assert.InRange(a.Daylight!.Value.TotalHours,14,16);
        Assert.InRange(b.Daylight!.Value.TotalHours,9,11);
        Assert.InRange(a.IlluminatedFraction,0,1);
        Assert.Throws<ArgumentOutOfRangeException>(() => SkyCalendar.ForYear(1799));
    }
}
