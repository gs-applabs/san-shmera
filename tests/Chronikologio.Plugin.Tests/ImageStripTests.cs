using Chronikologio.Core;
using Chronikologio.UI;
using Xunit;

namespace Chronikologio.Plugin.Tests;

[Collection("theme")]
public sealed class ImageStripTests
{
    [Fact]
    public void VerticalResizeReusesDecodedImageAndCreditControls()
    {
        var root = Path.Combine(Path.GetTempPath(), "chron-strip-" + Guid.NewGuid().ToString("N"));
        var imagePath = Path.Combine(root, "images", "test.png");
        Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
        try
        {
            using (var original = new Bitmap(320, 240))
                original.Save(imagePath, System.Drawing.Imaging.ImageFormat.Png);
            var store = DayStore.Load(root);
            using var strip = new ImageStrip { Vertical = true, ColumnWidth = 250, Size = new Size(280, 400) };
            strip.Show(store, new[] { new ImageRef("test.png", "Caption", "Creator", "CC0", "https://example.com/image") });
            var cell = Assert.Single(strip.Controls.Cast<Control>());
            var photo = Assert.Single(cell.Controls.OfType<PhotoBox>());
            var credit = Assert.Single(cell.Controls.OfType<Label>());
            var decoded = Assert.IsAssignableFrom<Image>(photo.Image);
            File.Delete(imagePath); // a subsequent resize must not reopen the source
            for (var i = 0; i < 20; i++)
            {
                strip.Height = i % 2 == 0 ? 190 : 400;
                Assert.Same(cell, Assert.Single(strip.Controls.Cast<Control>()));
                Assert.Same(decoded, photo.Image);
                Assert.Contains("Creator", credit.Text);
                Assert.True(credit.Width > 0 && photo.Height > 0);
            }
            strip.Clear();
            Assert.Empty(strip.Controls.Cast<Control>());
            strip.Height = 300;
            Assert.Empty(strip.Controls.Cast<Control>());
        }
        finally { Directory.Delete(root, true); }
    }
}
