using Chronikologio.UI;
using Xunit;

namespace Chronikologio.Plugin.Tests;

[Collection("theme")]
public class AccessibilityTests : IDisposable
{
    private readonly string _settings = Path.Combine(Path.GetTempPath(), "chronikologio-access-" + Guid.NewGuid().ToString("N") + ".json");
    private static string DataRoot() => Path.Combine(AppContext.BaseDirectory, "data");
    public void Dispose() { if (File.Exists(_settings)) File.Delete(_settings); }

    private static IEnumerable<Control> All(Control root)
    {
        foreach (Control c in root.Controls) { yield return c; foreach (var d in All(c)) yield return d; }
    }

    [Fact]
    public void TheMainControlsHaveNamesForAScreenReader()
    {
        using var window = new MainWindow(DataRoot(), _settings) { StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000), ShowInTaskbar = false, Opacity = 0 };
        window.Show();
        window.SetDate(new DateOnly(2026, 12, 6));
        Application.DoEvents();
        var rail = All(window).OfType<SideRail>().Single();
        Assert.False(string.IsNullOrWhiteSpace(rail.AccessibleName));
        Assert.Contains(rail.Items[0].Label, rail.AccessibleDescription);
        var timeline = All(window).OfType<YearTimeline>().Single();
        Assert.False(string.IsNullOrWhiteSpace(timeline.AccessibleName));
        Assert.All(All(window).OfType<EventTile>(), t => Assert.False(string.IsNullOrWhiteSpace(t.AccessibleName)));
        Assert.False(string.IsNullOrWhiteSpace(All(window).OfType<SearchBar>().Single().AccessibleName));
    }
}
