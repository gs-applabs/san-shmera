namespace Chronikologio.UI;

/// <summary>
/// Gives read-only multiline text boxes a vertical scrollbar only when text is longer
/// than the visible area. ScrollBars recreates the native HWND, so do not change it on
/// detached/invisible controls while country/language/layout data is being rebuilt.
/// </summary>
internal static class TextFit
{
    public static void ScrollWhenLonger(TextBox box)
    {
        var changingScrollBars = false;

        void Update()
        {
            if (box.IsDisposed || box.Disposing || !box.IsHandleCreated || !box.Visible ||
                changingScrollBars || box.Width < 40 || box.Height < 20)
                return;

            // Measure at the width *without* a vertical scrollbar so it does not
            // oscillate when the scrollbar appears or disappears.
            var full = box.ClientSize.Width +
                (box.ScrollBars == ScrollBars.Vertical ? SystemInformation.VerticalScrollBarWidth : 0) - 6;
            var height = TextRenderer.MeasureText(box.Text, box.Font,
                new Size(Math.Max(20, full), int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height + 4;
            var wanted = height > box.ClientSize.Height ? ScrollBars.Vertical : ScrollBars.None;
            if (box.ScrollBars == wanted) return;

            changingScrollBars = true;
            try { box.ScrollBars = wanted; }
            finally { changingScrollBars = false; }
        }

        // Hidden TabsLayout/Events may receive hundreds of text updates without
        // requiring any native window at all. On first display, HandleCreated/
        // VisibleChanged applies the actual measured scrollbar once.
        box.HandleCreated += (_, _) => Update();
        box.VisibleChanged += (_, _) => Update();
        box.TextChanged += (_, _) => Update();
        box.SizeChanged += (_, _) => Update();
    }
}
