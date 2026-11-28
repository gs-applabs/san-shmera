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
    public sealed record SolarContact(DateTime Utc, double AltitudeDegrees);
    public sealed record LocalSolar(string Kind, double ObscuredFraction, SolarContact Begin, SolarContact Peak, SolarContact End);
    public sealed record LunarPeak(DateTime Utc, double MoonAltitudeDegrees);
    /// <summary>Local geometric eclipse circumstances, not a weather or terrain visibility forecast.
    /// Year selection is based on the local maximum expressed in UTC, like ForYear.</summary>
    public static IReadOnlyList<LocalSolar> LocalSolarForYear(int year, Site site, CancellationToken ct = default)
    {
        Validate(year);
        ct.ThrowIfCancellationRequested();
        var start = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddYears(1);
        var observer = new Observer(site.Latitude, site.Longitude, 0);
        var results = new List<LocalSolar>();
        foreach (var e in Astronomy.LocalSolarEclipsesAfter(Time(start.AddDays(-1)), observer))
        {
            ct.ThrowIfCancellationRequested();
            var peak = e.peak.time.ToUtcDateTime();
            if (peak >= end) break;
            if (peak < start) continue;
            SolarContact Contact(EclipseEvent x) => new(x.time.ToUtcDateTime(), x.altitude);
            results.Add(new(e.kind.ToString(), e.obscuration, Contact(e.partial_begin), Contact(e.peak), Contact(e.partial_end)));
        }
        return results.AsReadOnly();
    }

    /// <summary>Altitude of the Moon at the global lunar eclipse maximum only.
    /// A negative altitude does not imply the entire eclipse is invisible.</summary>
    public static LunarPeak LunarPeakAt(DateTime utc, Site site)
    {
        var observer = new Observer(site.Latitude, site.Longitude, 0);
        var t = Time(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
        var eq = Astronomy.Equator(Body.Moon, t, observer, EquatorEpoch.OfDate, Aberration.Corrected);
        var horizon = Astronomy.Horizon(t, observer, eq.ra, eq.dec, Refraction.Normal);
        return new(utc, horizon.altitude);
    }

    public sealed record VisibilityWindow(DateTime BeginUtc, DateTime EndUtc);
    public sealed record LunarPhase(string Kind, DateTime BeginUtc, DateTime EndUtc, IReadOnlyList<VisibilityWindow> AboveHorizon);
    public sealed record LocalLunar(string Kind, DateTime PeakUtc, IReadOnlyList<LunarPhase> Phases);

    /// <summary>Geometric visibility of the Moon's centre above a flat horizon (0 degrees,
    /// no refraction). Phases are approximate symmetric intervals from the engine's semi-durations.
    /// The windows are nested, not additive. Weather, daylight and terrain are not modelled.</summary>
    public static IReadOnlyList<LocalLunar> LocalLunarForYear(int year, Site site, CancellationToken ct = default)
    {
        Validate(year);
        ct.ThrowIfCancellationRequested();
        var start = new DateTime(year,1,1,0,0,0,DateTimeKind.Utc);
        var end = start.AddYears(1);
        var observer = new Observer(site.Latitude,site.Longitude,0);
        var result = new List<LocalLunar>();
        double Altitude(DateTime utc)
        {
            var time = Time(utc);
            var eq = Astronomy.Equator(Body.Moon,time,observer,EquatorEpoch.OfDate,Aberration.Corrected);
            return Astronomy.Horizon(time,observer,eq.ra,eq.dec,Refraction.None).altitude;
        }
        foreach (var eclipse in Astronomy.LunarEclipsesAfter(Time(start)))
        {
            ct.ThrowIfCancellationRequested();
            var peak = eclipse.peak.ToUtcDateTime();
            if (peak >= end) break;
            if (peak < start) continue;
            var phases = new List<LunarPhase>();
            void AddPhase(string kind,double minutes)
            {
                if (minutes <= 0) return;
                ct.ThrowIfCancellationRequested();
                var begin = peak.AddMinutes(-minutes);
                var finish = peak.AddMinutes(minutes);
                var cuts = new List<DateTime> { begin,finish };
                foreach (var direction in new[] { Direction.Rise,Direction.Set })
                {
                    var crossing = Astronomy.SearchAltitude(Body.Moon,observer,direction,Time(begin),(finish-begin).TotalDays,0)?.ToUtcDateTime();
                    if (crossing is { } hit && hit > begin && hit < finish) cuts.Add(hit);
                }
                cuts.Sort();
                var visible = new List<VisibilityWindow>();
                for (int i=1;i<cuts.Count;i++)
                {
                    var midpoint = cuts[i-1].AddTicks((cuts[i]-cuts[i-1]).Ticks/2);
                    if (Altitude(midpoint) > 0) visible.Add(new(cuts[i-1],cuts[i]));
                }
                phases.Add(new(kind,begin,finish,visible.AsReadOnly()));
            }
            AddPhase("penumbral",eclipse.sd_penum);
            AddPhase("partial",eclipse.sd_partial);
            AddPhase("total",eclipse.sd_total);
            result.Add(new(eclipse.kind.ToString(),peak,phases.AsReadOnly()));
        }
        return result.AsReadOnly();
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
        ct.ThrowIfCancellationRequested();
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
        // Planetary conjunctions/oppositions relative to the Sun, not pairwise planet alignments.
        foreach (var planet in new[] { Body.Mercury, Body.Venus, Body.Mars, Body.Jupiter, Body.Saturn, Body.Uranus, Body.Neptune })
        {
            foreach (var angle in new[] { 0.0, 180.0 })
            {
                var cursor = t;
                for (var n = 0; n < 12; n++)
                {
                    ct.ThrowIfCancellationRequested();
                    var hit = Astronomy.SearchRelativeLongitude(planet, angle, cursor);
                    if (hit.ToUtcDateTime() >= end) break;
                    var inner = planet is Body.Mercury or Body.Venus;
                    var kind = angle == 180 ? (inner ? "superior-conjunction" : "solar-conjunction") :
                        inner ? "inferior-conjunction" : "opposition";
                    Add(kind, hit, planet.ToString());
                    cursor = hit.AddDays(1);
                }
            }
        }
        return events.OrderBy(e => e.Utc).ToArray();
    }
}
