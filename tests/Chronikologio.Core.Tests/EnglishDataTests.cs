using Chronikologio.Core;
using System.Text.RegularExpressions;
using Xunit;

namespace Chronikologio.Core.Tests;

public sealed class EnglishDataTests
{
    private static readonly DayStore Store = DayStore.Load(FindData(), "gr");

    private static string FindData()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "data"))) dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir ?? throw new DirectoryNotFoundException("data folder not found"), "data");
    }

    private static bool Greek(string s) => Regex.IsMatch(s, "[\u0370-\u03FF\u1F00-\u1FFF]");

    [Fact]
    public void TheEnglishDayHasEnglishTextsAndTheGreekDayKeepsItsOwn()
    {
        var el = Store.GetDayIn(10, 28, "el");
        var en = Store.GetDayIn(10, 28, "en");
        Assert.Contains(el.Events, e => e.Year == 1940 && Greek(e.Title));
        Assert.Contains(en.Events, e => e.Year == 1940 && !Greek(e.Title) && !Greek(e.Summary));
    }

    [Fact]
    public void MostEntriesOfEveryDayAreTranslated()
    {
        int total = 0, greek = 0;
        for (var m = 1; m <= 12; m++)
            for (var d = 1; d <= DateTime.DaysInMonth(2024, m); d++)
            {
                var v = Store.GetDayIn(m, d, "en");
                foreach (var e in v.Events) { total++; if (Greek(e.Title) || Greek(e.Summary)) greek++; }
                foreach (var p in v.People.Concat(v.DeathList)) { total++; if (Greek(p.Name) || Greek(p.Description)) greek++; }
                foreach (var c in v.Commemorations) { total++; if (Greek(c.Title)) greek++; }
            }
        Assert.True(greek <= total * 0.05, $"{greek} of {total} entries are still Greek in the English calendar");
    }

    [Fact]
    public void EnglishBiographiesMatchAGreekBiographyAndAreEnglish()
    {
        var files = Directory.GetFiles(Path.Combine(FindData(), "i18n"), "en-bios*.tsv");
        var seen = new HashSet<string>();
        foreach (var file in files)
        {
            var n = 0;
            foreach (var line in File.ReadLines(file))
            {
                n++;
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
                var f = line.Split('|');
                Assert.True(f.Length == 6 && f[0] == "bio", $"{Path.GetFileName(file)}:{n}: bad line");
                var m = int.Parse(f[1][..2]); var d = int.Parse(f[1][3..]); var y = int.Parse(f[2]);
                Assert.True(seen.Add($"{f[1]}|{y}|{f[3]}"), $"{Path.GetFileName(file)}:{n}: duplicate");
                var stores = new[] { Store, DayStore.Load(FindData(), "cy") };
                var person = stores.SelectMany(st => { var el = st.GetDayIn(m, d, "el"); return el.People.Concat(el.DeathList); }).FirstOrDefault(p => p.Year == y && p.Name == f[3]);
                Assert.True(person is { Details: { Length: > 0 } }, $"{Path.GetFileName(file)}:{n}: no Greek biography for {f[3]}");
                Assert.False(Greek(f[4]) || Greek(f[5]), $"{Path.GetFileName(file)}:{n}: Greek letters in the English text");
                var words = f[5].Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                Assert.InRange(words, 15, 330);
                Assert.Contains(stores.SelectMany(st => { var en = st.GetDayIn(m, d, "en"); return en.People.Concat(en.DeathList); }), p => p.Year == y && p.Details == f[5].Replace(" // ", "\n\n"));
            }
        }
    }

    [Fact]
    public void EnglishLivesAreShownInEnglish()
    {
        var en = Store.GetDayIn(1, 1, "en");
        var life = en.Commemorations.FirstOrDefault(c => c.Details is { Length: > 100 });
        Assert.NotNull(life);
        Assert.False(Greek(life!.Details!), "the life should be English");
    }
}
