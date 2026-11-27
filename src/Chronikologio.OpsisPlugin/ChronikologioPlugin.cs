using Chronikologio.Core;
using Chronikologio.UI;
using OpsisNewsCrawler.Plugin.Abstractions;

namespace Chronikologio.OpsisPlugin;

/// <summary>
/// Opsis plugin: offers the calendar widget (replacing the built-in one) and the "Σαν σήμερα" feed for the bars of the News Crawler.
/// </summary>
public sealed class ChronikologioPlugin : IOpsisNewsCrawlerPlugin, IPluginWidgetProvider, IPluginUpdateSource, IPluginHostSettingsAware
{
    public const string FeedKey = "san-simera";

    private DailySnapshotService? _service;
    private string _language = "el";
    private readonly object _lifecycle = new();
    private bool _started;

    /// <summary>True when Opsis itself manages the updates of its plugins: the plugin then runs no update check of its own and shows no update card.</summary>
    public bool HostManagesUpdates { get; private set; }

    // Contract 1.0.0.0: where the update button of Opsis looks for new versions (the release asset is a zip of the plugin folder).
    public PluginUpdateSource GetUpdateSource() => new("gs-applabs/san-shmera", "Chronikologio-Plugin.zip");

    // Contract 1.0.0.0: the settings of the host. The interface language follows Opsis (the language of the contents, gr/cy x el/en, stays the plugin's own).
    public void SetHostSettings(PluginHostSettings settings)
    {
        HostManagesUpdates = settings.HostManagesUpdates;
        if (!string.IsNullOrEmpty(settings.Language)) SetLanguage(settings.Language);
    }

    public string Id => "chronikologio";
    public string Name => "Χρονικολόγιο — Timeline Calendar";
    public string Version => typeof(ChronikologioPlugin).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";

    public event Action<string>? NavigationRequested { add { } remove { } }

    // The calendar (Greece or Cyprus) is the one chosen in the stand-alone app's View menu; it is stored in the same settings file.
    // The service reads the data and makes the lines of the day on a background thread, so the bars never wait for the files.
    private DailySnapshotService Service => _service ??= new DailySnapshotService(
        Path.Combine(Path.GetDirectoryName(typeof(ChronikologioPlugin).Assembly.Location) ?? AppContext.BaseDirectory, "data"),
        UiSettings.Load().Region);

    public void Start()
    {
        lock (_lifecycle)
        {
        if (_started) return;
        _started = true;
        PluginLog.Write($"Start: plugin {Version}, language {_language}, host contract {typeof(PluginFeeds).Assembly.Location}");
        var service = Service;
        PluginFeeds.Register(new PluginFeed(FeedKey, "Σαν σήμερα", "On this day", BuildFeed));
        // a new snapshot (midnight, another country, another language): register the feed again, which tells the bars to ask for the lines again
        service.Changed += () => 
        {
            PluginLog.Write($"snapshot ready: {service.Current?.Items.Count} lines for {service.Current?.Date}, region {service.Current?.Region}; feeds registered now: {PluginFeeds.All().Count}");
            lock (_lifecycle)
            {
                if (!_started || !ReferenceEquals(_service, service)) return;
                PluginFeeds.Register(new PluginFeed(FeedKey, "Σαν σήμερα", "On this day", BuildFeed));
            }
            FeedExchange.TryWrite(service.Store);   // for the News Crawler when it is not in this process (same content = no write)
        };
        service.Start();
        PluginLog.Write($"feed '{FeedKey}' registered; feeds known to the host: {string.Join(", ", PluginFeeds.All().Select(f => f.Key))}");
        }
    }

    public void Stop()
    {
        lock (_lifecycle)
        {
            _started = false;
            PluginFeeds.Unregister(FeedKey);
            _service?.Dispose();
            _service = null;
            _cached = null;
            _asked = 0;
        }
    }

