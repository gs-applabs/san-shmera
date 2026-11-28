using Chronikologio.Core;

namespace Chronikologio.UI;

/// <summary>A row of images with their credit line (creator, licence, source) under each one. Images that are missing on disk are skipped.</summary>
public sealed class ImageStrip : FlowLayoutPanel
{
    private readonly List<Image> _loaded = new();
    private Font? _creditFont;

    public ImageStrip()
    {
        AutoScroll = true;
        WrapContents = false;
        FlowDirection = FlowDirection.LeftToRight;
    }

    public int ThumbnailHeight { get; set; } = 150;

    /// <summary>True: images are stacked in a column (for placing beside the text). False: one row (above or below the text).</summary>
    public bool Vertical { get; set; }

    public int ColumnWidth { get; set; } = 300;

    private readonly List<(Image Bitmap, PhotoBox Box, Label Credit)> _cells = new();
    private int _lastHeight;
    private bool _arranging;

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (Vertical && !_arranging && _cells.Count > 0 && Math.Abs(Height - _lastHeight) > 6)
            ArrangeCells();
    }

    private void ArrangeCells()
    {
        if (_arranging) return;
        _arranging = true;
        SuspendLayout();
        try
        {
            _lastHeight = Height;
            foreach (var (bitmap, box, credit) in _cells)
            {
                var width = Vertical ? ColumnWidth : Math.Max(120, (int)(bitmap.Width * (double)ThumbnailHeight / Math.Max(1, bitmap.Height)));
                var height = Vertical ? Math.Min(720, Math.Max(1, (int)(bitmap.Height * (double)ColumnWidth / Math.Max(1, bitmap.Width)))) : ThumbnailHeight;
                if (Vertical && Height > 120)
                {
                    var room = Height - Padding.Vertical - 16;
                    for (var pass = 0; pass < 2 && height > 60; pass++)
                    {
                        var creditH = TextRenderer.MeasureText(credit.Text, credit.Font, new Size(width, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height + 8;
                        if (height + creditH <= room) break;
                        height = Math.Max(60, room - creditH);
                        width = Math.Max(60, Math.Min(ColumnWidth, (int)(bitmap.Width * (double)height / Math.Max(1, bitmap.Height))));
                    }
                }
                box.Size = new Size(width, height);
                credit.Size = new Size(width, TextRenderer.MeasureText(credit.Text, credit.Font, new Size(width, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height + 14);
            }
        }
        finally { ResumeLayout(true); _arranging = false; }
    }

    public void Show(DayStore store, IEnumerable<ImageRef> images)
    {
        // Materialize the references once, before clearing the previous display.
        var refs = images.ToList();
        _arranging = true;
        SuspendLayout();
        try
        {
        Clear();
        _creditFont = new Font(Font.FontFamily, 7.5f);
        FlowDirection = Vertical ? FlowDirection.TopDown : FlowDirection.LeftToRight;
        foreach (var image in refs)
        {
            Image bitmap;
            try
            {
                bitmap = ImageLoader.LoadFromStore(store, image);   // read completely, so the file is not locked while the image is shown
            }
            catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or IOException)
            {
                continue;
            }
            _loaded.Add(bitmap);

            var box = new PhotoBox { Image = bitmap, Margin = new Padding(0, 0, 0, 4), InsetRight = 0 };
            var credit = new Label
            {
                Text = $"{image.Caption}\n{image.Creator} · {image.License}\n{ShortUrl(image.SourceUrl)}",
                AutoSize = false,
                ForeColor = ForeColor,
                Font = _creditFont
            };
            var cell = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Margin = Vertical ? Padding.Empty : new Padding(0, 0, 10, 0) };   // a column that is as wide as the strip must not get a horizontal scroll bar
            cell.Controls.Add(box);
            cell.Controls.Add(credit);
            Controls.Add(cell);
            _cells.Add((bitmap, box, credit));
        }
        }
        finally { ResumeLayout(true); _arranging = false; }
        ArrangeCells();
    }

    /// <summary>The address of a source without the long file path, so the line under a photograph does not run out of its column.</summary>
    private static string ShortUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host.Replace("www.", "") : url;

    public int Count => Controls.Count;

    public void Clear()
    {
        foreach (var (_, box, _) in _cells) box.Image = null;
        _cells.Clear();
        foreach (Control c in Controls.Cast<Control>().ToArray()) c.Dispose();
        Controls.Clear();
        foreach (var i in _loaded) i.Dispose();
        _loaded.Clear();
        _creditFont?.Dispose(); _creditFont = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Clear();
        base.Dispose(disposing);
    }
}
