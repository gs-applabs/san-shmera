using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public sealed class AnalysisTests
{
    private static readonly DayStore Store = DayStore.Load(FindData(), "gr");
    private static readonly DayStore Cyprus = DayStore.Load(FindData(), "cy");

    private static string FindData()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "data"))) dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir ?? throw new DirectoryNotFoundException("data folder not found"), "data");
    }

    private static IEnumerable<(string File, int Line, string[] Fields)> Lines(string pattern)
    {
        foreach (var file in Directory.EnumerateFiles(FindData(), pattern, SearchOption.AllDirectories))
        {
            var n = 0;
            foreach (var line in File.ReadLines(file))
            {
                n++;
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#') || line.TrimStart('﻿').StartsWith('#')) continue;
                yield return (Path.GetFileName(file), n, line.TrimStart('﻿').Split('|'));
            }
        }
    }

    [Fact]
    public void EveryAnalysisBelongsToExactlyOneEventOfItsDay()
    {
        var bad = new List<string>();
        foreach (var (file, n, f) in Lines("analysis*.tsv"))
        {
            if (f.Length != 4 || f[0].Length != 5 || !int.TryParse(f[0][..2], out var m) || !int.TryParse(f[0][3..], out var d)) { bad.Add($"{file}:{n} format"); continue; }
            int? year = f[1].Length > 0 ? int.Parse(f[1]) : null;
            var hits = Store.GetDay(m, d).Events.Count(e => e.Year == year && e.Title == f[2]) + Cyprus.GetDay(m, d).Events.Count(e => e.Year == year && e.Title == f[2]);
            if (hits is < 1 or > 2) bad.Add($"{file}:{n} {f[0]} {f[1]} {f[2]}");
        }
        Assert.True(bad.Count == 0, string.Join("\n", bad.Take(20)));
    }

    [Fact]
    public void AnalysesAreRealTextsNotCopiesOfTheSummary()
    {
        var bad = new List<string>();
        foreach (var (file, n, f) in Lines("analysis*.tsv"))
        {
            if (f.Length != 4) continue;
            var words = f[3].Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            if (words < 40 || words > 400) bad.Add($"{file}:{n} has {words} words");
        }
        Assert.True(bad.Count == 0, string.Join("\n", bad.Take(20)));
    }

    [Fact]
    public void ShownAnalysisReachesTheEventAndEnglishHidesGreekAnalysis()
    {
        var any = Lines("analysis*.tsv").FirstOrDefault(l => l.Fields.Length == 4);
        if (any.Fields is null) return;
        var m = int.Parse(any.Fields[0][..2]); var d = int.Parse(any.Fields[0][3..]);
        int? year = any.Fields[1].Length > 0 ? int.Parse(any.Fields[1]) : null;
        var e = Store.GetDayIn(m, d, "el").Events.Concat(Cyprus.GetDayIn(m, d, "el").Events).First(x => x.Year == year && x.Title == any.Fields[2]);
        Assert.False(string.IsNullOrWhiteSpace(e.Details), $"{any.Fields[0]} {e.Year} {e.Title} errors={string.Join(";", Store.Errors.Take(3))}");
        var en = Store.GetDayIn(m, d, "en").Events.FirstOrDefault(x => x.Year == year);
        if (en is { Details: not null }) Assert.DoesNotContain(en.Details, c => c is >= (char)0x370 and <= (char)0x3FF);
    }
}
