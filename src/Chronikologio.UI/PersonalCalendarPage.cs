using Chronikologio.Core;
using Opsis.UI;

namespace Chronikologio.UI;

/// <summary>Private events are opened only when requested. No timer, external sync or feed publication.</summary>
public sealed class PersonalCalendarPage : TimeToolsPage
{
    private readonly PersonalCalendar _store;
    private PersonalCalendarSnapshot? _snapshot;
    private Guid? _editing;
    private readonly TextBox _title = new() { Width = 250, MaxLength = PersonalCalendar.MaxTitle };
    private readonly TextBox _date = new() { Width = 120, MaxLength = 10 };
    private readonly TextBox _notes = new() { Multiline = true, MaxLength = PersonalCalendar.MaxNotes, ScrollBars = ScrollBars.Vertical };
    private readonly FlatComboBox _kind = new() { Width = 145, Height = 32 };
    private readonly CheckBox _yearly = new() { AutoSize = true };
    private readonly CheckBox _upcoming = new() { AutoSize = true, Checked = true };
    private readonly CheckBox _weekOnly = new() { AutoSize = true };
    private readonly Label _due = new() { TextAlign=ContentAlignment.MiddleLeft,Tag="card" };
    private readonly Label _status = new() { TextAlign = ContentAlignment.MiddleLeft, Tag = "muted" };
    private readonly ListView _list;
    private readonly FlatButton _save, _delete, _export;
    private readonly DateOnly _initialDate;

    private static string KindName(PersonalEventKind kind) => kind switch
    {
        PersonalEventKind.Birthday => T("Γενέθλια","Birthday"),
        PersonalEventKind.NameDay => T("Ονομαστική εορτή","Name day"),
        PersonalEventKind.Anniversary => T("Επέτειος","Anniversary"),
        PersonalEventKind.Reminder => T("Υπενθύμιση","Reminder"),
        _ => T("Άλλο","Other")
    };

