using System.Globalization;
using Chronikologio.Core;
using Opsis.UI;

namespace Chronikologio.UI;

/// <summary>Loads the first available image of a day (the icon of the saint celebrated) from the embedded database.</summary>
public static class DayImages
{
    public static Image? First(DayStore store, DayView day, out ImageRef? image)
    {
        foreach (var candidate in day.SaintImages)
        {
            try
            {
                image = candidate;
                return ImageLoader.LoadFromStore(store, candidate);
            }
            catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or IOException) { }
        }
        image = null;
        return null;
    }
}

/// <summary>A flag that opens the menu of countries; choosing one raises <see cref="RegionChosen"/>.</summary>
public sealed class FlagButton : PictureBox
{
    private string _region = "gr";
    private readonly ToolTip _tip = new();

    public event Action<string>? RegionChosen;

    public FlagButton()
    {
        Size = new Size(44, 28);
        SizeMode = PictureBoxSizeMode.CenterImage;
        Cursor = Cursors.Hand;
        Click += (_, _) => ShowMenu();
    }

    public void SetRegion(string region)
    {
        if (_region != region || Image is null)
        {
            _region = region;
            var old = Image;
            Image = Flags.Create(region);
            old?.Dispose();
        }
        _tip.SetToolTip(this, Loc.T("flag.tip"));
    }

    private void ShowMenu()
    {
        var menu = new ContextMenuStrip { ShowImageMargin = true, ShowCheckMargin = false, Renderer = new FlatMenuRenderer() };
        foreach (var country in Countries.All)
        {
            var region = country.Region;
            menu.Items.Add(new ToolStripMenuItem(Loc.T("region." + region), Flags.Create(region, 24, 16), (_, _) => RegionChosen?.Invoke(region)) { Checked = region == _region });
        }
        menu.Closed += (_, _) =>
        {
            foreach (ToolStripItem item in menu.Items) item.Image?.Dispose();
            menu.Dispose();
        };
        menu.Show(this, new Point(0, Height));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { Image?.Dispose(); _tip.Dispose(); }
        base.Dispose(disposing);
    }
}

/// <summary>Month grid with its own title and arrows; used beside the text in the card and feed layouts.</summary>
public sealed class MiniCalendar : UserControl
{
    private readonly Label _title = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
    private readonly Label _prev = new() { Text = "◀", Dock = DockStyle.Left, Width = 28, TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand };
    private readonly Label _next = new() { Text = "▶", Dock = DockStyle.Right, Width = 28, TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand };
    private readonly CalendarGrid _grid = new() { Dock = DockStyle.Fill };
    private DayStore _store;
    private Font? _titleFont;

    public event Action<DateOnly>? DateChosen;

    public MiniCalendar(DayStore store)
    {
        _store = store;
        var nav = new Panel { Dock = DockStyle.Top, Height = 26 };
        nav.Controls.Add(_title);
        nav.Controls.Add(_prev);
        nav.Controls.Add(_next);
        Controls.Add(_grid);
        Controls.Add(nav);
        _prev.Click += (_, _) => { _grid.Month = _grid.Month.AddMonths(-1); UpdateTitle(); };
        _next.Click += (_, _) => { _grid.Month = _grid.Month.AddMonths(1); UpdateTitle(); };
        _grid.SelectedChanged += d => { UpdateTitle(); DateChosen?.Invoke(d); };
        SetStore(store);
        Restyle();
        UpdateTitle();
    }

    public DateOnly Selected
    {
        get => _grid.Selected;
        set => _grid.Selected = value;
    }

    public void SetStore(DayStore store)
    {
        _store = store;
        _grid.BindStore(store);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _titleFont?.Dispose(); _titleFont = null; }
        base.Dispose(disposing);
    }

    public void UpdateTitle() => _title.Text = _grid.Month.ToString("MMMM yyyy", Loc.Culture);

    public void Restyle()
    {
        var p = ChronikologioTheme.Palette;
        BackColor = p.Background;
        foreach (var c in new Control[] { _title, _grid }) { c.BackColor = p.Background; c.ForeColor = p.Text; }
        foreach (var c in new[] { _prev, _next }) { c.BackColor = p.Background; c.ForeColor = p.TextMuted; }
        if (_titleFont is null || _titleFont.FontFamily.Name != Font.FontFamily.Name || _titleFont.Size != Font.Size)
        {
            var oldFont = _titleFont;
            _title.Font = _titleFont = new Font(Font, FontStyle.Bold);
            oldFont?.Dispose();
        }
        _grid.Accent = p.Accent;
        _grid.Muted = p.TextMuted;
        _grid.Invalidate();
    }
}

