using System.Globalization;
using Chronikologio.Core;
using Opsis.UI;

namespace Chronikologio.UI;

/// <summary>Shared, on-demand tools surface using the existing Opsis palette and buttons.</summary>
public abstract class TimeToolsPage : Panel
{
    private readonly TableLayoutPanel _stack = new() { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1 };
    protected static string T(string el, string en) => Loc.Language == "en" ? en : el;
    protected TimeToolsPage(string title, string subtitle)
    {
        Dock = DockStyle.Fill; AutoScroll = true; Padding = new Padding(20);
        _stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(_stack);
        AddLabel(title, 36, "title");
        AddLabel(subtitle, 54, "muted");
    }
    protected void Add(Control control, int height)
    {
        control.Dock = DockStyle.Fill; control.Margin = new Padding(0, 0, 0, 10);
        var row = _stack.RowCount++;
        _stack.RowStyles.Add(new RowStyle(SizeType.Absolute, height + 10));
        _stack.Controls.Add(control, 0, row);
    }
    protected void AddLabel(string text, int height, string tag = "") =>
        Add(new Label { Text = text, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Tag = tag }, height);
    protected static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { WrapContents = false, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(10, 5, 0, 0), Tag = "card" };
        foreach (var control in controls) { control.Margin = new Padding(0, 3, 10, 0); row.Controls.Add(control); }
        return row;
    }
    protected static Label Field(string text, int width = 105) => new() { Text = text, Width = width, Height = 32, TextAlign = ContentAlignment.MiddleLeft };
    protected static FlatButton Button(string text, int width = 155) => new() { Text = text, Width = width, Height = 32 };
    protected static DateTimePicker Picker(DateOnly date, int min = 1753, int max = 9998) => new()
    {
        MinDate = new DateTime(min, 1, 1), MaxDate = new DateTime(max, 12, 31),
        Value = new DateTime(Math.Clamp(date.Year, min, max), date.Month, Math.Min(date.Day, DateTime.DaysInMonth(Math.Clamp(date.Year, min, max), date.Month))),
        Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = 135
    };
    protected static NumericUpDown Number(int min, int max, int value, int width = 72) => new() { Minimum = min, Maximum = max, Value = value, Width = width };
    protected static ListView Table(params (string Name, int Width)[] columns)
    {
        var table = new ListView { View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false, BorderStyle = BorderStyle.None, Tag = "card" };
        foreach (var c in columns) table.Columns.Add(c.Name, c.Width);
        return table;
    }
    protected static void Rows(ListView table, IEnumerable<string[]> rows)
    {
        table.BeginUpdate();
        try { table.Items.Clear(); foreach (var row in rows) table.Items.Add(new ListViewItem(row)); }
        finally { table.EndUpdate(); }
    }
    public void Restyle()
    {
        var p = ChronikologioTheme.Palette;
        void Paint(Control c, Color background)
        {
            var bg = Equals(c.Tag, "card") ? p.Surface : background;
            c.BackColor = bg; c.ForeColor = Equals(c.Tag, "title") ? p.Accent : Equals(c.Tag, "muted") ? p.TextMuted : p.Text;
            foreach (Control child in c.Controls) Paint(child, bg);
        }
        Paint(this, p.Background);
        Invalidate(true);
    }
}

