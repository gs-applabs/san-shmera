using Chronikologio.Core;
using Opsis.UI;
using System.Globalization;

namespace Chronikologio.UI;

/// <summary>Session-only learning activity. No timer, telemetry, stored score or duplicate data.</summary>
public sealed class HistoryQuizDialog : Form
{
    private readonly IReadOnlyList<HistoryCatalog.QuizQuestion> _questions;
    private readonly Label _progress=new(){Dock=DockStyle.Top,Height=35,TextAlign=ContentAlignment.MiddleLeft};
    private readonly Label _prompt=new(){Dock=DockStyle.Top,Height=86,TextAlign=ContentAlignment.MiddleLeft};
    private readonly FlowLayoutPanel _choices=new(){Dock=DockStyle.Top,Height=176,FlowDirection=FlowDirection.TopDown,WrapContents=false};
    private readonly RadioButton[] _options=new RadioButton[4];
    private readonly TextBox _feedback=new(){Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical};
    private readonly FlatButton _check=new(){Height=36,Width=145},_next=new(){Height=36,Width=145};
    private int _index,_score;
    private bool _answered;
    private static string T(string el,string en)=>Loc.Language=="en"?en:el;
    private static string Year(int year)=>year<0?(-(long)year).ToString(CultureInfo.InvariantCulture)+T(" π.Χ."," BCE"):year.ToString(CultureInfo.InvariantCulture);

    private HistoryQuizDialog(IReadOnlyList<HistoryCatalog.QuizQuestion> questions)
    {
        _questions=questions;
        var p=ChronikologioTheme.Palette;
        Text=T("Κουίζ χρονολογιών","Year quiz");
        StartPosition=FormStartPosition.CenterParent;
        Size=new Size(650,560);MinimumSize=new Size(480,460);
        MinimizeBox=false;MaximizeBox=false;
        BackColor=p.Background;ForeColor=p.Text;Padding=new Padding(16);
        _feedback.BackColor=p.Surface;_feedback.ForeColor=p.Text;
        _feedback.AccessibleName=T("Απάντηση και πηγές","Answer and sources");
        for(int i=0;i<4;i++)
        {
            _options[i]=new RadioButton{Width=350,Height=34,ForeColor=p.Text,BackColor=p.Background};
            _choices.Controls.Add(_options[i]);
            _options[i].CheckedChanged+=(_,_)=>_check.Enabled=!_answered&&_options.Any(x=>x?.Checked==true);
        }
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=48,FlowDirection=FlowDirection.LeftToRight};
        var close=new FlatButton{Text=T("Κλείσιμο","Close"),Width=115,Height=36};
        buttons.Controls.AddRange(new Control[]{_check,_next,close});
        Controls.Add(_feedback);Controls.Add(_choices);Controls.Add(_prompt);Controls.Add(_progress);Controls.Add(buttons);
        _check.Text=T("Έλεγχος","Check answer");
        _check.Click+=(_,_)=>Answer();
        _next.Click+=(_,_)=>Advance();
        close.Click+=(_,_)=>Close();
        KeyPreview=true;KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Escape)Close();};
        ShowQuestion();
    }
    private void ShowQuestion()
    {
        _answered=false;
        var q=_questions[_index];
        _progress.Text=$"{_index+1}/{_questions.Count} · "+T("Σωστές: ","Correct: ")+_score;
        _prompt.Text=T("Ποιο έτος συνέβη το ακόλουθο γεγονός;","In which year did this event occur?")+"\n"+q.Evidence.Title;
        for(int i=0;i<_options.Length;i++)
        {
            _options[i].Checked=false;_options[i].Enabled=true;_options[i].Text=Year(q.Choices[i]);
        }
        _check.Enabled=false;_next.Enabled=false;_next.Text=T("Επόμενη","Next");
        _feedback.Text=T("Επιλέξτε μία απάντηση. Η πηγή εμφανίζεται μετά τον έλεγχο.","Choose one answer. The source appears after checking.");
    }
    private void Answer()
    {
        if(_answered)return;
        var selected=Array.FindIndex(_options,x=>x.Checked);
        if(selected<0)return;
        _answered=true;
        var q=_questions[_index];
        var correct=q.Choices[selected]==q.AnswerYear;
        if(correct)_score++;
        foreach(var option in _options)option.Enabled=false;
        _check.Enabled=false;_next.Enabled=true;
        _progress.Text=$"{_index+1}/{_questions.Count} · "+T("Σωστές: ","Correct: ")+_score;
        _feedback.Text=(correct?T("Σωστά!","Correct!"):T("Σωστή απάντηση: ","Correct answer: "))+Year(q.AnswerYear)+"\r\n\r\n"+q.Evidence.Summary+
            "\r\n\r\n"+string.Join("\r\n",q.Evidence.Sources.Select(s=>s.Citation+(string.IsNullOrWhiteSpace(s.Url)?"":"\r\n"+s.Url)));
        if(_index+1==_questions.Count)_next.Text=T("Αποτέλεσμα","Result");
    }
    private void Advance()
    {
        if(!_answered)return;
        if(_index+1<_questions.Count){_index++;ShowQuestion();return;}
        _prompt.Text=T("Ολοκληρώθηκε το κουίζ.","Quiz complete.");
        _feedback.Text=T("Σωστές απαντήσεις: ","Correct answers: ")+$"{_score}/{_questions.Count}";
        _choices.Visible=false;_next.Enabled=false;_check.Enabled=false;
    }
    public static void ShowFor(IWin32Window? owner,IReadOnlyList<HistoryCatalog.QuizQuestion> questions)
    {
        if(questions.Count==0)return;
        using var form=new HistoryQuizDialog(questions);
        AppIcon.Inherit(form,owner);
        form.ShowDialog(owner);
    }
}