/// <summary>A label that always stays on one line and ends with "…" when it does not fit (a plain Label wraps a long text onto a second line).</summary>
internal sealed class EllipsisLabel : Label
{
    public EllipsisLabel() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true); AutoEllipsis = true; }

    protected override void OnPaint(PaintEventArgs e)
    {
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
    }
}

/// <summary>The top bar shared by the layouts: flag, previous and next day, the date, today, and the fasting line.</summary>
public sealed class DayHeader : Panel
{
    private readonly FlagButton _flag = new() { Dock = DockStyle.Left, Width = 50 };
    private readonly Label _prev = new() { Text = "◀", Dock = DockStyle.Left, Width = 30, TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand };
    private readonly Label _next = new() { Text = "▶", Dock = DockStyle.Left, Width = 30, TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand };
    private readonly Label _date = new() { Dock = DockStyle.Fill, AutoSize = false, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly EllipsisLabel _old = new() { Dock = DockStyle.Fill, Tag = "muted" };
    private readonly Label _fast = new() { Dock = DockStyle.Bottom, Height = 22, AutoSize = false, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Tag = "muted" };
    private readonly ToolTip _tip = new();
    private readonly FlatButton _today = new() { Dock = DockStyle.Right, Width = 90 };

    public event Action? Previous;
    public event Action? Next;
    public event Action? Today;
    public event Action<string>? RegionChosen;

    public DayHeader()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        _tick = () => { if (WaveClock.ShouldDraw(this)) Invalidate(); };
        WaveClock.Subscribe(this, _tick);
        Dock = DockStyle.Top;
        Height = 70;
        Padding = new Padding(4, 4, 4, 2);
        // dock order: the last added is laid out first; the old-calendar note takes what the date and the buttons leave
        Controls.Add(_old);
        Controls.Add(_today);
        Controls.Add(_date);
        Controls.SetChildIndex(_date, 0); // Fill is laid out after the navigation and search controls.
        Controls.Add(_next);
        Controls.Add(_prev);
        Controls.Add(_flag);
        Controls.Add(_fast);   // the second row, over the whole width
        _prev.Click += (_, _) => Previous?.Invoke();
        _next.Click += (_, _) => Next?.Invoke();
        _today.Click += (_, _) => Today?.Invoke();
        _flag.RegionChosen += r => RegionChosen?.Invoke(r);
        _date.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
        ApplyText();
    }

    public void ApplyText() { _today.Text = Loc.T("btn.today"); _today.AccessibleName = _today.Text; }

    /// <summary>Puts the search of the window in the first row, just left of the Today button.</summary>
    public void AttachSearch(Control search)
    {
        search.Dock = DockStyle.Right;
        Controls.Add(search);
        Controls.SetChildIndex(search, Controls.GetChildIndex(_today));   // laid out right after the Today button, so it sits on its left
    }

    public void DetachSearch(Control search) => Controls.Remove(search);

    public void Set(DateOnly date, DayView day, string region)
    {
        _date.Text = date.ToString("dddd d MMMM yyyy", Loc.Culture);
        _tip.SetToolTip(_date, _date.Text); // retain the full date when the available width is narrow
        // the old-calendar note has no room in the first row (the search and the Today button need it): it goes to the second row, after the holiday
        _old.Text = ""; _old.Visible = false;
        var oldNote = OldCalendar.Note(date);
        var holiday = string.Join(" · ", day.HolidayList.Select(Holidays.Line));
        var lineOne = holiday.Length > 0 ? holiday : day.Fasting ?? "";
        _fast.Text = lineOne.Length > 0 && oldNote.Length > 0 ? lineOne + "  ·  " + oldNote : lineOne.Length > 0 ? lineOne : oldNote;
        _tip.SetToolTip(_fast, _fast.Text);
        _flag.SetRegion(region);
    }

