using System.Reflection;
using Chronikologio.UI;
using Xunit;

namespace Chronikologio.Plugin.Tests;

[Collection("theme")]
public class RailTests : IDisposable
{
    private readonly string _settings = Path.Combine(Path.GetTempPath(), "chronikologio-rail-" + Guid.NewGuid().ToString("N") + ".json");
    private static string DataRoot() => Path.Combine(AppContext.BaseDirectory, "data");
    public void Dispose() { if (File.Exists(_settings)) File.Delete(_settings); }

    private static SideRail RailOf(MainWindow w) =>
        (SideRail)typeof(MainWindow).GetField("_rail", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(w)!;

    [Fact]
    public void TheTodayButtonOfTheRailGoesToToday()
    {
        using var window = new MainWindow(DataRoot(), _settings) { StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000), ShowInTaskbar = false, Opacity = 0 };
        window.Show();
        window.SetDate(new DateOnly(2026, 8, 15));
        Application.DoEvents();
        var today = DateOnly.FromDateTime(DateTime.Today);
        // the item itself, and a real click on its cell
        RailOf(window).Items.First(i => i.Key == "today").Click();
        Application.DoEvents();
        Assert.Equal(today, window.SelectedDate);

        window.SetDate(new DateOnly(2026, 8, 15));
        var rail = RailOf(window);
        var click = typeof(Control).GetMethod("OnMouseUp", BindingFlags.NonPublic | BindingFlags.Instance)!;
        // Group headings are not buttons. Click the actual painted cell after the heading.
        var cell = typeof(SideRail).GetMethod("Cell", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var bounds = (Rectangle)cell.Invoke(rail, new object[] { 0 })!;
        Assert.True(bounds.Top > SideRail.BrandHeight + 20);
        click.Invoke(rail, new object[] { new MouseEventArgs(MouseButtons.Left, 1, bounds.Left + 22, bounds.Top + bounds.Height / 2, 0) });
        Assert.Equal(today, window.SelectedDate);
        // Every newly grouped menu entry must use the same geometry for hit testing.
        foreach (var key in new[] { "calendar-tools", "astronomy", "today" })
        {
            var index = rail.Items.ToList().FindIndex(x => x.Key == key);
            Assert.True(index >= 0);
            var r = (Rectangle)cell.Invoke(rail, new object[] { index })!;
            click.Invoke(rail, new object[] { new MouseEventArgs(MouseButtons.Left, 1, r.Left + 22, r.Top + r.Height / 2, 0) });
            Assert.Equal(key, window.ActivePage);
        }
    }
}