    public PersonalCalendarPage(string path,DateOnly selected,bool startupReminder=false,Action<bool>? setStartupReminder=null) : base(
        T("Προσωπικό ημερολόγιο","Personal calendar"),
        T("Ιδιωτικές καταχωρίσεις μόνο στον υπολογιστή. Οι υπενθυμίσεις εμφανίζονται εδώ ή προαιρετικά στην εκκίνηση, χωρίς υπηρεσία παρασκηνίου.",
          "Private entries on this computer only. Reminders appear here or optionally at launch, with no background service."))
    {
        _store = new PersonalCalendar(path);
        _initialDate = selected;
        _title.AccessibleName = T("Τίτλος","Title");
        _date.AccessibleName = T("Ημερομηνία ΗΗ/ΜΜ/ΕΕΕΕ","Date DD/MM/YYYY");
        _kind.AccessibleName = T("Κατηγορία","Category");
        _notes.AccessibleName = T("Ιδιωτικές σημειώσεις","Private notes");
        _kind.SetItems(Enum.GetValues<PersonalEventKind>().Select(KindName),0);
        _yearly.Text = T("Κάθε έτος","Every year");
        _upcoming.Text = T("Μόνο επόμενες καταχωρίσεις","Upcoming only");
        Add(Row(Field(T("Τίτλος","Title"),75),_title,Field(T("Ημερομηνία","Date"),90),_date),48);
        Add(Row(Field(T("Κατηγορία","Category"),75),_kind,_yearly),48);
        AddLabel(T("Ιδιωτικές σημειώσεις","Private notes"),22);
        Add(_notes,72);
        _save = Button(T("Αποθήκευση","Save"),110);
        _delete = Button(T("Διαγραφή","Delete"),105);
        var add = Button(T("Νέα εγγραφή","New entry"),120);
        var reload = Button(T("Ανανέωση","Reload"),105);
        Add(Row(_save,_delete,add,reload),48);
        _weekOnly.Text = T("Επόμενες 7 ημέρες (μαζί με σήμερα)","Next 7 days (including today)");
        Add(Row(_upcoming,_weekOnly),35);
        Add(_due,38);
        if(setStartupReminder is not null)
        {
            var startup=new CheckBox{AutoSize=true,Checked=startupReminder,Text=T("Σύνοψη υπενθυμίσεων κατά την εκκίνηση","Reminder summary on launch")};
            startup.CheckedChanged+=(_,_)=>setStartupReminder(startup.Checked);
            Add(Row(startup),35);
        }
        _list = Table((T("Ημερομηνία","Date"),115),(T("Τίτλος","Title"),245),(T("Κατηγορία","Category"),125),(T("Ετήσια","Yearly"),75));
        Add(_list,260);
        _export = Button(T("Εξαγωγή επόμενων σε ICS","Export upcoming to ICS"),225);
        Add(Row(_export),48);
        var backup = Button(T("Αντίγραφο JSON","JSON backup"),155);
        var import = Button(T("Συγχώνευση JSON","Merge JSON"),170);
        var importIcs = Button(T("Εισαγωγή ICS","Import ICS"),150);
        Add(Row(backup,import,importIcs),48);
        importIcs.Click += (_,_) => ImportIcs();
        backup.Click += (_,_) => Backup();
        import.Click += (_,_) => Import();
        Add(_status,56);
        AddLabel(T("Τα δεδομένα αποθηκεύονται σε τοπικό αρχείο JSON, χωρίς κρυπτογράφηση. Η εξαγωγή ICS περιέχει τίτλους και ημερομηνίες, όχι σημειώσεις. Η 29/2 επαναλαμβάνεται 28/2 σε κοινό έτος.",
            "Data is stored in a local unencrypted JSON file. ICS exports titles and dates, not notes. Feb 29 recurs on Feb 28 in common years."),72,"muted");
        _save.Click += (_,_) => SaveEntry();
        _delete.Click += (_,_) => DeleteEntry();
        add.Click += (_,_) => ClearEditor();
        reload.Click += (_,_) => Reload();
        _upcoming.CheckedChanged += (_,_) => { if(!_upcoming.Checked)_weekOnly.Checked=false; _weekOnly.Enabled=_upcoming.Checked; RefreshList(); };
        _weekOnly.CheckedChanged += (_,_) => RefreshList();
        _list.SelectedIndexChanged += (_,_) => EditSelected();
        _export.Click += (_,_) => Export();
        ClearEditor(); Reload(); Restyle();
    }

    private static string Civil(DateOnly d) => d.ToString("dd/MM/yyyy",System.Globalization.CultureInfo.InvariantCulture);
    private void ClearEditor()
    {
        _editing = null;
        _title.Text = ""; _notes.Text = ""; _date.Text = Civil(_initialDate);
        _yearly.Checked = false;
        _kind.SetItems(Enum.GetValues<PersonalEventKind>().Select(KindName),0);
        _delete.Enabled = false;
        if (_list is not null) foreach (ListViewItem item in _list.SelectedItems.Cast<ListViewItem>().ToArray()) item.Selected = false;
    }

    private void Reload()
    {
        try
        {
            _snapshot = _store.Load();
            ClearEditor(); RefreshList();
            _save.Enabled = _export.Enabled = true;
            _status.Text = T("Τοπικές εγγραφές: ","Local entries: ")+_snapshot.Events.Count;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _snapshot = null; _list.Items.Clear(); _due.Text="";
            _save.Enabled = _delete.Enabled = _export.Enabled = false;
            _status.Text = T("Αδυναμία ανάγνωσης. Το αρχικό αρχείο διατηρήθηκε. ","Unable to read. Original file preserved. ")+ex.Message;
        }
    }

