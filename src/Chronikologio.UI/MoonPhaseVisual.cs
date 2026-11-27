using System.Drawing.Drawing2D;
using Chronikologio.Core;

namespace Chronikologio.UI;

/// <summary>Original schematic, not a photograph or a simulation of local sky orientation.</summary>
internal sealed class MoonPhaseVisual : Control
{
    private double _phase, _fraction;
    public MoonPhaseVisual()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AccessibleRole = AccessibleRole.Graphic;
    }
    public void SetPhase(double angle, double fraction)
    {
        _phase = angle; _fraction = fraction;
        AccessibleName = Loc.Language == "en" ? "Schematic moon phase" : "Σχηματική φάση Σελήνης";
        AccessibleDescription = _fraction.ToString("P1", Loc.Culture);
        Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float r = Math.Max(1, Math.Min(38, (Height - 14) / 2f)), cx = 18 + r, cy = Height / 2f;
        var bounds = new RectangleF(cx-r,cy-r,2*r,2*r);
        using var shadow = new SolidBrush(Color.FromArgb(43,49,60));
        using var light = new SolidBrush(Color.FromArgb(238,236,211));
        using var edge = new Pen(ChronikologioTheme.Palette.TextMuted);
        g.FillEllipse(shadow,bounds);
        // North-up schematic: right illuminated during waxing, left during waning.
        var sign = _phase <= 180 ? 1f : -1f;
        var c = (float)Math.Cos(_phase * Math.PI / 180);
        using var path = new GraphicsPath();
        var points = new PointF[66];
        for (int i=0;i<=32;i++)
        {
            float y = -r + 2*r*i/32, x = (float)Math.Sqrt(Math.Max(0,r*r-y*y));
            points[i] = new(cx+sign*x,cy+y);
            points[65-i] = new(cx+sign*c*x,cy+y);
        }
        path.AddPolygon(points); g.FillPath(light,path); g.DrawEllipse(edge,bounds);
        var title = Loc.Language == "en" ? "Moon illumination" : "Φωτισμός Σελήνης";
        var note = Loc.Language == "en" ? "Schematic • northern orientation" : "Σχηματική απεικόνιση • βόρειος προσανατολισμός";
        var xText = (int)(cx+r+22);
        TextRenderer.DrawText(g,title+"  "+_fraction.ToString("P1",Loc.Culture),Font,new Rectangle(xText,12,Math.Max(1,Width-xText-8),30),ForeColor,TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(g,note,Font,new Rectangle(xText,42,Math.Max(1,Width-xText-8),30),ChronikologioTheme.Palette.TextMuted,TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
    }
}
