using System.Runtime.InteropServices;
using Chronikologio.Core;

namespace Chronikologio.UI;

/// <summary>Dark (or light) scroll bars for scrollable controls, the way the other Opsis apps do it (the Windows "DarkMode_Explorer" visual style). Safe to call repeatedly.</summary>
public static class ThemeNative
{
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? appName, string? idList);

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> Hooked = new();

    public static void StyleScrollbars(Control c)
    {
        void Set()
        {
            try { SetWindowTheme(c.Handle, ChronikologioTheme.IsDark ? "DarkMode_Explorer" : "Explorer", null); }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
        }
        if (c.IsHandleCreated) Set();
        if (Hooked.TryGetValue(c, out _)) return;
        Hooked.Add(c, new object());
        c.HandleCreated += (_, _) => Set();
    }
}

/// <summary>
/// The side menu at the left edge of the window, in the style of the sidebar of Opsis: a brand block on top (the animated logo, the title and the version of the app),
/// then the buttons (today, theme, settings, help) with their names. Drawn in the colours of the theme.
/// </summary>
public sealed class SideRail : Control
{
    public sealed record RailItem(string Key, string Glyph, string Tip, Action Click, string Label = "");

    private readonly List<RailItem> _items = new();
    private readonly ToolTip _tip = new();
    private readonly Action _tick;
    private int _hover = -1;
    private string? _checked;

    public const int RailWidth = 224;
    public const int BrandHeight = 88;

    /// <summary>The title, the second line and the version shown next to the logo.</summary>
    public string BrandTitle { get; set; } = "";
    public string BrandSub { get; set; } = "";
    public string BrandVersion { get; set; } = "";

