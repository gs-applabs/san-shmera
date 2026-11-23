using System.Reflection;
using Chronikologio.Core;
using Chronikologio.UI;
using Xunit;

namespace Chronikologio.Plugin.Tests;

public sealed class CalendarLifecycleTests
{
    [Theory]
    [InlineData("day")]
    [InlineData("tabs")]
    [InlineData("cards")]
    public void ClosingWindowDisposesItsSharedCalendarEvenWhenDetached(string layout)
    {
        var dataRoot = Path.Combine(AppContext.BaseDirectory, "data");
        var settings = Path.Combine(Path.GetTempPath(), $"chronikologio-lifecycle-{Guid.NewGuid():N}.json");
        var window = new MainWindow(dataRoot, settings);
        var calendar = (CalendarPanel)typeof(MainWindow)
            .GetField("_panel", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(window)!;
        try
        {
            window.ChooseLayout(layout);
            window.Dispose();
            Assert.True(calendar.IsDisposed, $"Shared calendar survived closing layout {layout}");
        }
        finally
        {
            window.Dispose();
            if (File.Exists(settings)) File.Delete(settings);
        }
    }

    [Fact]
    public void CalendarColorsReuseTheirBoldFontWithoutNewGdiAllocations()
    {
        using var calendar = new CalendarPanel(DayStore.Load(Path.Combine(AppContext.BaseDirectory, "data")));
        var title = (Label)typeof(CalendarPanel)
            .GetField("_title", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(calendar)!;
        var font = title.Font;
        for (int i = 0; i < 60; i++)
        {
            calendar.ApplyColors(
                System.Drawing.Color.FromArgb(20, 24, 32),
                System.Drawing.Color.White,
                System.Drawing.Color.Silver,
                System.Drawing.Color.DeepSkyBlue,
                System.Drawing.Color.FromArgb(32, 37, 45));
            Assert.Same(font, title.Font);
        }
    }
}
