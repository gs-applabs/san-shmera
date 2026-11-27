using CosineKitty;

namespace Chronikologio.Core;

/// <summary>Local astronomical predictions; no network or data catalogue required.
/// Restricted to 1800–2100, the documented validation range for seasons.</summary>
public static class SkyCalendar
{
    public const int MinYear = 1800, MaxYear = 2100;
    public sealed record Site(string Id, double Latitude, double Longitude, string Zone);
    public static readonly IReadOnlyList<Site> Sites = Array.AsReadOnly(new[]
    {
        new Site("athens", 37.9838, 23.7275, "Europe/Athens"),
        new Site("thessaloniki", 40.6401, 22.9444, "Europe/Athens"),
        new Site("patras", 38.2466, 21.7346, "Europe/Athens"),
        new Site("heraklion", 35.3387, 25.1442, "Europe/Athens"),
        new Site("nicosia", 35.1856, 33.3823, "Asia/Nicosia"),
        new Site("limassol", 34.7071, 33.0226, "Asia/Nicosia")
    });
    public sealed record Event(string Kind, DateTime Utc, string Detail = "");
    public sealed record Day(DateTime? SunriseUtc, DateTime? SunsetUtc, DateTime? MoonriseUtc, DateTime? MoonsetUtc,
        double MoonPhaseDegrees, double IlluminatedFraction, TimeSpan? Daylight);
    private static void Validate(int year)
    {
        if (year < MinYear || year > MaxYear) throw new ArgumentOutOfRangeException(nameof(year));
    }
    private static AstroTime Time(DateTime utc) => new(utc);
    public static TimeZoneInfo Zone(Site site) => TimeZoneInfo.FindSystemTimeZoneById(site.Zone);
    public static DateTime Local(DateTime utc, Site site) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone(site));
    public static Day ForDay(DateOnly date, Site site)
    {
        Validate(date.Year);
        var zone = Zone(site);
        var start = TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), zone);
        var end = TimeZoneInfo.ConvertTimeToUtc(date.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);
        var observer = new Observer(site.Latitude, site.Longitude, 0);
        var t = Time(start);
        DateTime? RiseSet(Body body, Direction direction)
        {
            var hit = Astronomy.SearchRiseSet(body, observer, direction, t, (end - start).TotalDays, 0);
            var utc = hit?.ToUtcDateTime();
            return utc.HasValue && utc.Value >= start && utc.Value < end ? utc : null;
        }
        var sunrise = RiseSet(Body.Sun, Direction.Rise);
        var sunset = RiseSet(Body.Sun, Direction.Set);
        var noon = Time(start.AddHours((end-start).TotalHours/2));
        return new(sunrise, sunset, RiseSet(Body.Moon, Direction.Rise), RiseSet(Body.Moon, Direction.Set),
            Astronomy.MoonPhase(noon), Astronomy.Illumination(Body.Moon, noon).phase_fraction,
            sunrise.HasValue && sunset.HasValue && sunset >= sunrise ? sunset - sunrise : null);
    }
    public static IReadOnlyList<Event> ForYear(int year, CancellationToken ct = default)
    {
        Validate(year);
        var start = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddYears(1);
        var t = Time(start);
        var events = new List<Event>();
        void Add(string kind, AstroTime time, string detail = "")
        {
            ct.ThrowIfCancellationRequested();
            var utc = time.ToUtcDateTime();
            if (utc >= start && utc < end) events.Add(new(kind, utc, detail));
        }
        var s = Astronomy.Seasons(year);
        Add("march", s.mar_equinox); Add("june", s.jun_solstice);
        Add("september", s.sep_equinox); Add("december", s.dec_solstice);
        foreach (var m in Astronomy.MoonQuartersAfter(t))
        {
            ct.ThrowIfCancellationRequested();
            if (m.time.ToUtcDateTime() >= end) break;
            Add("moon-" + m.quarter, m.time);
        }
        foreach (var e in Astronomy.GlobalSolarEclipsesAfter(t))
        {
            ct.ThrowIfCancellationRequested();
            if (e.peak.ToUtcDateTime() >= end) break;
            Add("solar-eclipse", e.peak, e.kind.ToString());
        }
        foreach (var e in Astronomy.LunarEclipsesAfter(t))
        {
            ct.ThrowIfCancellationRequested();
            if (e.peak.ToUtcDateTime() >= end) break;
            Add("lunar-eclipse", e.peak, e.kind.ToString());
        }
        return events.OrderBy(e => e.Utc).ToArray();
    }
}
