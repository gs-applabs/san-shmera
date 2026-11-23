using Chronikologio.Core;
using Chronikologio.UI;
using Xunit;

namespace Chronikologio.Plugin.Tests;

public sealed class GdiResourceRegressionTests
{
    [Fact]
    public void RestylingMiniCalendarDoesNotReallocateTitleFont()
    {
        using var calendar = new MiniCalendar(DayStore.Load(Path.Combine(AppContext.BaseDirectory, "data")));
        var title = calendar.Controls.Cast<Control>()
            .SelectMany(parent => parent.Controls.Cast<Control>())
            .OfType<Label>()
            .Single(c => c.Font.Bold);
        var font = title.Font;
        for (var i = 0; i < 30; i++) calendar.Restyle();
        Assert.Same(font, title.Font);
    }
}