    private void RefreshList()
    {
        _list.BeginUpdate();
        try
        {
            _list.Items.Clear();
            if (_snapshot is null) { _due.Text="";return; }
            var today=DateOnly.FromDateTime(DateTime.Today);
            var dueToday=PersonalCalendar.DueWithin(_snapshot.Events,today,0);
            var dueWeek=PersonalCalendar.DueWithin(_snapshot.Events,today,6);
            _due.Text=T("Σήμερα: ","Today: ")+dueToday.Count+T(" · Επόμενες 7 ημέρες: "," · Next 7 days: ")+dueWeek.Count;
            var rows = _upcoming.Checked
                ? (_weekOnly.Checked ? dueWeek : PersonalCalendar.Upcoming(_snapshot.Events,today))
                : _snapshot.Events.OrderBy(x=>x.Date).Select(x=>new PersonalOccurrence(x,x.Date)).ToArray();
            foreach (var row in rows)
                _list.Items.Add(new ListViewItem(new[] {Civil(row.Date),row.Event.Title,KindName(row.Event.Kind),row.Event.Yearly ? T("Ναι","Yes") : T("Όχι","No")}) {Tag=row.Event.Id});
        }
        finally { _list.EndUpdate(); }
    }

    private void EditSelected()
    {
        if (_snapshot is null || _list.SelectedItems.Count != 1 || _list.SelectedItems[0].Tag is not Guid id) return;
        var entry = _snapshot.Events.FirstOrDefault(x=>x.Id==id);
        if (entry is null) return;
        _editing = id;
        _title.Text = entry.Title; _date.Text = Civil(entry.Date); _notes.Text = entry.Notes; _yearly.Checked = entry.Yearly;
        _kind.SetItems(Enum.GetValues<PersonalEventKind>().Select(KindName),(int)entry.Kind);
        _delete.Enabled = true;
    }

    private void SaveEntry()
    {
        if (_snapshot is null) return;
        if (!CalendarTools.TryParseCivilDate(_date.Text,out var date) || string.IsNullOrWhiteSpace(_title.Text) || _title.Text.Any(char.IsControl))
        { _status.Text = T("Συμπληρώστε έγκυρο τίτλο και ημερομηνία.","Enter a valid title and date."); return; }
        var entry = new PersonalEvent(_editing ?? Guid.NewGuid(),date,_title.Text.Trim(),(PersonalEventKind)Math.Max(0,_kind.SelectedIndex),_yearly.Checked,_notes.Text);
        Persist(_snapshot.Events.Where(x=>x.Id!=entry.Id).Append(entry));
    }

    private void DeleteEntry()
    {
        if (_snapshot is null || _editing is not Guid id) return;
        if (MessageBox.Show(FindForm(),T("Να διαγραφεί η επιλεγμένη εγγραφή;","Delete the selected entry?"),
            T("Προσωπικό ημερολόγιο","Personal calendar"),MessageBoxButtons.YesNo,MessageBoxIcon.Question) != DialogResult.Yes) return;
        Persist(_snapshot.Events.Where(x=>x.Id!=id));
    }

