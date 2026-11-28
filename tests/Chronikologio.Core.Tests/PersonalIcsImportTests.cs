using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public sealed class PersonalIcsImportTests : IDisposable
{
    private readonly string _root=Path.Combine(Path.GetTempPath(),"chron-ics-"+Guid.NewGuid().ToString("N"));
    public void Dispose(){if(Directory.Exists(_root))Directory.Delete(_root,true);}
    private static string Calendar(params string[] events)=>"BEGIN:VCALENDAR\r\nVERSION:2.0\r\n"+string.Concat(events)+"END:VCALENDAR\r\n";
    private static string Event(string uid,string start,string extra="",string title="Γιορτή\\, φίλου")=>
        "BEGIN:VEVENT\r\nUID:"+uid+"\r\n"+start+"\r\nSUMMARY:"+title+"\r\n"+extra+"END:VEVENT\r\n";

    [Fact]
    public void PreviewImportsOnlySingleDayAllDayRecordsAndDoesNotImportNotes()
    {
        var input=Calendar(
            Event("a","DTSTART;VALUE=DATE:20260228","DTEND;VALUE=DATE:20260301\r\nDESCRIPTION:private\r\n"),
            Event("b","DTSTART:20260301T090000Z"),
            Event("c","DTSTART;VALUE=DATE:20260301","RRULE:FREQ=YEARLY\r\n"),
            Event("d","DTSTART;VALUE=DATE:20260301","DTEND;VALUE=DATE:20260303\r\n"),
            Event("e","DTSTART;VALUE=DATE:20260301","BEGIN:VALARM\r\nACTION:DISPLAY\r\nEND:VALARM\r\n"),
            Event("f","DTSTART;VALUE=DATE:20260301","ATTACH:https://example.invalid/a\r\n"));
        var result=PersonalIcsImport.Parse(input);
        Assert.Equal(5,result.Skipped);
        var e=Assert.Single(result.Events);
        Assert.Equal("Γιορτή, φίλου",e.Title);
        Assert.Equal(new DateOnly(2026,2,28),e.Date);
        Assert.Equal("",e.Notes);
        Assert.False(e.Yearly);
        Assert.Equal(e.Id,Assert.Single(PersonalIcsImport.Parse(input).Events).Id);
    }

    [Fact]
    public void OwnExportRoundTripsWithoutDuplicatingOrReplacingExistingEntry()
    {
        var store=new PersonalCalendar(Path.Combine(_root,"personal.json"));
        var original=new PersonalEvent(Guid.NewGuid(),new(2026,3,25),"Επέτειος",PersonalEventKind.Anniversary,true,"secret");
        var snapshot=store.Save("",new[]{original});
        var ics=PersonalCalendar.ExportIcs(new[]{new PersonalOccurrence(original,original.Date)},DateTime.UtcNow);
        var preview=PersonalIcsImport.Parse(ics);
        Assert.Equal(original.Id,Assert.Single(preview.Events).Id);
        var merged=store.MergeIcs(preview,snapshot.Revision);
        Assert.Equal(0,merged.Added);
        Assert.Equal(1,merged.Conflicts);
        Assert.Equal(original,Assert.Single(merged.Snapshot.Events));
        var external=PersonalIcsImport.Parse(Calendar(Event("external","DTSTART;VALUE=DATE:20260326")));
        var added=store.MergeIcs(external,snapshot.Revision);
        Assert.Equal(1,added.Added);
        var again=store.MergeIcs(external,added.Snapshot.Revision);
        Assert.Equal(1,again.Identical);
        Assert.Equal(0,again.Added);
    }

    [Fact]
    public void RejectsMalformedStructureAndDuplicateUidAndUnfoldsText()
    {
        var e=Event("a","DTSTART;VALUE=DATE:20260228",title:"Long\r\n continuation");
        Assert.Equal("Longcontinuation",Assert.Single(PersonalIcsImport.Parse(Calendar(e)).Events).Title);
        Assert.Throws<InvalidDataException>(()=>PersonalIcsImport.Parse(Calendar(e,e)));
        Assert.Throws<InvalidDataException>(()=>PersonalIcsImport.Parse("BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nEND:VCALENDAR"));
        Assert.Throws<InvalidDataException>(()=>PersonalIcsImport.Parse(Calendar(Event("a","DTSTART;VALUE=DATE:20260228",title:new string('x',9000)))));
    }
}
