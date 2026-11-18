namespace Chronikologio.UI;

/// <summary>The dialogs show the icon of the window they belong to in their title bar (the application, or the host when the calendar is a plugin).</summary>
public static class AppIcon
{
    public static void Inherit(Form dialog, IWin32Window? owner)
    {
        if (owner is Form { Icon: { } icon }) { dialog.Icon = icon; dialog.ShowIcon = true; }
    }
}
