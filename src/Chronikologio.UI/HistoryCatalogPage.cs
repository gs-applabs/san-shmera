using Chronikologio.Core;
using Opsis.UI;
using System.Globalization;

namespace Chronikologio.UI;

/// <summary>One shared chronological catalogue for existing historical and biographical records.</summary>
public sealed class HistoryCatalogPage : TimeToolsPage
{
    private readonly HistoryCatalog _catalog;
    private readonly TextBox _query=new(){Width=220,MaxLength=150}, _from=new(){Width=75,MaxLength=6}, _to=new(){Width=75,MaxLength=6};
    private readonly FlatComboBox _kind=new(){Width=145,Height=32};
    private readonly FlatComboBox _region=new(){Width=210,Height=32};
    private readonly string[] _regions;
    private readonly CheckBox _verified=new(){AutoSize=true};
    private readonly ListView _list;
    private readonly Label _status=new(){TextAlign=ContentAlignment.MiddleLeft,Tag="muted"};
    private readonly FlatButton _previous,_next;
    private int _offset;
    private const int PageSize=100;
    private static readonly string[] Kinds={"all","event","birth","death"};

    public HistoryCatalogPage(DayStore store) : base(T("Ιστορική βιβλιοθήκη","Historical library"),
        T("Χρονογραμμή γεγονότων, γεννήσεων και θανάτων από την υπάρχουσα βιβλιοθήκη. Δεν δημιουργείται δεύτερη βάση.","Timeline of events, births and deaths from the existing library. No secondary database."))
    {
        _catalog=new HistoryCatalog(store,Loc.Language);
        _regions=new[]{""}.Concat(_catalog.Regions).ToArray();
        _region.SetItems(_regions.Select(x=>x.Length==0?T("Όλες οι περιοχές","All regions"):x),0);
        _region.AccessibleName=T("Περιοχή","Region");
        _query.AccessibleName=T("Αναζήτηση","Search");
        _from.AccessibleName=T("Από έτος","From year");
        _to.AccessibleName=T("Έως έτος","To year");
        _kind.SetItems(Kinds.Select(KindName),0);
        _verified.Text=T("Μόνο επαληθευμένα γεγονότα","Verified events only");
        Add(Row(Field(T("Αναζήτηση","Search"),90),_query,_kind),48);
        var apply=Button(T("Εφαρμογή","Apply"),110);
        Add(Row(Field(T("Από έτος","From year"),80),_from,Field(T("Έως","To"),40),_to,apply),48);
        Add(Row(_verified),35);
        var quiz=Button(T("Κουίζ χρονολογιών","Year quiz"),170);
        Add(Row(Field(T("Περιοχή","Region"),90),_region,quiz),48);
        _list=Table((T("Έτος / ημέρα","Year / day"),145),(T("Τίτλος","Title"),260),(T("Τύπος","Type"),95),(T("Τεκμηρίωση","Evidence"),145));
        Add(_list,320);
        _previous=Button(T("Προηγούμενα","Previous"),125);
        _next=Button(T("Επόμενα","Next"),125);
        var detail=Button(T("Ανάγνωση","Read"),105);
        Add(Row(_previous,_next,detail),48);
        Add(_status,42);
        AddLabel(T("Αρνητικό έτος = π.Χ. Κενά έτη = ετήσιες μνήμες. Τα πρόσωπα δεν διαθέτουν ανεξάρτητη κατάσταση επαλήθευσης στο υπάρχον σχήμα.","Negative year = BCE. Missing year = annual observance. Existing person records have no independent verification field."),60,"muted");
        apply.Click+=(_,_)=>RefreshPage(0);
        _verified.CheckedChanged+=(_,_)=>RefreshPage(0);
        _kind.SelectedIndexChanged+=(_,_)=>RefreshPage(0);
        _region.SelectedIndexChanged+=(_,_)=>RefreshPage(0);
        quiz.Click+=(_,_)=>StartQuiz();
        _query.TextChanged+=(_,_)=>{_previous.Enabled=_next.Enabled=false;};
        _from.TextChanged+=(_,_)=>{_previous.Enabled=_next.Enabled=false;};
        _to.TextChanged+=(_,_)=>{_previous.Enabled=_next.Enabled=false;};
        _query.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter){RefreshPage(0);e.SuppressKeyPress=true;}};
        _previous.Click+=(_,_)=>RefreshPage(Math.Max(0,_offset-PageSize));
        _next.Click+=(_,_)=>RefreshPage(_offset+PageSize);
        detail.Click+=(_,_)=>Read();
        _list.DoubleClick+=(_,_)=>Read();
        RefreshPage(0); Restyle();
    }

    private static string KindName(string kind)=>kind switch
    {
        "event"=>T("Γεγονός","Event"),"birth"=>T("Γέννηση","Birth"),"death"=>T("Θάνατος","Death"),_=>T("Όλα","All")
    };
    private static string Evidence(Verification? status)=>status switch
    {
        Verification.Verified=>T("Επαληθευμένο","Verified"),Verification.Reviewed=>T("Επισκοπημένο","Reviewed"),
        Verification.Unverified=>T("Μη επαληθευμένο","Unverified"),_=>T("Χωρίς ένδειξη","Not rated")
    };
    private static string DateLabel(HistoryCatalog.Entry e)=>
        (e.Year is int y ? (y<0 ? (-(long)y).ToString(CultureInfo.InvariantCulture)+T(" π.Χ."," BCE") : y.ToString(CultureInfo.InvariantCulture)) : "—")+$" · {e.Day:D2}/{e.Month:D2}";
    private static bool Year(string text,out int? year)
    {
        year=null;
        if(string.IsNullOrWhiteSpace(text))return true;
        if(!int.TryParse(text,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var parsed)||parsed==0||parsed< -99999||parsed>9999)return false;
        year=parsed;return true;
    }
    private void RefreshPage(int offset)
    {
        if(!Year(_from.Text,out var from)||!Year(_to.Text,out var to)||(from.HasValue&&to.HasValue&&from>to))
        {
            _list.Items.Clear();_previous.Enabled=_next.Enabled=false;
            _status.Text=T("Μη έγκυρο εύρος ετών.","Invalid year range.");return;
        }
        var result=_catalog.Search(_query.Text,Kinds[Math.Clamp(_kind.SelectedIndex,0,Kinds.Length-1)],from,to,_verified.Checked,offset,PageSize,_regions[Math.Clamp(_region.SelectedIndex,0,_regions.Length-1)]);
        if(offset>0 && offset>=result.Total){RefreshPage(0);return;}
        _offset=offset;
        _list.BeginUpdate();
        try
        {
            _list.Items.Clear();
            foreach(var e in result.Entries)
                _list.Items.Add(new ListViewItem(new[]{DateLabel(e),e.Title,KindName(e.Kind),Evidence(e.Status)}){Tag=e});
        }
        finally{_list.EndUpdate();}
        _previous.Enabled=offset>0;_next.Enabled=offset+PageSize<result.Total;
        _status.Text=result.Total==0?T("Δεν βρέθηκαν εγγραφές.","No matching records."):$"{offset+1}–{offset+result.Entries.Count} / {result.Total}";
    }
    private void StartQuiz()
    {
        if(!Year(_from.Text,out var from)||!Year(_to.Text,out var to)||(from.HasValue&&to.HasValue&&from>to))
        { _status.Text=T("Μη έγκυρο εύρος ετών.","Invalid year range.");return; }
        var questions=_catalog.CreateYearQuiz(10,Random.Shared,_regions[Math.Clamp(_region.SelectedIndex,0,_regions.Length-1)],from,to);
        if(questions.Count==0)
        { _status.Text=T("Δεν υπάρχουν αρκετά επαληθευμένα γεγονότα με πηγές για κουίζ.","Not enough verified events with sources for a quiz.");return; }
        HistoryQuizDialog.ShowFor(FindForm(),questions);
    }
    private void Read()
    {
        if(_list.SelectedItems.Count!=1||_list.SelectedItems[0].Tag is not HistoryCatalog.Entry e)return;
        var body=e.Summary;
        if(!string.IsNullOrWhiteSpace(e.Details))body+="\n\n"+e.Details;
        body+="\n\n"+Evidence(e.Status)+"\n"+e.Region+"\nID: "+e.Key;
        foreach(var source in e.Sources)body+="\n"+source.Citation+(string.IsNullOrWhiteSpace(source.Url)?"":"\n"+source.Url);
        TextDialog.Show(FindForm(),e.Title,DateLabel(e)+" — "+e.Title,body);
    }
}
