using Chronikologio.Core;
using Chronikologio.UI;
using Xunit;

namespace Chronikologio.Plugin.Tests;

[Collection("theme")]
public sealed class TimeToolsUiTests : IDisposable
{
    private readonly string _settings = Path.Combine(Path.GetTempPath(), "chron-tools-" + Guid.NewGuid().ToString("N") + ".json");
    public void Dispose() { if (File.Exists(_settings)) File.Delete(_settings); }
    private static IEnumerable<Control> All(Control root)
    {
        foreach (Control c in root.Controls) { yield return c; foreach (var child in All(c)) yield return child; }
    }

    [Fact]
    public void PagesAreCreatedOnlyOnDemandAndDisposedWhenLeaving()
    {
        using var window = new MainWindow(Path.Combine(AppContext.BaseDirectory,"data"), _settings)
        { ShowInTaskbar = false, Opacity = 0, StartPosition = FormStartPosition.Manual, Location = new Point(-32000,-32000) };
        window.Show(); Application.DoEvents();
        Assert.Empty(All(window).OfType<TimeToolsPage>());
        var rail = All(window).OfType<SideRail>().Single();
        Assert.Contains(rail.Items, x => x.Key == "calendar-tools");
        Assert.Contains(rail.Items, x => x.Key == "astronomy");
        rail.Items.Single(x => x.Key == "calendar-tools").Click();
        var calendar = Assert.Single(All(window).OfType<CalendarToolsPage>());
        Assert.Contains(All(calendar).OfType<ListView>(), x => x.Items.Count == 5);
        window.OpenPage("astronomy"); Application.DoEvents();
        Assert.True(calendar.IsDisposed);
        var sky = Assert.Single(All(window).OfType<AstronomyPage>());
        Assert.Contains(All(sky).OfType<ListView>(), x => x.Items.Count == 7);
        window.AstronomySiteSetting = "nicosia";
        Assert.True(sky.IsDisposed);
        Assert.Equal("nicosia", UiSettings.Load(_settings).AstronomySite);
        window.GoToday();
        Assert.Empty(All(window).OfType<TimeToolsPage>());
        Assert.Equal("today",window.ActivePage);
    }
}