public sealed class CalendarToolsPage : TimeToolsPage
{
    private readonly DateTimePicker _first, _second;
    private readonly FlatComboBox _system = new() { Width = 170, Height = 32 };
    private readonly NumericUpDown _year = Number(1, 9999, 2026), _month = Number(1, 13, 1, 50), _day = Number(1, 31, 1, 50);
    private readonly ListView _conversions, _intervals;
    private readonly Label _reverse = new() { AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
    private static string NameOf(string system) => system switch
    {
        "gregorian" => T("Γρηγοριανό", "Gregorian"), "julian" => T("Ιουλιανό", "Julian"),
        "hebrew" => T("Εβραϊκό", "Hebrew"), "hijri" => T("Ισλαμικό (αριθμητικό)", "Islamic (tabular)"),
        _ => T("Περσικό", "Persian")
    };
    public CalendarToolsPage(DateOnly selected) : base(T("Ημερολογιακά εργαλεία", "Calendar tools"),
        T("Μετατροπές ημερομηνιών, χρονικές αποστάσεις και επέτειοι — χωρίς Internet.", "Offline calendar conversions, date intervals and anniversaries."))
    {
        _first = Picker(selected); _second = Picker(DateOnly.FromDateTime(DateTime.Today));
        _first.AccessibleName = T("Αρχική ημερομηνία", "Start date"); _second.AccessibleName = T("Τελική ημερομηνία", "End date");
        Add(Row(Field(T("Από", "From")), _first, Field(T("Έως", "To"), 48), _second), 50);
        _conversions = Table((T("Ημερολόγιο", "Calendar"), 280), (T("Ημερομηνία (Ε-Μ-Η)", "Date (Y-M-D)"), 230));
        Add(_conversions, 174);
        _intervals = Table((T("Υπολογισμός", "Calculation"), 280), (T("Αποτέλεσμα", "Result"), 300));
        Add(_intervals, 190);
        AddLabel(T("Αντίστροφη μετατροπή προς Γρηγοριανό", "Convert back to Gregorian"), 28, "title");
        _system.SetItems(CalendarTools.Systems.Select(NameOf), 1);
        _system.AccessibleName = T("Ημερολογιακό σύστημα", "Calendar system");
        _year.AccessibleName = T("Έτος", "Year"); _month.AccessibleName = T("Μήνας", "Month"); _day.AccessibleName = T("Ημέρα", "Day");
        _year.Value = selected.Year; _month.Value = selected.Month; _day.Value = selected.Day;
        var convert = Button(T("Μετατροπή", "Convert"), 120);
        Add(Row(_system, _year, _month, _day, convert), 50);
        Add(_reverse, 34);
        AddLabel(T("Προληπτικά ημερολόγια: όχι ιστορικές ημερομηνίες υιοθέτησης. Το αριθμητικό ισλαμικό μπορεί να διαφέρει από παρατηρήσεις Σελήνης. Στις επετείους 29/2 χρησιμοποιείται 28/2 σε κοινό έτος.", "Proleptic calendars, not historical adoption dates. Tabular Islamic dates may differ from observations. Feb 29 anniversaries use Feb 28 in common years."), 65, "muted");
        _first.ValueChanged += (_, _) => RefreshResults(); _second.ValueChanged += (_, _) => RefreshResults();
        convert.Click += (_, _) =>
        {
            try { _reverse.Text = CalendarTools.ToGregorian(CalendarTools.Systems[Math.Max(0,_system.SelectedIndex)], (int)_year.Value,(int)_month.Value,(int)_day.Value).ToString("dd/MM/yyyy"); }
            catch (ArgumentException) { _reverse.Text = T("Μη έγκυρη ημερομηνία για το συγκεκριμένο ημερολόγιο.", "Invalid date for this calendar."); }
        };
        RefreshResults(); Restyle();
    }
    private void RefreshResults()
    {
        var a = DateOnly.FromDateTime(_first.Value); var b = DateOnly.FromDateTime(_second.Value);
        Rows(_conversions, CalendarTools.Systems.Select(s =>
        {
            var d = CalendarTools.Convert(a, s);
            return new[] { NameOf(s), d is null ? T("Εκτός εύρους", "Out of range") : $"{d.Year:D4}-{d.Month:D2}-{d.Day:D2}" };
        }));
        var interval = CalendarTools.Between(a,b); var iso = CalendarTools.IsoWeek(a);
        var anniversary = CalendarTools.NextAnniversary(a,b);
        Rows(_intervals, new[]
        {
            new[] { T("Διαφορά ημερών (με πρόσημο)", "Signed days"), interval.TotalDays.ToString("N0", Loc.Culture) },
            new[] { T("Έτη / μήνες / ημέρες (απόλυτα)", "Years / months / days (absolute)"), $"{interval.Years} / {interval.Months} / {interval.Days}" },
            new[] { T("Εβδομάδα ISO της αρχικής", "Start ISO week"), $"{iso.Year}-W{iso.Week:D2}" },
            new[] { T("Δευτέρα–Παρασκευή (μαζί με άκρα)", "Mon–Fri (inclusive)"), CalendarTools.Weekdays(a,b).ToString("N0",Loc.Culture) },
            new[] { T("Επόμενη επέτειος από τη λήξη", "Next anniversary from end"), anniversary.ToString("dd/MM/yyyy") }
        });
    }
}
