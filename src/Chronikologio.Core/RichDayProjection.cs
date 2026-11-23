using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Chronikologio.Core;

/// <summary>Precomputed, immutable shape for Chronikologio rich feeds. No dependency on the Opsis contract DLL.</summary>
public sealed record RichDayLine(
    string EntryId, string Text, string Kind, string CountryCode,
    string LanguageCode, int? HistoricalYear, int Priority, string DetailId);

public static class RichDayProjection
{
    private static readonly Regex YearAtStart = new(@":\s*(-?\d{3,4})\b", RegexOptions.Compiled);

    public static IReadOnlyList<RichDayLine> Project(DailySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var country = snapshot.Region.ToLowerInvariant() switch { "gr" => "GR", "cy" => "CY", _ => "" };
        return snapshot.Items.Take(500).Select(item =>
        {
            var key = $"{snapshot.Region}|{snapshot.Date:yyyy-MM-dd}|{item.Kind}|{item.Text}";
            var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..24].ToLowerInvariant();
            int? year = null;
            if (item.Kind == FeedKind.Event && YearAtStart.Match(item.Text) is { Success: true } match
                && int.TryParse(match.Groups[1].Value, out var parsed)) year = parsed;
            var priority = item.Kind switch
            {
                FeedKind.Holiday => 100, FeedKind.NameDays => 90, FeedKind.Saint => 75,
                FeedKind.Event => 60, FeedKind.Fasting => 55, FeedKind.Birth => 40, FeedKind.Death => 35,
                _ => 20
            };
            return new RichDayLine(id, item.Text, item.Kind.ToString().ToLowerInvariant(),
                country, snapshot.Language, year, priority, id);
        }).ToArray();
    }
}
