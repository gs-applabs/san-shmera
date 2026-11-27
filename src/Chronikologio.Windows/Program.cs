using Chronikologio.UI;

namespace Chronikologio.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var window = new MainWindow(Path.Combine(AppContext.BaseDirectory, "data"));
        using var paintAudit = PaintAudit.AttachIfRequested(window);
        // the icon of the executable (chronikologio.ico) for the title bar and the taskbar
        try { if (Environment.ProcessPath is { } exe) window.Icon = Icon.ExtractAssociatedIcon(exe); }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException) { }
        // The isolated diagnostics mode suppresses unrelated network I/O; ordinary startup is unchanged.
        if (Environment.GetEnvironmentVariable("CHRON_PAINT_AUDIT") is null ||
            Environment.GetEnvironmentVariable("CHRON_AUDIT_NO_UPDATES") != "1") window.EnableUpdates();
        // The lines of "On this day" for the News Crawler when it runs without Opsis: a small file, written only when its content changes.
        // Keep only the newest queued request; writes from this process are serialized.
        using var publisher = new Chronikologio.Core.LatestWork<Chronikologio.Core.DayStore>(
            store => Chronikologio.Core.FeedExchange.TryWrite(store));
        window.PublishFeed = publisher.Submit;
        var publishedDate = DateOnly.FromDateTime(DateTime.Today);
        using var dayTimer = new System.Windows.Forms.Timer { Interval = 60_000 };
        dayTimer.Tick += (_, _) =>
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (today == publishedDate) return;
            publishedDate = today;
            window.PublishFeedNow();
        };
        window.PublishFeedNow();
        dayTimer.Start();
        Application.Run(window);
        dayTimer.Stop();
        window.PublishFeed = null;
        // LatestWork drains at most the current and newest pending value on dispose.
    }
}