    public SideRail()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Dock = DockStyle.Left;
        Width = RailWidth;
        Cursor = Cursors.Hand;
        _tick = () => { if (WaveClock.ShouldDraw(this)) Invalidate(new Rectangle(0, 0, Width, BrandHeight)); };   // only the logo moves
        WaveClock.Subscribe(_tick);
    }

    public IReadOnlyList<RailItem> Items => _items;

    public void SetItems(IEnumerable<RailItem> items)
    {
        _items.Clear();
        _items.AddRange(items);
        Invalidate();
    }

    /// <summary>The key of the item drawn as selected, or null.</summary>
    public string? Checked
    {
        get => _checked;
        set { _checked = value; Invalidate(); }
    }

    private string? _pulse;

    /// <summary>Draws an item as pressed for a short moment.</summary>
    public void Pulse(string key)
    {
        _pulse = key;
        Invalidate();
        var timer = new System.Windows.Forms.Timer { Interval = 350 };
        timer.Tick += (_, _) => { timer.Stop(); timer.Dispose(); _pulse = null; Invalidate(); };
        timer.Start();
    }

    private Rectangle Cell(int index) => new(8, BrandHeight + 6 + index * 46, Width - 16, 42);

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = ChronikologioTheme.Palette;
        var g = e.Graphics;
        g.Clear(p.Surface);
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        DrawBrand(g, p);
        using var glyphFont = new Font("Segoe MDL2 Assets", 14f);
        using var labelFont = new Font("Segoe UI", 10.5f);
        using var accent = new SolidBrush(p.Accent);
        using var hoverBrush = new SolidBrush(Color.FromArgb(40, p.Text));
        for (var i = 0; i < _items.Count; i++)
        {
            var r = Cell(i);
            var selected = _items[i].Key == _checked || _items[i].Key == _pulse;
            var color = selected ? p.Accent : p.TextMuted;
            if (i == _hover || _items[i].Key == _pulse) g.FillRectangle(hoverBrush, r);
            if (selected) g.FillRectangle(accent, 0, r.Y + 8, 3, r.Height - 16);
            var iconCell = new Rectangle(r.X, r.Y, 44, r.Height);
            if (_items[i].Key == "help") DrawInfo(g, iconCell, color);
            else TextRenderer.DrawText(g, _items[i].Glyph, glyphFont, iconCell, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, _items[i].Label, labelFont, new Rectangle(r.X + 46, r.Y, r.Width - 50, r.Height), selected ? p.Accent : p.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }
        using var edge = new Pen(Color.FromArgb(60, p.Text));
        g.DrawLine(edge, Width - 1, 0, Width - 1, Height);
        g.DrawLine(edge, 12, BrandHeight, Width - 14, BrandHeight);
    }

    /// <summary>The brand block of Opsis: a rounded logo (gradient, gloss, a still wave and a bright dot that runs along it, a halo that pulses), the title and the version.</summary>
    private void DrawBrand(Graphics g, Opsis.UI.OpsisPalette p)
    {
        var t = WaveClock.Time * 3.3;   // the same pace as the logo of Opsis
        var old = g.SmoothingMode;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var state = g.Save();
        g.TranslateTransform(14, 16);
        const float size = 56f;
        var scale = size / 36f;
        g.ScaleTransform(scale, scale);
        var cyan = Color.FromArgb(0, 180, 216); var blue = Color.FromArgb(58, 110, 214);
        using (var halo = new SolidBrush(Color.FromArgb((int)(25 + (Math.Sin(t) * 0.5 + 0.5) * 55), cyan))) g.FillEllipse(halo, -2, -2, 40, 40);
        var rect = new Rectangle(2, 2, 32, 32);
        using (var path = Rounded(rect, 8))
        {
            using (var back = new System.Drawing.Drawing2D.LinearGradientBrush(rect, cyan, blue, 45f)) g.FillPath(back, path);
            var oldClip = g.Clip;
            g.SetClip(path, System.Drawing.Drawing2D.CombineMode.Intersect);
            var gloss = new Rectangle(rect.X, rect.Y, rect.Width, rect.Height / 2 + 4);
            using (var glossBrush = new System.Drawing.Drawing2D.LinearGradientBrush(gloss, Color.FromArgb(120, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f)) g.FillRectangle(glossBrush, gloss);
            g.Clip = oldClip;
        }
        PointF p0 = new(8, 20), p1 = new(13, 13), p2 = new(21, 27), p3 = new(26, 20);
        using (var wave = new Pen(Color.FromArgb(150, 255, 255, 255), 2.2f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round })
            g.DrawBezier(wave, p0, p1, p2, p3);
        var u = (t * 0.22) % 1.0; var m = 1 - u;
        var nx = (float)(m * m * m * p0.X + 3 * m * m * u * p1.X + 3 * m * u * u * p2.X + u * u * u * p3.X);
        var ny = (float)(m * m * m * p0.Y + 3 * m * m * u * p1.Y + 3 * m * u * u * p2.Y + u * u * u * p3.Y);
        using (var nodeHalo = new SolidBrush(Color.FromArgb(110, 255, 255, 255))) g.FillEllipse(nodeHalo, nx - 5, ny - 5, 10, 10);
        g.FillEllipse(Brushes.White, nx - 2.3f, ny - 2.3f, 4.6f, 4.6f);
        g.Restore(state);
        g.SmoothingMode = old;

        const int x = 14 + 56 + 10;
        var width = Width - x - 8;
        using var titleFont = new Font("Segoe UI", 12.5f, FontStyle.Bold);
        using var smallFont = new Font("Segoe UI", 8.5f);
        TextRenderer.DrawText(g, BrandTitle, titleFont, new Rectangle(x, 16, width, 24), p.Text, TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, BrandSub, smallFont, new Rectangle(x, 42, width, 16), p.TextMuted, TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, BrandVersion, smallFont, new Rectangle(x, 58, width, 16), p.TextMuted, TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
    }

    private static System.Drawing.Drawing2D.GraphicsPath Rounded(Rectangle r, int radius)
    {
        var d = radius * 2;
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>The icon of the About page of Opsis: a circle with a dot and a stem.</summary>
    private static void DrawInfo(Graphics g, Rectangle cell, Color color)
    {
        var old = g.SmoothingMode;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var pen = new Pen(color, 1.8f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        using var brush = new SolidBrush(color);
        var cx = cell.X + cell.Width / 2f; var cy = cell.Y + cell.Height / 2f; const float r = 10f;
        g.DrawEllipse(pen, cx - r, cy - r, 2 * r, 2 * r);
        g.FillEllipse(brush, cx - 1.4f, cy - 5.6f, 2.8f, 2.8f);
        g.DrawLine(pen, cx, cy - 1.2f, cx, cy + 5.5f);
        g.SmoothingMode = old;
    }

    private int IndexAt(Point point)
    {
        for (var i = 0; i < _items.Count; i++) if (Cell(i).Contains(point)) return i;
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var i = IndexAt(e.Location);
        if (i == _hover) return;
        _hover = i;
        if (i >= 0) _tip.Show(_items[i].Tip, this, Width + 4, Cell(i).Y + 10, 2000); else _tip.Hide(this);
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = -1;
        _tip.Hide(this);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        var i = IndexAt(e.Location);
        if (i >= 0) _items[i].Click();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { WaveClock.Unsubscribe(_tick); _tip.Dispose(); }
        base.Dispose(disposing);
    }
}

/// <summary>Tabs that follow the theme: a header strip drawn with the palette (the system tab control cannot be made dark) above the page of the chosen tab.</summary>
public sealed class TabHost : Panel
{
    private sealed class Header : Control
    {
        private readonly TabHost _owner;
        private int _hover = -1;

        public Header(TabHost owner)
        {
            _owner = owner;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Dock = DockStyle.Top;
            Height = 36;
            Cursor = Cursors.Hand;
        }

        private Rectangle Cell(int i) => new(i * 150, 0, 150, Height);

        protected override void OnPaint(PaintEventArgs e)
        {
            var p = ChronikologioTheme.Palette;
            e.Graphics.Clear(p.Background);
            for (var i = 0; i < _owner._pages.Count; i++)
            {
                var r = Cell(i);
                var selected = i == _owner.SelectedIndex;
                if (selected)
                {
                    using var back = new SolidBrush(p.Surface);
                    e.Graphics.FillRectangle(back, r);
                    using var line = new SolidBrush(p.Accent);
                    e.Graphics.FillRectangle(line, r.X, r.Bottom - 3, r.Width, 3);
                }
                else if (i == _hover)
                {
                    using var back = new SolidBrush(Color.FromArgb(30, p.Text));
                    e.Graphics.FillRectangle(back, r);
                }
                TextRenderer.DrawText(e.Graphics, _owner._pages[i].Text, Font, r, selected ? p.Accent : p.Text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var i = e.X / 150;
            if (i >= _owner._pages.Count) i = -1;
            if (i != _hover) { _hover = i; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = -1; Invalidate(); }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            var i = e.X / 150;
            if (e.Button == MouseButtons.Left && i < _owner._pages.Count) _owner.SelectedIndex = i;
        }
    }

    private readonly List<Control> _pages = new();
    private readonly Header _header;
    private readonly Panel _body = new() { Dock = DockStyle.Fill };
    private int _selected = -1;

    public event Action? SelectedChanged;

    public TabHost()
    {
        _header = new Header(this);
        Controls.Add(_body);
        Controls.Add(_header);
    }

    /// <summary>Adds pages; the Text of each page is the title of its tab.</summary>
    public void AddPages(params Control[] pages)
    {
        foreach (var page in pages)
        {
            page.Dock = DockStyle.Fill;
            page.Visible = false;
            _pages.Add(page);
            _body.Controls.Add(page);
        }
        if (_selected < 0 && _pages.Count > 0) SelectedIndex = 0;
        _header.Invalidate();
    }

    public void RefreshTitles() => _header.Invalidate();

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            if (value < 0 || value >= _pages.Count || value == _selected) return;
            _selected = value;
            for (var i = 0; i < _pages.Count; i++) _pages[i].Visible = i == _selected;
            _header.Invalidate();
            SelectedChanged?.Invoke();
        }
    }

    public Control? Selected
    {
        get => _selected >= 0 ? _pages[_selected] : null;
        set { var i = value is null ? -1 : _pages.IndexOf(value); if (i >= 0) SelectedIndex = i; }
    }

    public void Restyle()
    {
        var p = ChronikologioTheme.Palette;
        BackColor = p.Background;
        _body.BackColor = p.Background;
        foreach (var page in _pages) page.BackColor = p.Background;
        _header.Invalidate();
    }
}
