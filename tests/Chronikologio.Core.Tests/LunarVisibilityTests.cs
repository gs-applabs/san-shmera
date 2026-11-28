using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public sealed class LunarVisibilityTests
{
    [Fact]
    public void September2025ContactsAgreeWithNasaAndAthensHasVisibleTotality()
    {
        // https://eclipse.gsfc.nasa.gov/OH/OH2025.html — rounded UT contacts.
        var site = SkyCalendar.Sites.Single(x => x.Id == "athens");
        var e = Assert.Single(SkyCalendar.LocalLunarForYear(2025,site), x => x.PeakUtc.Month == 9);
        Assert.Equal("Total",e.Kind);
        var total = Assert.Single(e.Phases,x => x.Kind == "total");
        Assert.InRange(Math.Abs((total.BeginUtc-new DateTime(2025,9,7,17,31,0,DateTimeKind.Utc)).TotalMinutes),0,4);
        Assert.InRange(Math.Abs((total.EndUtc-new DateTime(2025,9,7,18,53,0,DateTimeKind.Utc)).TotalMinutes),0,4);
        var visible = Assert.Single(total.AboveHorizon);
        Assert.Equal(total.BeginUtc,visible.BeginUtc);
        Assert.Equal(total.EndUtc,visible.EndUtc);
        var penumbra = Assert.Single(e.Phases,x => x.Kind == "penumbral");
        var local = Assert.Single(penumbra.AboveHorizon);
        Assert.True(local.BeginUtc > penumbra.BeginUtc);
        Assert.Equal(penumbra.EndUtc,local.EndUtc);
    }

    [Fact]
    public void WindowsNeverExtendBeyondTheirPhaseAndCancellationIsHonored()
    {
        foreach (var site in SkyCalendar.Sites)
            foreach (var eclipse in SkyCalendar.LocalLunarForYear(2025,site))
                foreach (var phase in eclipse.Phases)
                    foreach (var window in phase.AboveHorizon)
                    {
                        Assert.True(window.BeginUtc >= phase.BeginUtc);
                        Assert.True(window.EndUtc <= phase.EndUtc);
                        Assert.True(window.BeginUtc < window.EndUtc);
                    }
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => SkyCalendar.LocalLunarForYear(2025,SkyCalendar.Sites[0],cts.Token));
    }

    [Fact]
    public void ExteriorPlanetsHaveSolarConjunctionNotSuperiorConjunction()
    {
        var events = SkyCalendar.ForYear(2024);
        Assert.Contains(events,x => x.Kind == "solar-conjunction" && x.Detail == "Jupiter");
        Assert.DoesNotContain(events,x => x.Kind == "superior-conjunction" && x.Detail == "Jupiter");
        Assert.Contains(events,x => x.Kind == "superior-conjunction" && x.Detail == "Mercury");
    }
}
