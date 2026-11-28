using System.Reflection;
using Chronikologio.UI;
using Chronikologio.Core;
using System.Globalization;
using Xunit;

namespace Chronikologio.Plugin.Tests;

[Collection("theme")]
public class SearchBarTests : IDisposable
{
    private readonly string _settings = Path.Combine(Path.GetTempPath(), "chronikologio-search-" + Guid.NewGuid().ToString("N") + ".json");
    private static string DataRoot() => Path.Combine(AppContext.BaseDirectory, "data");
    public void Dispose() { if (File.Exists(_settings)) File.Delete(_settings); }

    private static IEnumerable<Control> All(Control root)
    {
        foreach (Control c in root.Controls) { yield return c; foreach (var d in All(c)) yield return d; }
    }

    private MainWindow Open()
    {
        var w = new MainWindow(DataRoot(), _settings) { StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000), ShowInTaskbar = false, Opacity = 0 };
        w.Show();
        Application.DoEvents();
        return w;
    }

    [Fact]
    public void TheSearchIsAtTheTopLeftOfTheWindowAboveTheSideRail()
    {
        using var w = Open();
        var bar = All(w).OfType<SearchBar>().Single();
        // in the first row of the header, on the right, just left of the Today button
        Assert.IsType<DayHeader>(bar.Parent);
        var at = w.PointToClient(bar.PointToScreen(Point.Empty));
        Assert.True(at.Y < 80, "in the top row");
        Assert.True(at.X + bar.Width > w.ClientSize.Width - 130, "at the right, next to the Today button");
        Assert.True(at.X > w.ClientSize.Width / 2, "on the right half");
        var rail = All(w).OfType<SideRail>().Single();
        Assert.DoesNotContain(rail.Items, i => i.Key == "search");
    }

    [Fact]
    public void TheLastItemOfTheRailIsCalledAbout()
    {
        using var w = Open();
        var rail = All(w).OfType<SideRail>().Single();
        Assert.StartsWith("Σχετικά", rail.Items[^1].Tip);
        Assert.Equal("help", rail.Items[^1].Key);
    }

    [Fact]
    public void TypingANameListsItAndChoosingItGoesToItsDay()
    {
        using var w = Open();
        var bar = All(w).OfType<SearchBar>().Single();
        bar.Search("Νεκταρ");
        Application.DoEvents();
        Assert.True(bar.ListVisible);
        Assert.Contains(bar.Results, r => r.Contains("Νεκτάριος"));
        var index = bar.Results.ToList().FindIndex(r => r.Contains("Νεκτάριος"));
        bar.ChooseResult(index);
        Assert.Equal(11, w.SelectedDate.Month);
        Assert.Equal(9, w.SelectedDate.Day);
        Assert.False(bar.ListVisible);
    }

    [Theory]
    [InlineData("gr", "el")]
    [InlineData("gr", "en")]
    [InlineData("cy", "el")]
    [InlineData("cy", "en")]
    public void OptimizedSearchMatchesPreviousRanking(string region, string language)
    {
        new UiSettings { Region = region, Language = language }.Save(_settings);
        using var w = Open();
        var bar = All(w).OfType<SearchBar>().Single();
        var store = DayStore.Load(DataRoot(), region);
        var entries = new List<(string Folded, string Text, string Kind, int Month, int Day, int Priority)>();
        for (var i = 0; i < 366; i++)
        {
            var date = new DateOnly(2024, 1, 1).AddDays(i);
            var day = store.GetDay(date.Month, date.Day);
            foreach (var c in day.Commemorations)
            {
                var priority = c.Sources.Any(s => s.Url?.Contains("synaxarion.gr/gr/sid/", StringComparison.Ordinal) == true) ? 1 : 0;
                foreach (var n in c.Names)
                    entries.Add((TextNorm.Fold(n.Name + " " + string.Join(" ", n.Variants)), n.Name, "name", date.Month, date.Day, priority));
                entries.Add((TextNorm.Fold(c.Title), c.Title, "saint", date.Month, date.Day, priority));
            }
            foreach (var e in day.Events)
                entries.Add((TextNorm.Fold(e.Title), (e.Year is { } y ? y.ToString(CultureInfo.InvariantCulture) + " · " : "") + e.Title, "event", date.Month, date.Day, 0));
        }
        foreach (var query in new[] { "αγ", "αγιος", "νεκταρ", "δημητρ", "τιταν", "sa", "saint", "john", "war", "titanic", "zzzzzz" })
        {
            var q = TextNorm.Fold(query);
            var old = entries.Where(e => e.Folded.Contains(q, StringComparison.Ordinal))
                .OrderBy(e => e.Kind == "name" ? 0 : e.Kind == "saint" ? 1 : 2)
                .ThenBy(e => e.Folded.StartsWith(q, StringComparison.Ordinal) ? 0 : 1)
                .ThenBy(e => e.Priority)
                .DistinctBy(e => (e.Text, e.Month, e.Day)).ToList();
            var expected = old.Where(e => e.Kind == "name").Take(6)
                .Concat(old.Where(e => e.Kind == "saint").Take(6))
                .Concat(old.Where(e => e.Kind == "event").Take(4)).Select(e => e.Text).ToArray();
            bar.Search(query);
            Assert.Equal(expected, bar.Results.ToArray());
        }
    }

    [Fact]
    public void SaintsAndEventsAreFoundToo()
    {
        using var w = Open();
        var bar = All(w).OfType<SearchBar>().Single();
        bar.Search("Τιτανικός");
        Application.DoEvents();
        Assert.Contains(bar.Results, r => r.Contains("Τιτανικ"));
        bar.Search("Δημήτριος");
        Application.DoEvents();
        Assert.Contains(bar.Results, r => r.Contains("Άγιος Δημήτριος"));
    }
}
