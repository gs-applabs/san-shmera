namespace Chronikologio.Core;

/// <summary>The "On this day" lines of one date, country and language, ready to be shown. Immutable: a new snapshot replaces it, it is never changed.</summary>
public sealed record DailySnapshot(DateOnly Date, string Region, string Language, IReadOnlyList<FeedItem> Items, DateTimeOffset GeneratedAt);

/// <summary>
/// Makes the snapshot of the day on a background thread, so that the bars of the News Crawler (which ask for the lines on the UI thread and need them at once)
/// never wait for the data files. The snapshot is made when the service starts, again at local midnight, when the country or the language changes and on
/// <see cref="Refresh"/>; <see cref="Changed"/> is raised after each new snapshot. <see cref="Current"/> is null until the first one is ready.
/// </summary>
public sealed class DailySnapshotService : IDisposable
{
    private readonly string _dataRoot;
    private readonly Func<DateOnly> _today;
    private readonly object _gate = new();
    private string _region;
    private DayStore? _store;
    private volatile DailySnapshot? _current;
    private System.Threading.Timer? _midnight;
    private volatile bool _disposed;
    private readonly object _workGate = new();
    private bool _workerRunning, _pending, _pendingReload;

    public DailySnapshotService(string dataRoot, string region, Func<DateOnly>? today = null)
    {
        _dataRoot = dataRoot;
        _region = region;
        _today = today ?? (() => DateOnly.FromDateTime(DateTime.Now));
    }

    /// <summary>Raised on a background thread after a new snapshot is ready.</summary>
    public event Action? Changed;

    /// <summary>The newest snapshot, or null while the first one is being made.</summary>
    public DailySnapshot? Current => _current;

    /// <summary>The country of the snapshots (the calendar).</summary>
    public string Region { get { lock (_gate) return _region; } }

    /// <summary>The loaded database; waits for the first load (only for the widget, which needs the store itself and is created once).</summary>
    public DayStore Store
    {
        get
        {
            lock (_gate) return _store ??= DayStore.Load(_dataRoot, _region);
        }
    }

    /// <summary>Starts the first snapshot and the midnight timer. Returns at once.</summary>
    public void Start()
    {
        Rebuild(reload: true);
        ScheduleMidnight();
    }

    /// <summary>The user chose another country: new database, new snapshot.</summary>
    public void SetRegion(string region)
    {
        lock (_gate)
        {
            if (region == _region) return;
            _region = region;
            _store = null;
        }
        Rebuild(reload: true);
    }

    /// <summary>The interface language changed: the same data, new texts.</summary>
    public void LanguageChanged() => Rebuild(reload: false);

    /// <summary>Reads the data files again (and makes a new snapshot).</summary>
    public void Refresh()
    {
        lock (_gate) _store = null;
        Rebuild(reload: true);
    }

    /// <summary>If the date of the snapshot is not today's (the clock moved, the computer slept over midnight) a new snapshot is started. Cheap; call it often.</summary>
    public void CheckDate()
    {
        var snapshot = _current;
        if (snapshot is not null && snapshot.Date != _today()) Rebuild(reload: false, onlyIfIdle: true);
    }

    /// <summary>Makes a snapshot now, on the calling thread (for the tests and for callers that can wait).</summary>
    public DailySnapshot BuildNow(bool reload = false)
    {
        DailySnapshot snapshot;
        lock (_gate)
        {
            if (reload) _store = null;
            _store ??= DayStore.Load(_dataRoot, _region);
            var date = _today();
            snapshot = new DailySnapshot(date, _region, Loc.Language, SanSimeraFeed.BuildForTicker(_store, date), DateTimeOffset.Now);
            _current = snapshot;
        }
        if (!_disposed) Changed?.Invoke();
        return snapshot;
    }

    private void Rebuild(bool reload, bool onlyIfIdle = false)
    {
        lock (_workGate)
        {
            if (_disposed || (onlyIfIdle && (_workerRunning || _current?.Date == _today()))) return;
            _pending = true;
            _pendingReload |= reload;
            if (_workerRunning) return;
            _workerRunning = true;
        }
        _ = Task.Run(ProcessPending);
    }

    private void ProcessPending()
    {
        while (true)
        {
            bool reload;
            lock (_workGate)
            {
                if (_disposed || !_pending)
                {
                    _workerRunning = false;
                    return;
                }
                reload = _pendingReload;
                _pending = _pendingReload = false;
            }
            try { if (!_disposed) BuildNow(reload); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                System.Diagnostics.Debug.WriteLine("Chronikologio snapshot failed: " + ex.Message);
            }
        }
    }

    private void ScheduleMidnight()
    {
        lock (_workGate)
        {
            if (_disposed) return;
            var now = DateTime.Now;
            var due = now.Date.AddDays(1).AddSeconds(5) - now;
            _midnight?.Dispose();
            _midnight = new System.Threading.Timer(_ =>
            {
                Rebuild(reload: false);
                ScheduleMidnight();
            }, null, due, Timeout.InfiniteTimeSpan);
        }
    }

    public void Dispose()
    {
        lock (_workGate)
        {
            _disposed = true;
            _pending = _pendingReload = false;
            _midnight?.Dispose();
            _midnight = null;
        }
    }
}
