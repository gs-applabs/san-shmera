#if OPSIS_UI6
using Opsis.UI;

namespace Chronikologio.UI;

/// <summary>
/// Optional Opsis.UI 6 visual widgets for book covers, daily history and manuscript timelines.
/// Not compiled until the host and Chronikologio both deploy the same tested UI6 DLL.
/// </summary>
internal static class OpsisUi6Cards
{
    public static OpsisMediaTile Cover(Image? artwork, string title, string subtitle, string status = "")
    {
        return new OpsisMediaTile
        {
            Artwork = artwork, Text = title, Subtitle = subtitle, Status = status,
            Size = new Size(178, 222), AccessibleName = title
        };
    }

    public static OpsisTimelineCard Event(string when, string title, string summary, bool highlighted = false)
    {
        return new OpsisTimelineCard
        {
            TimeLabel = when, Heading = title, DescriptionText = summary,
            Highlighted = highlighted, Size = new Size(360, 94)
        };
    }
}
#endif
