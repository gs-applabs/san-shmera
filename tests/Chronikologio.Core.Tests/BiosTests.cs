using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public sealed class BiosTests
{
    private static readonly DayStore Store = DayStore.Load(FindData(), "gr");

    private static string FindData()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "data"))) dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir ?? throw new DirectoryNotFoundException("data folder not found"), "data");
    }

    private static IEnumerable<(string File, int Line, string[] Fields)> Lines()
    {
        foreach (var file in Directory.EnumerateFiles(FindData(), "bios*.tsv", SearchOption.AllDirectories))
        {
            var n = 0;
            foreach (var line in File.ReadLines(file))
            {
                n++;
                var l = line.TrimStart('﻿');
                if (string.IsNullOrWhiteSpace(l) || l.StartsWith('#')) continue;
                yield return (Path.GetFileName(file), n, l.Split('|'));
            }
        }
    }

    [Fact]
    public void EveryBiographyBelongsToExactlyOnePersonOfItsDay()
    {
        var bad = new List<string>();
        foreach (var (file, n, f) in Lines())
        {
            if (f.Length != 5 || f[0].Length != 5 || !int.TryParse(f[0][..2], out var m) || !int.TryParse(f[0][3..], out var d) || !int.TryParse(f[2], out var year)) { bad.Add($"{file}:{n} format"); continue; }
            var day = Store.GetDayIn(m, d, "el");
            var list = f[1] == "d" ? day.DeathList : day.People;
            if (list.Count(p => p.Year == year && p.Name == f[3]) < 1) bad.Add($"{file}:{n} {f[0]} {f[1]} {f[2]} {f[3]}");
        }
        Assert.True(bad.Count == 0, bad.Count + ": " + string.Join("\n", bad.Take(20)));
    }

    [Fact]
    public void BiographiesAreRealTexts()
    {
        var bad = new List<string>();
        foreach (var (file, n, f) in Lines())
        {
            if (f.Length != 5) continue;
            var words = f[4].Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            if (words < 25 || words > 300) bad.Add($"{file}:{n} has {words} words");
        }
        Assert.True(bad.Count == 0, string.Join("\n", bad.Take(20)));
    }

    [Fact]
    public void ABiographyReachesThePersonAndEnglishHidesGreekOnes()
    {
        var first = Lines().FirstOrDefault(l => l.Fields.Length == 5);
        if (first.Fields is null) return;
        var m = int.Parse(first.Fields[0][..2]); var d = int.Parse(first.Fields[0][3..]);
        var year = int.Parse(first.Fields[2]);
        var day = Store.GetDayIn(m, d, "el");
        var p = (first.Fields[1] == "d" ? day.DeathList : day.People).First(x => x.Year == year && x.Name == first.Fields[3]);
        Assert.False(string.IsNullOrWhiteSpace(p.Details));
        var en = Store.GetDayIn(m, d, "en");
        Assert.All(en.People.Concat(en.DeathList), x => Assert.DoesNotContain(x.Details ?? "", c => c is >= (char)0x370 and <= (char)0x3FF));
    }
}
