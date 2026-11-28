using Chronikologio.Core;
using Opsis.UI;
using System.Diagnostics;

namespace Chronikologio.UI;

/// <summary>Show original evidence, not a new verification claim. Browser opens only on explicit click.</summary>
public sealed class HistorySourcesDialog : Form
{
    private readonly ListView _list=new(){Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,MultiSelect=false,HideSelection=false};
    private readonly Label _status=new(){Dock=DockStyle.Bottom,Height=46,TextAlign=ContentAlignment.MiddleLeft};
    private readonly FlatButton _open=new(){Width=160,Height=36},_copy=new(){Width=150,Height=36};
    private static string T(string el,string en)=>Loc.Language=="en"?en:el;
    private HistorySourcesDialog(HistoryCatalog.Entry entry)
    {
        var p=ChronikologioTheme.Palette;
        Text=T("Πηγές τεκμηρίου","Entry sources");StartPosition=FormStartPosition.CenterParent;
        Size=new Size(780,480);MinimumSize=new Size(520,360);MinimizeBox=false;MaximizeBox=false;
        BackColor=p.Background;ForeColor=p.Text;Padding=new Padding(12);
        var heading=new Label{Dock=DockStyle.Top,Height=68,Text=entry.Title+"\n"+entry.Key,ForeColor=p.Text};
        _list.BackColor=p.Surface;_list.ForeColor=p.Text;
        _list.Columns.Add(T("Είδος","Kind"),125);_list.Columns.Add(T("Αναφορά","Citation"),285);_list.Columns.Add(T("Πηγή","Source"),300);
        foreach(var source in entry.Sources)
        {
            var uri=HistoryLink.SourceUri(source.Url);
            _list.Items.Add(new ListViewItem(new[]{source.Kind,source.Citation,source.Url??""}){Tag=uri});
        }
        ThemeNative.StyleScrollbars(_list);
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=48};
        _open.Text=T("Άνοιγμα πηγής","Open source");_copy.Text=T("Αντιγραφή URL","Copy URL");
        var close=new FlatButton{Width=115,Height=36,Text=T("Κλείσιμο","Close")};
        buttons.Controls.AddRange(new Control[]{_open,_copy,close});
        Controls.Add(_list);Controls.Add(_status);Controls.Add(buttons);Controls.Add(heading);
        _status.Text=entry.Sources.Count==0?T("Δεν υπάρχουν καταγεγραμμένες πηγές.","No recorded sources."):
            T("Οι πηγές εμφανίζονται όπως είναι καταγεγραμμένες. Δεν αποτελούν νέο έλεγχο.","Sources are displayed as recorded, not independently reverified.");
        _open.Enabled=_copy.Enabled=false;
        _list.SelectedIndexChanged+=(_,_)=>_open.Enabled=_copy.Enabled=Selected() is not null;
        _open.Click+=(_,_)=>OpenSource();
        _copy.Click+=(_,_)=>CopySource();
        close.Click+=(_,_)=>Close();
        KeyPreview=true;KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Escape)Close();};
    }
    private Uri? Selected()=>_list.SelectedItems.Count==1?_list.SelectedItems[0].Tag as Uri:null;
    private void OpenSource()
    {
        if(Selected() is not Uri uri)return;
        if(MessageBox.Show(this,T("Άνοιγμα εξωτερικής ιστοσελίδας;","Open external website?")+"\n"+uri.AbsoluteUri,
            Text,MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
        try{Process.Start(new ProcessStartInfo(uri.AbsoluteUri){UseShellExecute=true});}
        catch(Exception ex) when(ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {_status.Text=T("Αδυναμία ανοίγματος πηγής.","Unable to open source.");}
    }
    private void CopySource()
    {
        if(Selected() is not Uri uri)return;
        try{Clipboard.SetText(uri.AbsoluteUri);_status.Text=T("Αντιγράφηκε.","Copied.");}
        catch(System.Runtime.InteropServices.ExternalException){_status.Text=T("Μη διαθέσιμο πρόχειρο.","Clipboard unavailable.");}
    }
    public static void ShowFor(IWin32Window? owner,HistoryCatalog.Entry entry)
    {
        using var form=new HistorySourcesDialog(entry);
        AppIcon.Inherit(form,owner);
        form.ShowDialog(owner);
    }
}
