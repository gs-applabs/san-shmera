using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public sealed class RichDayProjectionTests
{
    [Fact]
    public void ProjectionPreservesCountryKindStableIdAndAncientYear()
    {
        var now = DateTimeOffset.UtcNow;
        var sample = new DailySnapshot(new DateOnly(2026,11,23),"gr","el",
            new[] { new FeedItem(FeedKind.Event, "23 Νοεμβρίου: -490 Η μάχη του Μαραθώνα", "Chronikologio"),
                    new FeedItem(FeedKind.NameDays, "Εορτάζουν σήμερα: ...", "Chronikologio") }, now);
        var lines = RichDayProjection.Project(sample);
        Assert.Equal(2,lines.Count);
        Assert.Equal("GR",lines[0].CountryCode);
        Assert.Equal("el",lines[0].LanguageCode);
        Assert.Equal("event",lines[0].Kind);
        Assert.Equal(-490,lines[0].HistoricalYear);
        Assert.True(lines[1].Priority > lines[0].Priority);
        Assert.Equal(lines[0].EntryId,RichDayProjection.Project(sample)[0].EntryId);
        Assert.Equal(lines[0].EntryId,lines[0].DetailId);
    }
}
