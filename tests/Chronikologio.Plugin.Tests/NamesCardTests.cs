using Chronikologio.UI;
using Xunit;

namespace Chronikologio.Plugin.Tests;

[Collection("theme")]
public class NamesCardTests : IDisposable
{
    private readonly string _settings = Path.Combine(Path.GetTempPath(), "chronikologio-names-" + Guid.NewGuid().ToString("N") + ".json");
    private static string DataRoot() => Path.Combine(AppContext.BaseDirectory, "data");
    public void Dispose() { if (File.Exists(_settings)) File.Delete(_settings); }

    private static IEnumerable<Control> All(Control root)
    {
        foreach (Control c in root.Controls) { yield return c; foreach (var d in All(c)) yield return d; }
    }

    [Theory]
    [InlineData(6, 10)]
    [InlineData(12, 6)]
    public void TheDayShowsEveryNameAndTheirEtymologyWithoutCuts(int month, int day)
    {
        using var window = new MainWindow(DataRoot(), _settings) { StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000), ShowInTaskbar = false, Opacity = 0 };
        window.Show();
        window.SetDate(new DateOnly(2026, month, day));
        Application.DoEvents();
        var store = Chronikologio.Core.DayStore.Load(DataRoot(), "gr");
        var names = store.GetDayIn(month, day, "el").Names.Select(n => n.Name).Distinct().ToList();
        Assert.NotEmpty(names);
        var text = string.Join("\n", All(window).OfType<Label>().Where(l => l.Visible).Select(l => l.Text));
        Assert.All(names, n => Assert.Contains(n, text));
        Assert.DoesNotContain("…", string.Join("\n", All(window).OfType<Label>().Where(l => l.Text.Contains("—") && l.Tag as string == "muted").Select(l => l.Text)));
    }
}
