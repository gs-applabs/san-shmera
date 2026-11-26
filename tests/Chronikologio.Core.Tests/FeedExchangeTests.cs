using System.Text.Json;
using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

/// <summary>The lines of the day for the News Crawler running without Opsis: a small file, rewritten only when its content changes.</summary>
public class FeedExchangeTests
{
    private static string DataRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "data"))) dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir ?? throw new DirectoryNotFoundException("data folder not found"), "data");
    }

    private static readonly DayStore Store = DayStore.Load(DataRoot());
    private static readonly DateOnly Day = new(2026, 3, 25);

    [Fact]
    public void FileHasSchema1AndOneEntryPerDayWithLines()
    {
        var json = FeedExchange.Build(Store, Day);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("schema").GetInt32());
        Assert.Equal("san-simera", root.GetProperty("key").GetString());
        var days = root.GetProperty("days");
        Assert.Equal(FeedExchange.DaysAhead, days.GetArrayLength());
        Assert.Equal("2026-03-25", days[0].GetProperty("date").GetString());
        Assert.NotEmpty(days[0].GetProperty("lines").EnumerateArray());
        Assert.All(days.EnumerateArray().SelectMany(d => d.GetProperty("lines").EnumerateArray()), l => Assert.False(string.IsNullOrWhiteSpace(l.GetString())));
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(json) < 256 * 1024);   // the Crawler refuses bigger files
    }

    [Fact]
    public void WritesOnlyWhenTheContentChangesAndNeverThrows()
    {
        var dir = Path.Combine(Path.GetTempPath(), "chron-feeds-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.True(FeedExchange.TryWrite(Store, Day, dir));
            Assert.True(File.Exists(Path.Combine(dir, FeedExchange.FileName)));
            Assert.False(FeedExchange.TryWrite(Store, Day, dir));            // same day, same lines: no write
            Assert.True(FeedExchange.TryWrite(Store, Day.AddDays(1), dir));  // the next day: new content
            Assert.False(File.Exists(Path.Combine(dir, FeedExchange.FileName + ".tmp")));
            // the folder cannot be created (a file is in the way): false, no exception
            var blocker = Path.Combine(dir, "blocker");
            File.WriteAllText(blocker, "x");
            Assert.False(FeedExchange.TryWrite(Store, Day, Path.Combine(blocker, "feeds")));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
