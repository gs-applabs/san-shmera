using Chronikologio.Core;
using Opsis.UI;

namespace Chronikologio.UI;

public sealed class AstronomyPage : TimeToolsPage
{
    private readonly DateTimePicker _date;
    private readonly FlatComboBox _site = new() { Width = 180, Height = 32 };
    private readonly ListView _daily, _annual;
    private readonly Label _status = new() { TextAlign = ContentAlignment.MiddleLeft, Tag = "muted" };
    private readonly FlatButton _calculate;
    private CancellationTokenSource? _work;
    public static string SiteName(string id) => id switch
    {
        "athens" => T("Αθήνα", "Athens"), "thessaloniki" => T("Θεσσαλονίκη", "Thessaloniki"),
        "patras" => T("Πάτρα", "Patras"), "heraklion" => T("Ηράκλειο", "Heraklion"),
        "nicosia" => T("Λευκωσία", "Nicosia"), "limassol" => T("Λεμεσός", "Limassol"),
        _ => id
    };
    public AstronomyPage(DateOnly selected, string siteId) : base(T("Αστρονομικό ημερολόγιο", "Astronomical calendar"),
        T("Τοπικοί υπολογισμοί 1800–2100 · Ήλιος, Σελήνη, εκλείψεις και πλανήτες.", "Offline predictions 1800–2100 · Sun, Moon, eclipses and planets."))
    {
        _date = Picker(selected, SkyCalendar.MinYear, SkyCalendar.MaxYear);
        _date.AccessibleName = T("Ημερομηνία", "Date");
        _site.AccessibleName = T("Τόπος παρατήρησης", "Observation site");
        _site.SetItems(SkyCalendar.Sites.Select(s => SiteName(s.Id)), Math.Max(0, SkyCalendar.Sites.ToList().FindIndex(s => s.Id == siteId)));
        Add(Row(Field(T("Ημερομηνία", "Date")), _date, Field(T("Τόπος", "Site"), 55), _site), 50);
        _daily = Table((T("Ημερήσιο φαινόμενο", "Daily event"), 290), (T("Τοπική ώρα / τιμή", "Local time / value"), 280));
        Add(_daily, 204);
        _calculate = Button(T("Υπολογισμός έτους", "Calculate year"), 185);
        Add(Row(_calculate, _status), 48);
        _status.Width = 430; _status.Height = 32;
        _annual = Table((T("Φαινόμενο", "Event"), 280), (T("Μέγιστο / χρόνος UTC", "Peak / time UTC"), 240), (T("Τύπος", "Type"), 115));
        Add(_annual, 340);
        AddLabel(T("Εκλείψεις: παγκόσμιο μέγιστο, όχι εγγύηση τοπικής ορατότητας. Οι ανατολές/δύσεις είναι προσεγγίσεις για ορίζοντα στο επίπεδο της θάλασσας. Δεν χρησιμοποιείται για ναυσιπλοΐα.", "Eclipses: global peak, not local visibility. Rise/set predictions assume a sea-level horizon. Not for navigation."), 65, "muted");
        AddLabel("Astronomy Engine 2.1.19 · Don Cross · MIT · github.com/cosinekitty/astronomy", 32, "muted");
        _date.ValueChanged += (_, _) => { ResetAnnual(); RefreshDay(); };
        _site.SelectedIndexChanged += (_, _) => RefreshDay();
        _calculate.Click += async (_, _) => await CalculateYear();
        RefreshDay(); Restyle();
    }
    private void ResetAnnual()
    {
        _work?.Cancel();
        _annual.Items.Clear();
        _status.Text = "";
    }
    private void RefreshDay()
    {
        var site = SkyCalendar.Sites[Math.Max(0,_site.SelectedIndex)];
        try
        {
            var day = SkyCalendar.ForDay(DateOnly.FromDateTime(_date.Value), site);
            string Format(DateTime? t) => t is null ? T("Δεν συμβαίνει σήμερα", "No event today") : SkyCalendar.Local(t.Value, site).ToString("HH:mm");
            Rows(_daily, new[]
            {
                new[] { T("Ανατολή Ηλίου", "Sunrise"), Format(day.SunriseUtc) },
                new[] { T("Δύση Ηλίου", "Sunset"), Format(day.SunsetUtc) },
                new[] { T("Διάρκεια ημέρας", "Daylight"), day.Daylight?.ToString(@"hh\:mm") ?? "—" },
                new[] { T("Ανατολή Σελήνης", "Moonrise"), Format(day.MoonriseUtc) },
                new[] { T("Δύση Σελήνης", "Moonset"), Format(day.MoonsetUtc) },
                new[] { T("Φωτισμένο ποσοστό Σελήνης", "Moon illuminated"), day.IlluminatedFraction.ToString("P1", Loc.Culture) },
                new[] { T("Γωνία φάσης (0° νέα / 180° πανσέληνος)", "Phase angle (0° new / 180° full)"), day.MoonPhaseDegrees.ToString("F1",Loc.Culture) + "°" }
            });
        }
        catch (Exception ex) when (ex is ArgumentException or TimeZoneNotFoundException or InvalidTimeZoneException)
        { Rows(_daily, new[] { new[] { T("Μη διαθέσιμος υπολογισμός", "Calculation unavailable"), ex.Message } }); }
    }
    private static string EventName(string kind) => kind switch
    {
        "march" => T("Εαρινή ισημερία", "March equinox"), "june" => T("Θερινό ηλιοστάσιο", "June solstice"),
        "september" => T("Φθινοπωρινή ισημερία", "September equinox"), "december" => T("Χειμερινό ηλιοστάσιο", "December solstice"),
        "moon-0" => T("Νέα Σελήνη", "New moon"), "moon-1" => T("Πρώτο τέταρτο", "First quarter"),
        "moon-2" => T("Πανσέληνος", "Full moon"), "moon-3" => T("Τελευταίο τέταρτο", "Last quarter"),
        "solar-eclipse" => T("Ηλιακή έκλειψη", "Solar eclipse"), "lunar-eclipse" => T("Σεληνιακή έκλειψη", "Lunar eclipse"),
        "superior-conjunction" => T("Ανώτερη σύνοδος με Ήλιο", "Superior conjunction"),
        "inferior-conjunction" => T("Κατώτερη σύνοδος με Ήλιο", "Inferior conjunction"),
        "opposition" => T("Αντίθεση πλανήτη", "Planetary opposition"), _ => kind
    };
    private static string Detail(string kind) => kind switch
    {
        "Total" => T("Ολική", "Total"), "Partial" => T("Μερική", "Partial"), "Annular" => T("Δακτυλιοειδής", "Annular"),
        "Penumbral" => T("Παρασκιάς", "Penumbral"),
        "Mercury" => T("Ερμής", "Mercury"), "Venus" => T("Αφροδίτη", "Venus"), "Mars" => T("Άρης", "Mars"),
        "Jupiter" => T("Δίας", "Jupiter"), "Saturn" => T("Κρόνος", "Saturn"), "Uranus" => T("Ουρανός", "Uranus"),
        "Neptune" => T("Ποσειδώνας", "Neptune"), _ => kind
    };
    private async Task CalculateYear()
    {
        if (_work is not null) return;
        using var cts = new CancellationTokenSource();
        _work = cts; _calculate.Enabled = false;
        int year = _date.Value.Year;
        _status.Text = T("Υπολογισμός…", "Calculating…");
        try
        {
            var events = await Task.Run(() => SkyCalendar.ForYear(year, cts.Token), cts.Token);
            if (!cts.IsCancellationRequested && !IsDisposed && !Disposing && _date.Value.Year == year)
            {
                Rows(_annual, events.Select(e => new[] { EventName(e.Kind), e.Utc.ToString("dd/MM/yyyy HH:mm"), Detail(e.Detail) }));
                _status.Text = $"{year} · {events.Count} " + T("φαινόμενα", "events");
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        { if (!IsDisposed && !Disposing) _status.Text = T("Αποτυχία υπολογισμού: ", "Calculation failed: ") + ex.Message; }
        finally { _work = null; if (!IsDisposed && !Disposing) _calculate.Enabled = true; }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) _work?.Cancel();
        base.Dispose(disposing);
    }
}
