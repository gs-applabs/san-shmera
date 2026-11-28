using Chronikologio.Core;
using Opsis.UI;

namespace Chronikologio.UI;

public sealed class AstronomyPage : TimeToolsPage
{
    private readonly DateTimePicker _date;
    private readonly FlatComboBox _site = new() { Width = 180, Height = 32 };
    private readonly ListView _daily, _annual;
    private readonly MoonPhaseVisual _moon = new() { Height = 96, Width = 100, Tag = "card" };
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
        Add(_moon, 96);
        Add(_daily, 204);
        _calculate = Button(T("Υπολογισμός έτους", "Calculate year"), 185);
        Add(Row(_calculate, _status), 48);
        _status.Width = 430; _status.Height = 32;
        _annual = Table((T("Φαινόμενο", "Event"), 280), (T("Μέγιστο / χρόνος UTC", "Peak / time UTC"), 240), (T("Τύπος / ύψος / κάλυψη", "Type / altitude / coverage"), 230));
        Add(_annual, 340);
        AddLabel(T("Εκλείψεις: παγκόσμια και τοπικά στοιχεία σε UTC. Αρνητικό ύψος = κάτω από τον ορίζοντα στη συγκεκριμένη στιγμή. Τοπικά σεληνιακά διαστήματα: κέντρο Σελήνης πάνω από επίπεδο ορίζοντα, κατά προσέγγιση. Δεν συνυπολογίζονται φωτεινότητα ουρανού, καιρός ή ανάγλυφο. Παρατήρηση Ήλιου μόνο με πιστοποιημένο φίλτρο.", "Eclipses: global and local circumstances in UTC. Negative altitude means below horizon at that instant. Local lunar windows approximate the Moon centre above a flat horizon; sky brightness, weather and terrain are excluded. Use certified solar eye protection."), 112, "muted");
        AddLabel("Astronomy Engine 2.1.19 · Don Cross · MIT · github.com/cosinekitty/astronomy", 32, "muted");
        _date.ValueChanged += (_, _) => { ResetAnnual(); RefreshDay(); };
        _site.SelectedIndexChanged += (_, _) => { ResetAnnual(); RefreshDay(); };
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
            _moon.SetPhase(day.MoonPhaseDegrees, day.IlluminatedFraction);
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
        "solar-eclipse" => T("Παγκόσμια ηλιακή έκλειψη", "Global solar eclipse"), "lunar-eclipse" => T("Σεληνιακή — παγκόσμιο μέγιστο", "Lunar — global peak"),
        "solar-conjunction" => T("Σύνοδος εξωτερικού πλανήτη με Ήλιο", "Outer planet solar conjunction"),
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
        var site = SkyCalendar.Sites[Math.Max(0,_site.SelectedIndex)];
        _status.Text = T("Υπολογισμός…", "Calculating…");
        try
        {
            var result = await Task.Run(() =>
            {
                var events = SkyCalendar.ForYear(year, cts.Token);
                var rows = new List<(DateTime Utc, string[] Cells)>();
                foreach (var e in events)
                {
                    cts.Token.ThrowIfCancellationRequested();
                    var detail = Detail(e.Detail);
                    if (e.Kind == "lunar-eclipse")
                    {
                        var peak = SkyCalendar.LunarPeakAt(e.Utc, site);
                        detail += $" · {peak.MoonAltitudeDegrees:F1}°";
                    }
                    rows.Add((e.Utc, new[] { EventName(e.Kind), e.Utc.ToString("dd/MM/yyyy HH:mm"), detail }));
                }
                foreach (var eclipse in SkyCalendar.LocalLunarForYear(year, site, cts.Token))
                {
                    foreach (var phase in eclipse.Phases)
                    {
                        var name = phase.Kind switch
                        {
                            "total" => T("Ολικότητα", "Totality"),
                            "partial" => T("Φάση σκιάς", "Umbral phase"),
                            _ => T("Φάση παρασκιάς", "Penumbral phase")
                        };
                        if (phase.AboveHorizon.Count == 0)
                            rows.Add((phase.BeginUtc,new[] { name, phase.BeginUtc.ToString("dd/MM/yyyy HH:mm"), T("Κάτω από ορίζοντα", "Below horizon") }));
                        foreach (var window in phase.AboveHorizon)
                            rows.Add((window.BeginUtc,new[] { name + T(" — τοπικά", " — local"), window.BeginUtc.ToString("dd/MM/yyyy HH:mm"), T("Έως ", "Until ") + window.EndUtc.ToString("dd/MM HH:mm") }));
                    }
                }
                foreach (var eclipse in SkyCalendar.LocalSolarForYear(year, site, cts.Token))
                {
                    var prefix = T("Τοπική ηλιακή — ", "Local solar — ");
                    void Contact(string name, SkyCalendar.SolarContact contact, string extra = "")
                        => rows.Add((contact.Utc, new[] { prefix + name, contact.Utc.ToString("dd/MM/yyyy HH:mm"), $"{contact.AltitudeDegrees:F1}°" + extra }));
                    Contact(T("έναρξη", "start"), eclipse.Begin);
                    Contact(T("μέγιστο", "peak"), eclipse.Peak, " · " + eclipse.ObscuredFraction.ToString("P1") + " · " + Detail(eclipse.Kind));
                    Contact(T("λήξη", "end"), eclipse.End);
                }
                return (Rows: rows.OrderBy(x => x.Utc).Select(x => x.Cells).ToArray(), Count: events.Count);
            }, cts.Token);
            if (!cts.IsCancellationRequested && !IsDisposed && !Disposing && _date.Value.Year == year && SkyCalendar.Sites[Math.Max(0,_site.SelectedIndex)] == site)
            {
                Rows(_annual, result.Rows);
                _status.Text = $"{year} · {SiteName(site.Id)} · {result.Count} " + T("παγκόσμια φαινόμενα", "global events");
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
