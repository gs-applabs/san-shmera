using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public sealed class WorkdayCalendarTests
{
    [Fact]
    public void GreeceCountsOnlyNationwideMandatoryHolidays()
    {
        var days = WorkdayCalendar.Dates(2026, "gr")!;
        Assert.Equal(9,days.Count);
        Assert.Contains(new DateOnly(2026,4,13),days);
        Assert.DoesNotContain(new DateOnly(2026,2,23),days); // Clean Monday not universally mandatory
        Assert.DoesNotContain(new DateOnly(2026,6,1),days); // Whit Monday likewise
        Assert.Equal(new WorkdayCalendar.Result(261,7,254),WorkdayCalendar.Between(new(2026,1,1),new(2026,12,31),"gr"));
        Assert.Null(WorkdayCalendar.Between(new(2026,12,31),new(2027,1,1),"gr"));
    }

    [Fact]
    public void CyprusBankHolidaysDoNotCountWeekendTwice()
    {
        Assert.Equal(new WorkdayCalendar.Result(261,13,248),WorkdayCalendar.Between(new(2026,1,1),new(2026,12,31),"cy"));
        Assert.Equal(new WorkdayCalendar.Result(261,11,250),WorkdayCalendar.Between(new(2027,1,1),new(2027,12,31),"cy"));
        Assert.Equal(new WorkdayCalendar.Result(2,2,0),WorkdayCalendar.Between(new(2027,5,4),new(2027,5,3),"cy"));
        Assert.Null(WorkdayCalendar.Between(new(2025,12,31),new(2026,1,1),"cy"));
        Assert.Null(WorkdayCalendar.Between(new(2026,1,1),new(2026,1,2),"invalid"));
    }
}