    public void Restyle()
    {
        var p = ChronikologioTheme.Palette;
        BackColor = p.Background;
        // the labels are transparent: the waves of the header show through
        foreach (var c in new Control[] { _date, _flag }) { c.BackColor = Color.Transparent; c.ForeColor = p.Text; }
        _fast.BackColor = Color.Transparent; _fast.ForeColor = p.TextMuted;
        _old.BackColor = Color.Transparent; _old.ForeColor = p.TextMuted;
        foreach (var c in new[] { _prev, _next }) { c.BackColor = Color.Transparent; c.ForeColor = p.TextMuted; }
        Invalidate();
    }

    private readonly Action _tick;

    // Buffer the header itself, not the entire native child-window tree.
    // WS_EX_COMPOSITED can repeatedly schedule descendant painting with transparent controls.
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var p = ChronikologioTheme.Palette;
        e.Graphics.Clear(p.Background);
        Waves.Draw(e.Graphics, new Rectangle(0, 0, Width, Height), WaveClock.Time, p);
        using var line = new Pen(Color.FromArgb(70, p.Text), 1f);
        e.Graphics.DrawLine(line, 0, Height - 1, Width, Height - 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !IsDisposed) { WaveClock.Unsubscribe(_tick); _tip.Dispose(); _date.Font.Dispose(); }
        base.Dispose(disposing);
    }
}

/// <summary>
/// A way of showing a day. Three exist: cards (<see cref="CardsLayout"/>), a feed (<see cref="FeedLayout"/>) and tabs (<see cref="TabsLayout"/>);
/// the user picks one in the View menu. All share the data, the chosen day and the country.
/// </summary>
public abstract class DayLayout : UserControl
{
    protected DayStore Store;
    private DateOnly _date = DateOnly.FromDateTime(DateTime.Today);
    private bool _hasRendered;
    protected readonly DayHeader Header = new();

    public event Action<DateOnly>? DateChanged;
    public event Action<string>? RegionRequested;

    protected DayLayout(DayStore store)
    {
        Store = store;
        Header.Previous += () => ChangeDate(_date.AddDays(-1));
        Header.Next += () => ChangeDate(_date.AddDays(1));
        Header.Today += () => ChangeDate(DateOnly.FromDateTime(DateTime.Today));
        Header.RegionChosen += r => RegionRequested?.Invoke(r);
        Loc.Changed += OnLanguage;
        ChronikologioTheme.Changed += RestyleAll;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Loc.Changed -= OnLanguage;
            ChronikologioTheme.Changed -= RestyleAll;
        }
        base.Dispose(disposing);
    }

    public void AttachSearch(Control search) => Header.AttachSearch(search);

    public void DetachSearch(Control search) => Header.DetachSearch(search);

    /// <summary>Which of the three layouts this is: "cards", "feed" or "tabs".</summary>
    public abstract string Name { get; }

    public DateOnly SelectedDate
    {
        get => _date;
        set
        {
            if (value == _date) return;
            _date = value;
            Refresh_();
        }
    }

    /// <summary>The user chose a day: shows it and tells the window.</summary>
    protected void ChangeDate(DateOnly date)
    {
        if (date == _date) return;
        _date = date;
        Refresh_();
        DateChanged?.Invoke(date);
    }

    public void SetStore(DayStore store)
    {
        Store = store;
        StoreChanged();
        Refresh_();
    }

    protected virtual void StoreChanged() { }

    protected abstract void ShowDay(DayView day);

    private void Refresh_()
    {
        var day = Store.GetDay(_date);
        Header.Set(_date, day, Store.Region);
        ShowDay(day);
        _hasRendered = true;
    }

    private void OnLanguage()
    {
        Header.ApplyText();
        LanguageChanged();
        Refresh_();
    }

    protected virtual void LanguageChanged() { }

    protected void RestyleAll()
    {
        var p = ChronikologioTheme.Palette;
        BackColor = p.Background;
        Header.Restyle();
        Paint(this, p, false);
        if (!_hasRendered) Refresh_();
        OnPaletteChanged();
    }

    protected virtual void OnPaletteChanged() { }

    /// <summary>Applies the palette to a control tree: cards and inputs take the surface colour, the rest the background. Self-styled controls are left alone.</summary>
    /// <summary>Black or white, whichever reads better on the accent colour.</summary>
    public static Color OnAccent(Color accent)
    {
        static double Lin(int v) { var s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
        var l = 0.2126 * Lin(accent.R) + 0.7152 * Lin(accent.G) + 0.0722 * Lin(accent.B);
        return (l + 0.05) / 0.05 > 1.05 / (l + 0.05) ? Color.Black : Color.White;
    }

    protected static void Paint(Control c, OpsisPalette p, bool inCard)
    {
        var tag = c.Tag as string;
        var card = inCard || tag == "card";
        if (c is EventCarousel carousel) carousel.Restyle();
        // dark scroll bars (and light ones in a light theme) wherever a control can scroll
        if (c is ListView or TextBoxBase or ScrollableControl { AutoScroll: true }) ThemeNative.StyleScrollbars(c);
        switch (c)
        {
            case DayHeader or CalendarPanel: return;
            case MiniCalendar mini: mini.Restyle(); return;
            case Chip chip: chip.Restyle(); return;
            case YearTimeline line: line.Invalidate(); return;
            case FlatButton: return;
            case PictureBox: c.BackColor = card ? p.Surface : p.Background; return;
            case ListView or TextBoxBase or ComboBox:
                c.BackColor = p.Surface; c.ForeColor = p.Text; break;
            default:
                c.BackColor = card ? p.Surface : p.Background;
                c.ForeColor = tag == "muted" ? p.TextMuted : p.Text;
                break;
        }
        foreach (Control child in c.Controls) Paint(child, p, card);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RestyleAll();
    }
}

