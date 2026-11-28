using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Chronikologio.Core;

public enum PersonalEventKind { Birthday, NameDay, Anniversary, Reminder, Other }
public sealed record PersonalEvent(Guid Id, DateOnly Date, string Title, PersonalEventKind Kind, bool Yearly, string Notes = "");
public sealed record PersonalOccurrence(PersonalEvent Event, DateOnly Date);
public sealed record PersonalCalendarSnapshot(string Revision, IReadOnlyList<PersonalEvent> Events);

/// <summary>Local-only private calendar. Never used by the public day store, feed or plugin widget.
/// Corrupt or oversized files are reported, never silently replaced.</summary>
public sealed class PersonalCalendar
{
    public const int MaxEvents = 5000, MaxTitle = 160, MaxNotes = 1000, MaxBytes = 16 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private sealed record Document(int Version, string Revision, PersonalEvent[] Events);
    private readonly string _path;
    public PersonalCalendar(string path) => _path = Path.GetFullPath(path);

    public PersonalCalendarSnapshot Load()
    {
        try
        {
            using var input = new FileStream(_path,FileMode.Open,FileAccess.Read,FileShare.Read | FileShare.Delete);
            if (input.Length > MaxBytes) throw new InvalidDataException("Personal calendar exceeds its size limit.");
            var bytes = new byte[(int)input.Length];
            input.ReadExactly(bytes);
            var doc = JsonSerializer.Deserialize<Document>(bytes) ?? throw new InvalidDataException("Invalid calendar.");
            if (doc.Version != 1 || string.IsNullOrEmpty(doc.Revision) || doc.Events is null) throw new InvalidDataException("Unsupported calendar.");
            Validate(doc.Events);
            return new(doc.Revision,Array.AsReadOnly(doc.Events));
        }
        catch (FileNotFoundException) { return new("",Array.Empty<PersonalEvent>()); }
        catch (DirectoryNotFoundException) { return new("",Array.Empty<PersonalEvent>()); }
        catch (JsonException ex) { throw new InvalidDataException("Personal calendar cannot be read; original preserved.",ex); }
    }

    private static void Validate(IReadOnlyCollection<PersonalEvent> events)
    {
        if (events.Count > MaxEvents) throw new InvalidDataException("Too many personal events.");
        var ids = new HashSet<Guid>();
        foreach (var e in events)
        {
            if (e is null || e.Id == Guid.Empty || !ids.Add(e.Id) || string.IsNullOrWhiteSpace(e.Title) ||
                e.Title.Length > MaxTitle || e.Title.Any(char.IsControl) || e.Notes is null ||
                e.Notes.Length > MaxNotes || !Enum.IsDefined(e.Kind))
                throw new InvalidDataException("Invalid or duplicate personal event.");
        }
    }

    /// <summary>Compare-and-save under an exclusive cross-process lock. No lost updates from stale windows.
    /// The lock file contains no event data. Failed writes leave the previous version intact.</summary>
    public PersonalCalendarSnapshot Save(string expectedRevision,IEnumerable<PersonalEvent> events)
    {
        var items = events.Take(MaxEvents+1).ToArray();
        Validate(items);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        using var gate = new FileStream(_path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        if (!string.Equals(Load().Revision,expectedRevision,StringComparison.Ordinal))
            throw new InvalidOperationException("The personal calendar was changed elsewhere. Reload before saving.");
        var revision = Guid.NewGuid().ToString("N");
        var data = JsonSerializer.SerializeToUtf8Bytes(new Document(1,revision,items),JsonOptions);
        if (data.Length > MaxBytes) throw new InvalidDataException("Personal calendar exceeds its size limit.");
        var temp = _path+"."+revision+".tmp";
        try
        {
            using (var stream = new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            { stream.Write(data); stream.Flush(true); }
            File.Move(temp,_path,true);
        }
        finally
        {
            try { File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        return new(revision,Array.AsReadOnly(items));
    }

    /// <summary>Occurrences starting at reference, including today; yearly Feb 29 uses Feb 28 in common years.
    /// Never generate dates before the original event. Bounded to a single next occurrence per item.</summary>
    public static IReadOnlyList<PersonalOccurrence> Upcoming(IEnumerable<PersonalEvent> events,DateOnly reference)
    {
        var result = new List<PersonalOccurrence>();
        foreach (var e in events)
        {
            DateOnly? date = e.Yearly ? CalendarTools.TryNextAnniversary(e.Date,reference) : e.Date >= reference ? e.Date : null;
            if (date.HasValue) result.Add(new(e,date.Value));
        }
        return result.OrderBy(x=>x.Date).ThenBy(x=>x.Event.Title,StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    /// <summary>Export concrete all-day occurrences only, not a calendar subscription.
    /// Notes are excluded unless expressly requested; no network, alarms or recipients.</summary>
    public static string ExportIcs(IEnumerable<PersonalOccurrence> occurrences,DateTime utcNow,bool includeNotes=false)
    {
        var sb = new StringBuilder();
        void Line(string line)
        {
            int bytes=0;
            foreach (var rune in line.EnumerateRunes())
            {
                if (bytes+rune.Utf8SequenceLength > 75) { sb.Append("\r\n "); bytes=1; }
                sb.Append(rune.ToString()); bytes+=rune.Utf8SequenceLength;
            }
            sb.Append("\r\n");
        }
        string Escape(string value) => value.Replace("\\","\\\\").Replace("\r\n","\\n").Replace("\r","\\n").Replace("\n","\\n").Replace(";","\\;").Replace(",","\\,");
        Line("BEGIN:VCALENDAR"); Line("VERSION:2.0"); Line("PRODID:-//GS App Labs//Chronikologio//EN"); Line("CALSCALE:GREGORIAN");
        foreach (var x in occurrences.Take(MaxEvents))
        {
            if (x.Date == DateOnly.MaxValue) continue; // RFC 5545 exclusive end cannot represent year 10000.
            string date = x.Date.ToString("yyyyMMdd",CultureInfo.InvariantCulture);
            Line("BEGIN:VEVENT"); Line("UID:"+x.Event.Id.ToString("N")+"-"+date+"@chronikologio.local");
            Line("DTSTAMP:"+utcNow.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'",CultureInfo.InvariantCulture));
            Line("DTSTART;VALUE=DATE:"+date);
            Line("DTEND;VALUE=DATE:"+x.Date.AddDays(1).ToString("yyyyMMdd",CultureInfo.InvariantCulture));
            Line("SUMMARY:"+Escape(x.Event.Title));
            if (includeNotes && x.Event.Notes.Length>0) Line("DESCRIPTION:"+Escape(x.Event.Notes));
            Line("CLASS:PRIVATE"); Line("END:VEVENT");
        }
        Line("END:VCALENDAR");
        return sb.ToString();
    }
}
