using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public sealed class LocalEclipseTests
{
    [Fact]
    public void AthensOctober2022MatchesNationalObservatoryReference()
    {
        // National Observatory of Athens, 25 Oct 2022 partial solar eclipse:
        // https://www.astro.noa.gr/news/nea/stoicheia-tis-merikis-ekleipsis-ilioy-tis-25is-oktovrioy-2022/
        var site = SkyCalendar.Sites.Single(s => s.Id == "athens");
        var eclipse = Assert.Single(SkyCalendar.LocalSolarForYear(2022,site), e => e.Peak.Utc.Month == 10);
        Assert.Equal("Partial",eclipse.Kind);
        Assert.InRange(Math.Abs((eclipse.Peak.Utc - new DateTime(2022,10,25,10,43,53,DateTimeKind.Utc)).TotalMinutes),0,3);
        Assert.InRange(eclipse.ObscuredFraction,0.25,0.28);
        Assert.InRange(eclipse.Peak.AltitudeDegrees,37,42);
        Assert.True(eclipse.Begin.Utc < eclipse.Peak.Utc && eclipse.Peak.Utc < eclipse.End.Utc);
        Assert.Equal(13,SkyCalendar.Local(eclipse.Peak.Utc,site).Hour);
    }

    [Fact]
    public void GlobalSolarEclipseIsNotNecessarilyLocal()
    {
        var site = SkyCalendar.Sites.Single(s => s.Id == "athens");
        Assert.Contains(SkyCalendar.ForYear(2024),e => e.Kind == "solar-eclipse" && e.Utc.Month == 4);
        Assert.DoesNotContain(SkyCalendar.LocalSolarForYear(2024,site),e => e.Peak.Utc.Month == 4);
    }

    [Fact]
    public void LocalEclipseSearchHonorsCancellationAndBounds()
    {
        var site = SkyCalendar.Sites[0];
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => SkyCalendar.LocalSolarForYear(2022,site,cts.Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => SkyCalendar.LocalSolarForYear(1799,site));
        var peak = SkyCalendar.LunarPeakAt(new DateTime(2025,9,7,18,11,0,DateTimeKind.Utc),site);
        Assert.InRange(peak.MoonAltitudeDegrees,0,90);
    }
}