    public void Pause() { }
    public void Resume() { }
    public void Refresh() => _service?.Refresh();
    public void SetMode(string mode) { }
    public void SetPosition(string position) { }
    public void SetLanguage(string code)
    {
        _language = code;
        if (Loc.Set(code)) _service?.LanguageChanged();   // the interface language follows Opsis when the app has that language; otherwise it keeps its own
    }

    public void SetTheme(bool dark, int background, int surface, int border, int text, int textMuted, int accent, double opacity)
    {
        // The widget follows the colours of Opsis; every open calendar listens to the theme and restyles itself.
        ChronikologioTheme.SetFromHost(dark, background, surface, border, text, textMuted, accent);
    }

    public IReadOnlyList<PluginWidget> GetWidgets() => new[]
    {
        new PluginWidget("calendar", "Ημερολόγιο", "Calendar", CreateCalendar, ReplacesBuiltIn: "calendar")
    };

    public IReadOnlyList<PluginNavigationItem> GetNavigationItems() => Array.Empty<PluginNavigationItem>();
    public IReadOnlyList<PluginMenuItem> GetTrayMenuItems() => Array.Empty<PluginMenuItem>();

    public PluginAboutInfo GetAboutInfo() => new(
        Name, Version,
        "Εορτολόγιο, ημερολόγιο και «Σαν σήμερα» με βίους αγίων, ετυμολογίες ονομάτων και ιστορικά γεγονότα. Αντικαθιστά το ενσωματωμένο ημερολόγιο και προσθέτει τη ροή «Σαν σήμερα» στις μπάρες.",
        "Name days, calendar and \"on this day\" with saints' lives, name etymologies and history. Replaces the built-in calendar and adds the \"On this day\" feed to the bars.",
        "Χρονικολόγιο", "—",
        new[]
        {
            new PluginAboutSection("Δεδομένα", "Data",
                "Οι εγγραφές έχουν γραφτεί από την αρχή και δείχνουν ποια κατάσταση επαλήθευσης έχουν. Στο τέλος κάθε κειμένου αναφέρονται οι πηγές."),
            new PluginAboutSection("Ενημερώσεις", "Updates",
                "Ως plugin, το Χρονικολόγιο ενημερώνεται μόνο από το κουμπί ενημέρωσης του Opsis, όχι από δικό του έλεγχο. Οι εκδόσεις βρίσκονται στο github.com/giorstergiopoulos-design/san-shmera (releases) με αρχείο SHA-256· ο εγκαταστάτης Setup ενημερώνει εφαρμογή και plugin μαζί, και ο Opsis μπορεί να ζητήσει νέα έγκριση για το ενημερωμένο plugin.")
        });

    private Control CreateCalendar()
    {
        var panel = new CalendarPanel(Service.Store);
        panel.StoreChanged += store => Service.SetRegion(store.Region);   // the bar feed follows the country chosen with the flag
        return panel;
    }

    // Called by the bars on the UI thread: only reads the ready snapshot (empty for the first moments after Start).
    private int _asked;
    private sealed record CachedFeed(DailySnapshot Snapshot, IReadOnlyList<PluginFeedItem> Items);
    private volatile CachedFeed? _cached;

    private IReadOnlyList<PluginFeedItem> BuildFeed()
    {
        if (Interlocked.Increment(ref _asked) <= 3) PluginLog.Write($"the bars asked for the lines (time {_asked}); snapshot: {(_service?.Current is { } c ? c.Items.Count + " lines" : "not ready")}");
        var service = _service;
        if (service is null) return Array.Empty<PluginFeedItem>();
        service.CheckDate();
        var snapshot = service.Current;
        if (snapshot is null) return Array.Empty<PluginFeedItem>();
        var cache = _cached;
        if (cache is not null && ReferenceEquals(cache.Snapshot, snapshot)) return cache.Items;
        var items = Array.AsReadOnly(snapshot.Items.Select(i => new PluginFeedItem(i.Text)).ToArray());
        _cached = new CachedFeed(snapshot, items);
        return items;
    }
}