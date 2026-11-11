using Chronikologio.UI;
using Xunit;

namespace Chronikologio.Plugin.Tests;

[Collection("theme")]
public class RailLogoTests
{
    /// <summary>Draws the brand block of the side menu at several moments (only when CHRON_RENDER_DIR is set) to look at the page of the calendar icon turning over.</summary>
    [Fact]
    public void RenderTheTurningCalendarIcon()
    {
        var dir = Environment.GetEnvironmentVariable("CHRON_RENDER_DIR");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        ChronikologioTheme.Set("dark");
        using var rail = new SideRail { Height = 140 };
        rail.BrandTitle = "Χρονικολόγιο"; rail.BrandSub = "Σαν σήμερα"; rail.BrandVersion = "v1.0.0";
        using var sheet = new Bitmap(SideRail.RailWidth * 8, 140);
        using var g = Graphics.FromImage(sheet);
        for (var i = 0; i < 8; i++)
        {
            using var bmp = new Bitmap(SideRail.RailWidth, 140);
            rail.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
            g.DrawImage(bmp, i * SideRail.RailWidth, 0);
            Thread.Sleep(620);
        }
        sheet.Save(Path.Combine(dir, "rail-logo-frames.png"), System.Drawing.Imaging.ImageFormat.Png);
    }
}
