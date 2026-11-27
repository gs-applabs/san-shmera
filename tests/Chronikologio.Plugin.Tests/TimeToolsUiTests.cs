using Chronikologio.Core;
using Chronikologio.UI;
using Xunit;

namespace Chronikologio.Plugin.Tests;

[Collection("theme")]
public sealed class TimeToolsUiTests : IDisposable
{
    private readonly string _settings = Path.Combine(Path.GetTempPath(), "chron-tools-" + Guid.NewGuid().ToString("N") + ".json");
    public void Dispose() { Loc.Set("el"); ChronikologioTheme.Set("system"); if (File.Exists(_settings)) File.Delete(_settings); }
    private static IEnumerable<Control> All(Control root)
    {
        foreach (Control c in root.Controls) { yield return c; foreach (var child in All(c)) yield return child; }
    }

    [Theory]
    [InlineData("dark", "el")]
    [InlineData("light", "en")]
    public void RenderNewWorkspacesAndSettings(string theme, string language)
    {
        new UiSettings { Theme = theme, Language = language }.Save(_settings);
        using var window = new MainWindow(Path.Combine(AppContext.BaseDirectory, "data"), _settings)
        { ShowInTaskbar = false, Opacity = 0, StartPosition = FormStartPosition.Manual, Location = new Point(-32000,-32000) };
        window.Show(); Application.DoEvents();
        var dir = Environment.GetEnvironmentVariable("CHRON_RENDER_DIR") ?? Environment.GetEnvironmentVariable("CHRON_METRICS_DIR");
        void Render(Control c, string name)
        {
            if (string.IsNullOrEmpty(dir)) return;
            Directory.CreateDirectory(dir);
            using var image = new Bitmap(c.ClientSize.Width, c.ClientSize.Height);
            c.DrawToBitmap(image, new Rectangle(0,0,image.Width,image.Height));
            image.Save(Path.Combine(dir, name + "-" + theme + "-" + language + ".png"));
        }
        foreach (var page in new[] { "calendar-tools", "astronomy" })
        {
            window.OpenPage(page); Application.DoEvents();
            var surface = Assert.Single(All(window).OfType<TimeToolsPage>());
            Assert.True(surface.Width > 600);
            Assert.All(All(surface).OfType<ListView>(), table => Assert.True(table.Columns.Count >= 2));
            Render(window,page);
        }
        using var settings = new SettingsDialog(window) { ShowInTaskbar = false, Opacity = 0 };
        settings.Show(); Application.DoEvents();
        settings.Pick("astronomy-site","patras");
        Assert.Equal("patras",window.AstronomySiteSetting);
        Render(settings,"time-tools-settings");
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
