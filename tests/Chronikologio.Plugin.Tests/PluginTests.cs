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

    [Fact]
    public void PluginAssemblyNameMatchesTheHostSearchPattern() =>
        Assert.EndsWith("Plugin", typeof(ChronikologioPlugin).Assembly.GetName().Name);
}
