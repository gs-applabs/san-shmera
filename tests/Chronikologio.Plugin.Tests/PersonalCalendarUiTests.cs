using Chronikologio.Core;
using Chronikologio.UI;
using Opsis.UI;
using Xunit;

namespace Chronikologio.Plugin.Tests;

[Collection("theme")]
public sealed class PersonalCalendarUiTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(),"chron-private-ui-"+Guid.NewGuid().ToString("N"));
    public void Dispose()
    {
        Loc.Set("el");
        if (Directory.Exists(_root)) Directory.Delete(_root,true);
    }
    private static IEnumerable<Control> All(Control root)
    {
        foreach (Control c in root.Controls) { yield return c; foreach (var child in All(c)) yield return child; }
    }

    [Fact]
    public void PersonalPageIsLazyAndWritesOnlyOnExplicitSave()
    {
        Directory.CreateDirectory(_root);
        var settings = Path.Combine(_root,"settings.json");
        new UiSettings { Language="en" }.Save(settings);
        var personal = settings+".personal.json";
        using var window = new MainWindow(Path.Combine(AppContext.BaseDirectory,"data"),settings)
        { ShowInTaskbar=false,Opacity=0,StartPosition=FormStartPosition.Manual,Location=new Point(-32000,-32000) };
        window.Show(); Application.DoEvents();
        Assert.Empty(All(window).OfType<PersonalCalendarPage>());
        Assert.False(File.Exists(personal));
        Assert.True(window.HandleShortcut(Keys.Control|Keys.D3));
        var page = Assert.Single(All(window).OfType<PersonalCalendarPage>());
        Assert.False(File.Exists(personal));
        var title = All(page).OfType<TextBox>().Single(x=>x.AccessibleName=="Title");
        var date = All(page).OfType<TextBox>().Single(x=>x.AccessibleName=="Date DD/MM/YYYY");
        title.Text = "Private birthday";
        date.Text = "01/01/2099";
        var save = All(page).OfType<FlatButton>().Single(x=>x.Text=="Save");
        typeof(Control).GetMethod("OnClick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(save,new object[]{EventArgs.Empty});
        Assert.Equal("Private birthday",Assert.Single(new PersonalCalendar(personal).Load().Events).Title);
        window.GoToday();
        Assert.True(page.IsDisposed);
        Assert.Empty(All(window).OfType<PersonalCalendarPage>());
    }
}