    private void Persist(IEnumerable<PersonalEvent> events)
    {
        if (_snapshot is null) return;
        try
        {
            _snapshot = _store.Save(_snapshot.Revision,events);
            ClearEditor(); RefreshList();
            _status.Text = T("Αποθηκεύτηκε τοπικά.","Saved locally.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException)
        { _status.Text = T("Δεν αποθηκεύτηκε. Ανανεώστε και δοκιμάστε ξανά. ","Not saved. Reload and retry. ")+ex.Message; }
    }

    private void Backup()
    {
        if (_snapshot is null) return;
        using var picker = new SaveFileDialog { Filter="JSON (*.json)|*.json",FileName="personal-backup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".json",OverwritePrompt=true };
        if (picker.ShowDialog(FindForm()) != DialogResult.OK) return;
        try
        {
            _store.WriteBackup(picker.FileName);
            _status.Text=T("Δημιουργήθηκε μη κρυπτογραφημένο αντίγραφο με σημειώσεις.","Unencrypted backup including notes created.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or ArgumentException)
        { _status.Text=T("Αποτυχία αντιγράφου: ","Backup failed: ")+ex.Message; }
    }

    private void Import()
    {
        if (_snapshot is null) return;
        using var picker = new OpenFileDialog { Filter="JSON (*.json)|*.json",CheckFileExists=true,Multiselect=false };
        if (picker.ShowDialog(FindForm()) != DialogResult.OK) return;
        if (MessageBox.Show(FindForm(),T("Προσθήκη νέων εγγραφών από το αντίγραφο; Οι υπάρχουσες δεν αντικαθίστανται.","Add new entries from the backup? Existing records will not be overwritten."),
            T("Συγχώνευση","Merge"),MessageBoxButtons.YesNo,MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            var result=_store.MergeBackup(picker.FileName,_snapshot.Revision);
            _snapshot=result.Snapshot;
            ClearEditor(); RefreshList();
            _status.Text=T("Προστέθηκαν: ","Added: ")+result.Added+T(" · Ίδιες: "," · Identical: ")+result.Identical+T(" · Συγκρούσεις (διατηρήθηκαν οι τοπικές): "," · Conflicts (local kept): ")+result.Conflicts;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or ArgumentException)
        { _status.Text=T("Δεν έγινε εισαγωγή: ","Import failed: ")+ex.Message; }
    }

    private void ImportIcs()
    {
        if (_snapshot is null) return;
        using var picker = new OpenFileDialog { Filter="iCalendar (*.ics)|*.ics",CheckFileExists=true,Multiselect=false };
        if (picker.ShowDialog(FindForm()) != DialogResult.OK) return;
        try
        {
            var preview=PersonalIcsImport.Read(picker.FileName);
            var message=T("Μονοήμερες ολοήμερες εγγραφές: ","Single-day all-day entries: ")+preview.Events.Count+
                T(" · Μη υποστηριζόμενες: "," · Unsupported: ")+preview.Skipped;
            if(preview.Events.Count==0){_status.Text=message;return;}
            if(MessageBox.Show(FindForm(),message+"\n\n"+T("Να εισαχθούν μόνο οι τίτλοι και ημερομηνίες; Οι υπάρχουσες εγγραφές διατηρούνται. Δεν εισάγονται σημειώσεις, επαναλήψεις, ώρες ή ειδοποιήσεις.",
                "Import titles and dates only? Existing entries are kept. Notes, recurrence, times and alarms are not imported."),
                T("Εισαγωγή ICS","ICS import"),MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
            var result=_store.MergeIcs(preview,_snapshot.Revision);
            _snapshot=result.Snapshot;
            ClearEditor();RefreshList();
            _status.Text=T("Προστέθηκαν: ","Added: ")+result.Added+T(" · Ίδιες: "," · Identical: ")+result.Identical+
                T(" · Συγκρούσεις: "," · Conflicts: ")+result.Conflicts+T(" · Παραλείφθηκαν: "," · Skipped: ")+preview.Skipped;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or ArgumentException)
        { _status.Text=T("Δεν έγινε εισαγωγή: ","Import failed: ")+ex.Message; }
    }

    private void Export()
    {
        if (_snapshot is null) return;
        using var picker = new SaveFileDialog { Filter = "iCalendar (*.ics)|*.ics", FileName = "personal-calendar.ics", OverwritePrompt = true };
        if (picker.ShowDialog(FindForm()) != DialogResult.OK) return;
        try
        {
            _store.WriteUpcomingIcs(picker.FileName,DateOnly.FromDateTime(DateTime.Today),DateTime.UtcNow);
            _status.Text = T("Η εξαγωγή ολοκληρώθηκε. Δεν περιλαμβάνονται σημειώσεις.","Export complete. Notes are not included.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or InvalidOperationException)
        { _status.Text = T("Αποτυχία εξαγωγής. ","Export failed. ")+ex.Message; }
    }
}
