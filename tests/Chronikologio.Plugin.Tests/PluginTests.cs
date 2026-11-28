using Chronikologio.OpsisPlugin;
using OpsisNewsCrawler.Plugin.Abstractions;
using Xunit;

namespace Chronikologio.Plugin.Tests;

[Collection("theme")]
public class PluginTests
{
    [Fact]
    public void StartRegistersTheSanSimeraFeedAndStopRemovesIt()
    {
        var plugin = new ChronikologioPlugin();
        plugin.Start();
        try
        {
            var feed = PluginFeeds.Find(ChronikologioPlugin.FeedKey);
            Assert.NotNull(feed);
            Assert.Equal("Σαν σήμερα", feed!.TitleEl);
            Assert.NotNull(feed.GetItems());
        }
        finally { plugin.Stop(); }
        Assert.Null(PluginFeeds.Find(ChronikologioPlugin.FeedKey));
    }

    [Fact]
    public void RepeatedFeedReadsReuseSnapshotAndStartStopRemainsIdempotent()
    {
        var plugin = new ChronikologioPlugin();
        for (var cycle = 0; cycle < 3; cycle++)
        {
            plugin.Start();
            plugin.Start();
            try
            {
                var feed = PluginFeeds.Find(ChronikologioPlugin.FeedKey);
                Assert.NotNull(feed);
                Assert.True(SpinWait.SpinUntil(() => feed!.GetItems().Count > 0, TimeSpan.FromSeconds(20)));
                var first = feed!.GetItems();
                for (var i = 0; i < 1000; i++) Assert.Same(first, feed.GetItems());
            }
            finally { plugin.Stop(); plugin.Stop(); }
            Assert.Null(PluginFeeds.Find(ChronikologioPlugin.FeedKey));
        }
    }

    [Fact]
    public void OffersACalendarWidgetThatReplacesTheBuiltInOne()
    {
        var widget = Assert.Single(((IPluginWidgetProvider)new ChronikologioPlugin()).GetWidgets());
        Assert.Equal("calendar", widget.ReplacesBuiltIn);
        using var control = widget.CreateContent();
        Assert.NotNull(control);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetGuiResources(IntPtr process, uint flags);

    [Fact]
    public void RepeatedWidgetCreationAndDisposalMaintainsResourcePlateau()
    {
        if (Environment.GetEnvironmentVariable("CHRON_SOAK") != "1") return;
        var plugin = new ChronikologioPlugin();
        plugin.Start();
        try
        {
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            var widget = Assert.Single(plugin.GetWidgets());
            var samples = new List<(long Private, uint Gdi, uint User, int Handles)>();
            for (var cycle = 0; cycle < 30; cycle++)
            {
                using (var form = new Form { Size = new Size(420, 400), StartPosition = FormStartPosition.Manual,
                    Location = new Point(-32000, -32000), ShowInTaskbar = false, Opacity = 0 })
                {
                    var panel = Assert.IsType<Chronikologio.UI.CalendarPanel>(widget.CreateContent());
                    panel.Dock = DockStyle.Fill;
                    form.Controls.Add(panel);
                    form.Show();
                    for (var day = 0; day < 10; day++)
                    {
                        panel.SelectedDate = new DateOnly(2026, 1, 1).AddDays(cycle * 10 + day);
                        Application.DoEvents();
                    }
                    form.Close();
                }
                Application.DoEvents();
                process.Refresh();
                samples.Add((process.PrivateMemorySize64, GetGuiResources(process.Handle, 0), GetGuiResources(process.Handle, 1), process.HandleCount));
            }
            var baseline = samples[9]; var end = samples[^1];
            var dir = Environment.GetEnvironmentVariable("CHRON_METRICS_DIR");
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "widget-soak.json"), System.Text.Json.JsonSerializer.Serialize(
                    samples.Select((s, i) => new { cycle = i + 1, privateBytes = s.Private, gdi = s.Gdi, user = s.User, handles = s.Handles })));
            }
            Assert.True(baseline.Gdi > 0 && baseline.User > 0);
            Assert.True(end.Gdi <= baseline.Gdi + 60, $"Widget GDI {baseline.Gdi} -> {end.Gdi}");
            Assert.True(end.User <= baseline.User + 60, $"Widget USER {baseline.User} -> {end.User}");
            Assert.True(end.Handles <= baseline.Handles + 100, $"Widget handles {baseline.Handles} -> {end.Handles}");
            Assert.True(end.Private <= baseline.Private + 64L * 1024 * 1024, $"Widget private bytes {baseline.Private} -> {end.Private}");
        }
        finally { plugin.Stop(); }
    }

    [Fact]
    public void PluginDllIdentityIsSeparateFromProductVersion()
    {
        var assembly = typeof(ChronikologioPlugin).Assembly;
        Assert.Equal(new Version(1, 0, 0, 0), assembly.GetName().Version);
        Assert.Equal("1.0.0.0", System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion);
        var product = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location).ProductVersion!;
        Assert.Equal(product.Split('+')[0], new ChronikologioPlugin().Version);
    }

    [Fact]
    public void PluginAssemblyNameMatchesTheHostSearchPattern() =>
        Assert.EndsWith("Plugin", typeof(ChronikologioPlugin).Assembly.GetName().Name);
}