/// <summary>A titled box with a body text (and optionally an image beside it).</summary>
internal sealed class CardBox : Panel
{
    private readonly Label _title = new() { Dock = DockStyle.Top, Height = 24, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold) };
    private readonly TextBox _body = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, ScrollBars = ScrollBars.None, TabStop = false };
    private readonly PhotoBox _picture = new() { Dock = DockStyle.Left, Width = 0 };
    private readonly ToolTip _tip = new();

    private Image? _fitImage;

    // the picture column is as wide as the picture is at the height of the card: no empty band between an icon and the text
    private void FitPicture()
    {
        if (_fitImage is null) { _picture.Width = 0; return; }
        var h = Math.Max(120, _picture.Height > 30 ? _picture.Height : 180);
        var w = Math.Min(190, (int)Math.Round(_fitImage.Width * (h / (double)_fitImage.Height)));
        _picture.Width = Math.Max(90, w) + _picture.InsetRight;
    }

    public CardBox()
    {
        Tag = "card";
        Padding = new Padding(10, 8, 10, 8);
        Margin = new Padding(4);
        Dock = DockStyle.Fill;
        TextFit.ScrollWhenLonger(_body);   // a longer text than the card scrolls
        Controls.Add(_body);
        Controls.Add(_picture);
        Controls.Add(_title);
        Resize += (_, _) => FitPicture();
    }

    // A thin accent stripe at the left edge ties the cards to the accent colour of the theme
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var stripe = new SolidBrush(ChronikologioTheme.Palette.Accent);
        e.Graphics.FillRectangle(stripe, 0, 0, 3, Height);
    }

    public void Set(string title, string text, Image? image = null, string? credit = null)
    {
        _title.Text = title;
        _body.Text = text.Replace("\n", Environment.NewLine);
        _body.SelectionStart = 0;
        _body.SelectionLength = 0;
        _picture.Image?.Dispose();
        _picture.Image = image;
        _fitImage = image;
        FitPicture();
        if (credit is not null) _tip.SetToolTip(_picture, credit);
    }
}

/// <summary>Two frames side by side, Greece and abroad, each showing the events of the day one at a time with left and right arrows.</summary>
public sealed class EventFrames : TableLayoutPanel
{
    public EventCarousel Greek { get; } = new();
    public EventCarousel World { get; } = new();

    public EventFrames()
    {
        ColumnCount = 2;
        RowCount = 1;
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        Controls.Add(Greek, 0, 0);
        Controls.Add(World, 1, 0);
    }

    public void Set(DayStore store, DayView day)
    {
        // the first frame is the history of the country of the flag, the second everything else
        Greek.FrameTitle = Loc.TR("frame.gr", store.Region);
        World.FrameTitle = Loc.T("frame.world");
        Greek.SetEvents(store, day.Events.Where(e => Countries.IsDomestic(store.Region, e)).ToList(), Loc.TR("event.none.gr", store.Region));
        World.SetEvents(store, day.Events.Where(e => !Countries.IsDomestic(store.Region, e)).ToList(), Loc.T("event.none.world"));
    }
}
