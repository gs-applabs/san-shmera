using Chronikologio.UI;

namespace Chronikologio.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        var window = new MainWindow(Path.Combine(AppContext.BaseDirectory, "data"));
        // the icon of the executable (chronikologio.ico) for the title bar and the taskbar
        try { if (Environment.ProcessPath is { } exe) window.Icon = Icon.ExtractAssociatedIcon(exe); }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException) { }
        window.EnableUpdates();   // only the stand-alone app: inside Opsis the plugin is updated by Opsis
        // The lines of "On this day" for the News Crawler when it runs without Opsis: a small file, written only when its content changes.
        // Keep only the newest queued request; writes from this process are serialized.
        long generation = 0;
        Task pending = Task.CompletedTask;
        window.PublishFeed = store =>
        {
            var request = Interlocked.Increment(ref generation);
            pending = pending.ContinueWith(previous =>
            {
                if (previous.IsFaulted) System.Diagnostics.Debug.WriteLine(previous.Exception);
                if (request == Interlocked.Read(ref generation))
                    Chronikologio.Core.FeedExchange.TryWrite(store);
            }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        };
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
        // Drain the serial chain before process exit.
        try { pending.GetAwaiter().GetResult(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
