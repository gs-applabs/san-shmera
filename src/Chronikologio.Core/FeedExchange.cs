using System.Text;
using System.Text.Json;

namespace Chronikologio.Core;

/// <summary>
/// The "Σαν σήμερα" lines for programs that are NOT in the same process as Chronikologio (the News Crawler running on its own, or Opsis hosting
/// only one of the two). The lines of the next days are left in a small JSON file in the per-user family folder, where the Crawler picks up today's
/// lines. Inside Opsis nothing depends on this: the plugin feed travels in-process and wins. Nothing is written when the content has not changed,
/// so the file is rewritten about once a day (or when the country or the language changes).
/// </summary>
public static class FeedExchange
{
    public const string FileName = "san-simera.json";
    public const int DaysAhead = 7;

    /// <summary>The family feeds folder: <c>%LocalAppData%\Opsis\feeds</c>, or the folder named by OPSIS_FEEDS_DIR (tests, special installs).</summary>
    public static string DefaultFolder =>
        Environment.GetEnvironmentVariable("OPSIS_FEEDS_DIR") is { Length: > 0 } dir
            ? dir
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Opsis", "feeds");

    /// <summary>The JSON text for today and the next days (schema 1; no time stamps, so equal content gives equal text).</summary>
    public static string Build(DayStore store, DateOnly from, int days = DaysAhead)
    {
        ArgumentNullException.ThrowIfNull(store);
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            w.WriteStartObject();
            w.WriteNumber("schema", 1);
            w.WriteString("key", "san-simera");
            w.WriteString("titleEl", "Σαν σήμερα");
            w.WriteString("titleEn", "On this day");
            w.WriteString("region", store.Region);
            w.WriteString("language", Loc.Language);
            w.WriteStartArray("days");
            for (var i = 0; i < Math.Clamp(days, 1, 14); i++)
            {
                var date = from.AddDays(i);
                w.WriteStartObject();
                w.WriteString("date", date.ToString("yyyy-MM-dd"));
                w.WriteStartArray("lines");
                foreach (var item in SanSimeraFeed.BuildForTicker(store, date).Take(12))
                    if (!string.IsNullOrWhiteSpace(item.Text)) w.WriteStringValue(item.Text);
                w.WriteEndArray();
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Writes the file when its content would change. Never throws: a full disk or a locked file must not disturb the program. True when written.</summary>
    public static bool TryWrite(DayStore store, DateOnly? from = null, string? folder = null)
    {
        try
        {
            var json = Build(store, from ?? DateOnly.FromDateTime(DateTime.Now));
            var dir = folder ?? DefaultFolder;
            var path = Path.Combine(dir, FileName);
            if (File.Exists(path) && string.Equals(File.ReadAllText(path, Encoding.UTF8), json, StringComparison.Ordinal)) return false;
            Directory.CreateDirectory(dir);
            var temp = path + ".tmp";
            File.WriteAllText(temp, json, new UTF8Encoding(false));
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            System.Diagnostics.Debug.WriteLine("Chronikologio feed file not written: " + ex.Message);
            return false;
        }
    }
}
