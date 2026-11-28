namespace Chronikologio.Core;

/// <summary>Read-only projection of the existing DayStore. No secondary historical database.</summary>
public sealed class HistoryCatalog
{
    public sealed record Entry(string Key,string Kind,int Month,int Day,int? Year,string Title,string Summary,
        string Region,Verification? Status,IReadOnlyList<SourceRef> Sources,string? Details);
    private sealed record Indexed(Entry Value,string SearchText);
    private readonly Indexed[] _entries;
    public HistoryCatalog(DayStore store,string language)
    {
        var list=new List<Entry>();
        // A leap-year template includes every month/day, not invented historical dates.
        for (var date=new DateOnly(2000,1,1);date.Year==2000;date=date.AddDays(1))
        {
            var day=store.GetDayIn(date.Month,date.Day,language);
            foreach (var e in day.Events)
                list.Add(new(e.Id??EntryIds.Make("event",date.Month,date.Day,e.Year,e.Title),"event",date.Month,date.Day,e.Year,e.Title,e.Summary,e.Region,e.Status,e.Sources,e.Details));
            void People(IEnumerable<Person> people,string kind)
            {
                foreach (var p in people)
                    list.Add(new(p.Id??EntryIds.Make(kind,date.Month,date.Day,p.Year,p.Name),kind,date.Month,date.Day,p.Year,p.Name,p.Description,p.Region,null,Array.Empty<SourceRef>(),p.Details));
            }
            People(day.People,"birth");
            People(day.DeathList,"death");
        }
        _entries=list.GroupBy(x=>x.Key,StringComparer.Ordinal).Select(g=>g.First())
            .OrderBy(x=>x.Year??int.MaxValue).ThenBy(x=>x.Month).ThenBy(x=>x.Day).ThenBy(x=>x.Title,StringComparer.OrdinalIgnoreCase)
            .Select(x=>new Indexed(x,TextNorm.Fold(x.Title+" "+x.Summary+" "+x.Region))).ToArray();
    }
    public IReadOnlyList<string> Regions => _entries.Select(x=>x.Value.Region).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.CurrentCultureIgnoreCase).ToArray();
    public Entry? FindById(string id) => _entries.FirstOrDefault(x=>x.Value.Key==id)?.Value;
    public sealed record Page(int Total,IReadOnlyList<Entry> Entries);
    public Page Search(string? text,string kind,int? fromYear,int? toYear,bool verifiedOnly,int offset=0,int limit=100,string? region=null)
    {
        if (fromYear.HasValue && toYear.HasValue && fromYear>toYear) throw new ArgumentException("Reversed year range.");
        if (kind is not ("all" or "event" or "birth" or "death")) throw new ArgumentException("Unknown kind.");
        if (offset<0 || limit<1 || limit>500) throw new ArgumentOutOfRangeException(nameof(limit));
        var query=TextNorm.Fold((text??"").Trim());
        var result=new List<Entry>(limit);
        int total=0;
        foreach (var x in _entries)
        {
            var e=x.Value;
            if (kind!="all" && e.Kind!=kind || verifiedOnly && e.Status!=Verification.Verified ||
                !string.IsNullOrEmpty(region) && !string.Equals(region,e.Region,StringComparison.Ordinal) ||
                fromYear.HasValue && (!e.Year.HasValue || e.Year.Value<fromYear.Value) ||
                toYear.HasValue && (!e.Year.HasValue || e.Year.Value>toYear.Value) ||
                query.Length>0 && !x.SearchText.Contains(query,StringComparison.Ordinal)) continue;
            if (total>=offset && result.Count<limit) result.Add(e);
            total++;
        }
        return new(total,result.AsReadOnly());
    }
    public sealed record QuizQuestion(Entry Evidence,IReadOnlyList<int> Choices,int AnswerYear);

    /// <summary>Questions use only uniquely titled, explicitly verified, dated events with an HTTPS source.
    /// Distractors are years from other eligible records, not invented claims.
    /// Verification status is inherited from the library, not independent legal clearance.</summary>
    public IReadOnlyList<QuizQuestion> CreateYearQuiz(int count,Random random,string? region=null,int? fromYear=null,int? toYear=null)
    {
        if(count<1||count>20) throw new ArgumentOutOfRangeException(nameof(count));
        if(fromYear.HasValue&&toYear.HasValue&&fromYear>toYear) throw new ArgumentException("Reversed year range.");
        var candidates=_entries.Select(x=>x.Value).Where(e=>e.Kind=="event"&&e.Status==Verification.Verified&&
            e.Year is int y && y!=0 && !string.IsNullOrWhiteSpace(e.Title) &&
            e.Sources.Any(s=>Uri.TryCreate(s.Url,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&string.IsNullOrEmpty(uri.UserInfo)))
            .GroupBy(e=>TextNorm.Fold(e.Title),StringComparer.Ordinal)
            .Where(g=>g.Select(e=>e.Year).Distinct().Count()==1).Select(g=>g.First()).ToArray();
        var pool=candidates.Select(e=>e.Year!.Value).Distinct().ToArray();
        if(pool.Length<4)return Array.Empty<QuizQuestion>();
        var eligible=candidates.Where(e=>(string.IsNullOrEmpty(region)||e.Region==region)&&
            (!fromYear.HasValue||e.Year!.Value>=fromYear.Value)&&(!toYear.HasValue||e.Year!.Value<=toYear.Value)).ToArray();
        random.Shuffle(eligible);
        var result=new List<QuizQuestion>();
        foreach(var e in eligible.Take(count))
        {
            var year=e.Year!.Value;
            var options=pool.Where(x=>x!=year).ToArray();
            random.Shuffle(options);
            var choices=options.Take(3).Append(year).ToArray();
            random.Shuffle(choices);
            result.Add(new(e,Array.AsReadOnly(choices),year));
        }
        return result.AsReadOnly();
    }

}
