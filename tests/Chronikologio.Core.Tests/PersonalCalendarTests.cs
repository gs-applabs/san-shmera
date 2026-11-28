using System.Text;
using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public sealed class PersonalCalendarTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(),"chron-private-"+Guid.NewGuid().ToString("N"));
    private string FileName => Path.Combine(_root,"personal.json");
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root,true); }
    private static PersonalEvent Entry(DateOnly date,bool yearly=false) => new(Guid.NewGuid(),date,"Γενέθλια",PersonalEventKind.Birthday,yearly,"Ιδιωτική σημείωση");

    [Fact]
    public void LocalPersistenceIsAtomicAndRejectsStaleRevision()
    {
        var a = new PersonalCalendar(FileName);
        var b = new PersonalCalendar(FileName);
        Assert.Empty(a.Load().Events);
        Assert.Equal("",a.Load().Revision);
        var entry = Entry(new(2020,2,29),true);
        var saved = a.Save("",new[]{entry});
        Assert.Equal(entry,Assert.Single(b.Load().Events));
        Assert.Throws<InvalidOperationException>(()=>b.Save("",Array.Empty<PersonalEvent>()));
        Assert.Equal(saved.Revision,a.Load().Revision);
        Assert.Empty(Directory.GetFiles(_root,"*.tmp"));
        Assert.Empty(a.Save(saved.Revision,Array.Empty<PersonalEvent>()).Events);
    }

    [Fact]
    public void CorruptOrDuplicateDataDoesNotOverwritePriorFile()
    {
        var store = new PersonalCalendar(FileName);
        var entry = Entry(new(2026,1,1));
        var state = store.Save("",new[]{entry});
        var old = File.ReadAllBytes(FileName);
        Assert.Throws<InvalidDataException>(()=>store.Save(state.Revision,new[]{entry,entry}));
        Assert.Equal(old,File.ReadAllBytes(FileName));
        File.WriteAllText(FileName,"{invalid");
        Assert.Throws<InvalidDataException>(()=>store.Load());
        Assert.Throws<InvalidDataException>(()=>store.Save("",Array.Empty<PersonalEvent>()));
        Assert.Equal("{invalid",File.ReadAllText(FileName));
    }

    [Fact]
    public void BackupMergePreservesLocalConflictsAndDoesNotDuplicateEntries()
    {
        var store=new PersonalCalendar(FileName);
        var original=Entry(new(2026,1,1));
        var state=store.Save("",new[]{original});
        var backup=Path.Combine(_root,"backup.json");
        store.WriteBackup(backup);
        Assert.Throws<IOException>(()=>store.WriteBackup(backup));
        Assert.Throws<InvalidOperationException>(()=>store.WriteBackup(FileName));
        var other=new PersonalCalendar(backup);
        var incoming=Entry(new(2027,1,1));
        other.Save(other.Load().Revision,new[]{original with {Title="Changed externally"},incoming});
        var result=store.MergeBackup(backup,state.Revision);
        Assert.Equal(1,result.Added);
        Assert.Equal(1,result.Conflicts);
        Assert.Contains(original,result.Snapshot.Events);
        Assert.Contains(incoming,result.Snapshot.Events);
        var again=store.MergeBackup(backup,result.Snapshot.Revision);
        Assert.Equal(0,again.Added);
        Assert.Equal(1,again.Identical);
        Assert.Equal(result.Snapshot.Revision,again.Snapshot.Revision);
        Assert.Throws<InvalidOperationException>(()=>store.MergeBackup(backup,state.Revision));
        File.WriteAllText(backup,"bad json");
        Assert.Throws<InvalidDataException>(()=>store.MergeBackup(backup,again.Snapshot.Revision));
        Assert.Equal(2,store.Load().Events.Count);
    }

    [Fact]
    public void UpcomingEventsRespectLeapPolicyAndOriginalDate()
    {
        var leap = Entry(new(2020,2,29),true);
        var past = Entry(new(2025,1,1));
        var future = Entry(new(2028,2,29),true);
        var next = PersonalCalendar.Upcoming(new[]{leap,past,future},new(2026,1,1));
        Assert.Equal(new[]{new DateOnly(2026,2,28),new DateOnly(2028,2,29)},next.Select(x=>x.Date).ToArray());
        Assert.Equal(new DateOnly(2028,2,29),Assert.Single(PersonalCalendar.Upcoming(new[]{leap},new(2028,2,29))).Date);
        Assert.Empty(PersonalCalendar.Upcoming(new[]{leap},DateOnly.MaxValue));
    }

    [Fact]
    public void IcsIsPrivateEscapedUtf8FoldedAndNotesAreOptIn()
    {
        var title = "Γιορτή, επέτειος; "+new string('Α',80);
        var e = Entry(new(2026,3,25)) with {Title=title,Notes="secret\r\nBEGIN:VEVENT"};
        var row = new PersonalOccurrence(e,e.Date);
        var ics = PersonalCalendar.ExportIcs(new[]{row},new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc));
        Assert.Contains("CLASS:PRIVATE\r\n",ics);
        Assert.Contains("DTSTART;VALUE=DATE:20260325\r\n",ics);
        Assert.Contains("DTEND;VALUE=DATE:20260326\r\n",ics);
        Assert.DoesNotContain("secret",ics);
        Assert.DoesNotContain("DESCRIPTION:",ics);
        Assert.All(ics.Split("\r\n"),line=>Assert.InRange(Encoding.UTF8.GetByteCount(line),0,75));
        Assert.Contains("SUMMARY:Γιορτή\\, επέτειος\\; ",ics.Replace("\r\n ",""));
        var detailed = PersonalCalendar.ExportIcs(new[]{row},DateTime.UtcNow,true).Replace("\r\n ","");
        Assert.Contains("DESCRIPTION:secret\\nBEGIN:VEVENT",detailed);
        var boundary = PersonalCalendar.ExportIcs(new[]{new PersonalOccurrence(e,DateOnly.MaxValue)},DateTime.UtcNow);
        Assert.Contains("DTSTART;VALUE=DATE:99991231",boundary);
        Assert.DoesNotContain("DTEND",boundary);
    }
}
