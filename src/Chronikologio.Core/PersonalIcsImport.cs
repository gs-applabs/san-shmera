using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Chronikologio.Core;

/// <summary>Conservative subset of iCalendar: single-day, all-day VEVENT records only.
/// Never follows URLs, attachments, alarms, time zones or recurring rules.
/// Unsupported events are counted, not converted into misleading dates.</summary>
public static class PersonalIcsImport
{
    public const int MaxInputBytes = 4 * 1024 * 1024;
    private const int MaxLineLength = 8192;
    public sealed record Preview(IReadOnlyList<PersonalEvent> Events, int Skipped);

    public static Preview Read(string path)
    {
        using var stream = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if (stream.Length>MaxInputBytes) throw new InvalidDataException("ICS exceeds its size limit.");
        var bytes=new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        if(stream.ReadByte()!=-1)throw new InvalidDataException("ICS changed while reading.");
        try { return Parse(new UTF8Encoding(false,true).GetString(bytes).TrimStart('\uFEFF')); }
        catch(DecoderFallbackException ex){throw new InvalidDataException("ICS is not valid UTF-8.",ex);}
    }

    public static Preview Parse(string text)
    {
        if(Encoding.UTF8.GetByteCount(text)>MaxInputBytes)throw new InvalidDataException("ICS exceeds its size limit.");
        var events=new List<PersonalEvent>();
        var uids=new HashSet<string>(StringComparer.Ordinal);
        var components=new Stack<string>();
        var properties=new Dictionary<string,(string Header,string Value)>(StringComparer.OrdinalIgnoreCase);
        bool calendarSeen=false,calendarClosed=false,unsupported=false;
        int skipped=0,seen=0;
        foreach(var line in Unfold(text))
        {
            if(line.Length==0)continue;
            var colon=line.IndexOf(':');
            if(colon<=0)throw new InvalidDataException("Malformed ICS property.");
            var header=line[..colon];
            var key=header.Split(';',2)[0].ToUpperInvariant();
            var value=line[(colon+1)..];
            if(key=="BEGIN")
            {
                if(header!="BEGIN"&&!header.Equals("BEGIN",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Invalid component.");
                var component=value.ToUpperInvariant();
                if(components.Count==0)
                {
                    if(calendarSeen||component!="VCALENDAR")throw new InvalidDataException("Expected one VCALENDAR.");
                    calendarSeen=true;
                }
                else if(component=="VEVENT")
                {
                    if(components.Count!=1||components.Peek()!="VCALENDAR")throw new InvalidDataException("Nested VEVENT.");
                    if(++seen>PersonalCalendar.MaxEvents)throw new InvalidDataException("Too many ICS events.");
                    properties.Clear();unsupported=false;
                }
                else if(components.Contains("VEVENT"))unsupported=true;
                if(components.Count>=8)throw new InvalidDataException("ICS nesting limit exceeded.");
                components.Push(component);
                continue;
            }
            if(key=="END")
            {
                if(!header.Equals("END",StringComparison.OrdinalIgnoreCase)||components.Count==0||
                    components.Pop()!=value.ToUpperInvariant())throw new InvalidDataException("Mismatched ICS component.");
                if(value.Equals("VEVENT",StringComparison.OrdinalIgnoreCase))
                {
                    if(unsupported||!TryEvent(properties,out var entry,out var uid)){skipped++;continue;}
                    if(!uids.Add(uid))throw new InvalidDataException("Repeated supported ICS UID.");
                    events.Add(entry);
                }
                else if(components.Count==0)calendarClosed=true;
                continue;
            }
            if(components.Count==0)throw new InvalidDataException("Property outside calendar.");
            if(components.Peek()!="VEVENT")continue;
            // Do not import recurrence, exceptions, cancellations, multi-day or timed events.
            if(key is "RRULE" or "RDATE" or "EXDATE" or "EXRULE" or "RECURRENCE-ID" or "DURATION" or "ATTACH" or "STATUS")
                unsupported=true;
            if(key is "UID" or "DTSTART" or "DTEND" or "SUMMARY")
                if(!properties.TryAdd(key,(header,value)))throw new InvalidDataException("Repeated ICS property: "+key);
        }
        if(!calendarSeen||!calendarClosed||components.Count!=0)throw new InvalidDataException("Incomplete ICS calendar.");
        return new(events.AsReadOnly(),skipped);
    }

    private static IEnumerable<string> Unfold(string text)
    {
        using var reader=new StringReader(text);
        var current=new StringBuilder();
        bool hasLine=false;
        while(reader.ReadLine() is { } line)
        {
            if(line.StartsWith(' ')||line.StartsWith('\t'))
            {
                if(!hasLine)throw new InvalidDataException("Invalid ICS continuation.");
                current.Append(line.AsSpan(1));
            }
            else
            {
                if(hasLine)yield return current.ToString();
                current.Clear();current.Append(line);hasLine=true;
            }
            if(current.Length>MaxLineLength)throw new InvalidDataException("ICS line exceeds its size limit.");
        }
        if(hasLine)yield return current.ToString();
    }

    private static bool TryEvent(Dictionary<string,(string Header,string Value)> p,out PersonalEvent entry,out string uid)
    {
        entry=null!;uid="";
        if(!p.TryGetValue("UID",out var id)||!id.Header.Equals("UID",StringComparison.OrdinalIgnoreCase)||
            string.IsNullOrWhiteSpace(id.Value)||id.Value.Length>512||id.Value.Any(char.IsControl)||
            !p.TryGetValue("DTSTART",out var start)||!Date(start,"DTSTART",out var date)||
            !p.TryGetValue("SUMMARY",out var summary)||!summary.Header.Equals("SUMMARY",StringComparison.OrdinalIgnoreCase))return false;
        if(p.TryGetValue("DTEND",out var end)&&(!Date(end,"DTEND",out var endDate)||date==DateOnly.MaxValue||endDate!=date.AddDays(1)))return false;
        var title=Unescape(summary.Value);
        if(title is null||string.IsNullOrWhiteSpace(title)||title.Length>PersonalCalendar.MaxTitle||title.Any(char.IsControl))return false;
        uid=id.Value;
        // The app's own exported occurrence ID retains the original event identity.
        var suffix="-"+date.ToString("yyyyMMdd",CultureInfo.InvariantCulture)+"@chronikologio.local";
        Guid guid;
        if(uid.EndsWith(suffix,StringComparison.Ordinal)&&Guid.TryParseExact(uid[..^suffix.Length],"N",out var own)&&own!=Guid.Empty)
            guid=own;
        else
            guid=new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("Chronikologio/ICS/"+uid)).AsSpan(0,16));
        entry=new PersonalEvent(guid,date,title.Trim(),PersonalEventKind.Other,false);
        return true;
    }

    private static bool Date((string Header,string Value) p,string name,out DateOnly date)
    {
        date=default;
        return p.Header.Equals(name+";VALUE=DATE",StringComparison.OrdinalIgnoreCase)&&
            DateOnly.TryParseExact(p.Value,"yyyyMMdd",CultureInfo.InvariantCulture,DateTimeStyles.None,out date);
    }

    private static string? Unescape(string value)
    {
        var sb=new StringBuilder();
        for(int i=0;i<value.Length;i++)
        {
            var c=value[i];
            if(c!='\\'){sb.Append(c);continue;}
            if(++i>=value.Length)return null;
            switch(value[i])
            {
                case '\\':sb.Append('\\');break;
                case ',':sb.Append(',');break;
                case ';':sb.Append(';');break;
                case 'n':case 'N':sb.Append(' ');break;
                default:return null;
            }
        }
        return sb.ToString();
    }
}
