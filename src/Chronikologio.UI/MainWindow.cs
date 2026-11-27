using System.Globalization;
using Chronikologio.Core;
using Opsis.UI;

namespace Chronikologio.UI;

/// <summary>
/// The window of the stand-alone app: the day screen (<see cref="DayOverviewLayout"/>) with a name search, and the side menu of Opsis style with the animated logo on top.
/// There is no menu bar: the look, the country, the language and the updates are in the settings window (Ctrl+,), the guide, the history of versions and the licence in the help window (F1).
/// Ctrl+T changes the theme. The look comes from the shared Opsis.UI palettes through <see cref="ChronikologioTheme"/>.
/// </summary>
public sealed class MainWindow : Form
{
    private static CultureInfo El => Loc.Culture;

    private DayStore _store;
    private readonly string _dataRoot;
    private readonly UiSettings _settings;
    private readonly string? _settingsPath;
    private readonly SearchBar _searchBar = new();
    private readonly Panel _host = new() { Dock = DockStyle.Fill };
    private readonly SideRail _rail = new();
    private DayLayout _layout;
    private DateOnly _date = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Set by the stand-alone program: called with the store whenever the lines of the day may have changed (start, country, language), so the News Crawler can show them without Opsis. The tests never set it.</summary>
    public Action<DayStore>? PublishFeed { get; set; }

    public MainWindow(string dataRoot, string? settingsPath = null)
    {
        _dataRoot = dataRoot;
        _settingsPath = settingsPath;
        _settings = UiSettings.Load(settingsPath);
        WaveClock.Enabled = _settings.Animations;
        _store = DayStore.Load(dataRoot, _settings.Region);
        Loc.Set(Countries.Resolve(_settings.Language, _store.Region));

        Font = new Font("Segoe UI", 10f);
        // Wide enough for the side menu, the two columns of cards and the small calendar; the last size is remembered.
        MinimumSize = new Size(UiSettings.MinWidth, UiSettings.MinHeight);
        ClientSize = new Size(_settings.Width, _settings.Height);
        StartPosition = FormStartPosition.CenterScreen;
        if (_settings.Maximized) WindowState = FormWindowState.Maximized;
        KeyPreview = true;
        FormClosing += (_, _) => SaveWindowState();

        _layout = new DayOverviewLayout(_store) { Dock = DockStyle.Fill };
        _layout.SelectedDate = _date;
        _layout.DateChanged += SetDate;
        _layout.RegionRequested += ChooseRegion;
        _host.Controls.Add(_layout);
        Controls.Add(_host);
        Controls.Add(_rail);
        _searchBar.SetStore(_store);
        _searchBar.DateChosen += SetDate;
        _layout.AttachSearch(_searchBar);   // in the top row of the day screen, next to the Today button

        ApplyLanguage();
        Loc.Changed += ApplyLanguage;
        Disposed += (_, _) => Loc.Changed -= ApplyLanguage;

        ChronikologioTheme.Changed += ApplyTheme;
        Disposed += (_, _) => ChronikologioTheme.Changed -= ApplyTheme;

        ChronikologioTheme.Set(_settings.Theme);
        ApplyTheme();
    }

    /// <summary>Ctrl+T: the next theme of the list.</summary>
    public void CycleTheme() => ChooseTheme(ChronikologioTheme.Next());

    /// <summary>The calendar in use: "gr" (Church of Greece) or "cy" (Church of Cyprus).</summary>
    /// <summary>Publishes the current lines once (the stand-alone program calls it after setting <see cref="PublishFeed"/>).</summary>
    public void PublishFeedNow() => PublishFeed?.Invoke(_store);

    public string Region => _store.Region;

    private void SaveWindowState()
    {
        _settings.Maximized = WindowState == FormWindowState.Maximized;
        var size = WindowState == FormWindowState.Normal ? ClientSize : RestoreBounds.Size;
        if (WindowState == FormWindowState.Normal)
        {
            _settings.Width = Math.Clamp(size.Width, UiSettings.MinWidth, 4000);
            _settings.Height = Math.Clamp(size.Height, UiSettings.MinHeight, 3000);
        }
        _settings.Save(_settingsPath);
    }

    private void BuildRail()
    {
        _rail.BrandTitle = Loc.T("brand.title");
        _rail.BrandSub = Loc.T("brand.sub");
        _rail.BrandVersion = "v" + UpdateChecker.Norm(UpdateChecker.Current);
        _rail.SetItems(new[]
        {
            new SideRail.RailItem("today", "\uE787", Loc.T("menu.today") + " (Ctrl+Home)", GoToday, Loc.T("menu.today")),
            new SideRail.RailItem("theme", "\uE790", Loc.T("menu.theme") + " (Ctrl+T)", CycleTheme, Loc.T("menu.theme")),
            new SideRail.RailItem("settings", "", Loc.T("menu.settings") + " (Ctrl+,)", ShowSettings, Loc.T("menu.settings")),
            new SideRail.RailItem("help", "", Loc.T("menu.about") + " (F1)", ShowGuide, Loc.T("menu.about"))
        });
    }

    /// <summary>The day shown on the screen.</summary>
    public DateOnly SelectedDate => _date;

    /// <summary>Goes to today on the screen and the shared calendar, and makes the button of the side menu light up for a moment so the click is seen even when today is already shown.</summary>
    public void GoToday()
    {
        SetDate(DateOnly.FromDateTime(DateTime.Today));
        _rail.Pulse("today");
    }

    /// <summary>Shows a day on the screen and in the shared calendar.</summary>
    public void SetDate(DateOnly date)
    {
        _date = date;
        _layout.SelectedDate = date;
    }

    /// <summary>Switches the database and, with it, the interface language (the flag button of the screen does the same).</summary>
    public void ChooseRegion(string region)
    {
        if (!DayStore.Regions.Contains(region) || region == _store.Region) return;
        var store = DayStore.Load(_dataRoot, region);
        _store = store;
        _settings.Region = region;
        _settings.Save(_settingsPath);
        Loc.Set(Countries.Resolve(_settings.Language, region));
        _layout.SetStore(store);
        _searchBar.SetStore(store);
        PublishFeed?.Invoke(store);
    }

    /// <summary>Texts of the window in the language of the chosen country.</summary>
    private void ApplyLanguage()
    {
        Text = Loc.T("app.title");
        _searchBar.SetPlaceholder(Loc.T("search.placeholder"));
        _searchBar.SetStore(_store);
        BuildRail();
    }

    /// <summary>The language of the interface: "auto" (that of the country), "el" or "en". Kept apart from the country and remembered.</summary>
    public void ChooseLanguage(string setting)
    {
        if (setting is not ("auto" or "el" or "en")) return;
        _settings.Language = setting;
        _settings.Save(_settingsPath);
        Loc.Set(Countries.Resolve(setting, _store.Region));
        PublishFeed?.Invoke(_store);
    }

    /// <summary>The language setting in use: "auto", "el" or "en".</summary>
    public string LanguageSetting => _settings.Language;

    public bool Animations
    {
        get => _settings.Animations;
        set { _settings.Animations = value; WaveClock.Enabled = value; _settings.Save(_settingsPath); }
    }

    public void ChooseTheme(string name)
    {
        if (!ChronikologioTheme.Set(name)) return;
        _settings.Theme = name;
        _settings.Save(_settingsPath);
    }

    /// <summary>The window's keyboard shortcuts; true when the key was one of them.</summary>
    public bool HandleShortcut(Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Control | Keys.T: CycleTheme(); return true;
            case Keys.Control | Keys.Home: GoToday(); return true;
            case Keys.F1: ShowGuide(); return true;
            case Keys.Control | Keys.Oemcomma: ShowSettings(); return true;
            case Keys.Control | Keys.F: _searchBar.Focus(); return true;
            case Keys.Control | Keys.K: _searchBar.Focus(); return true;
            default: return false;
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData) => HandleShortcut(keyData) || base.ProcessCmdKey(ref msg, keyData);

    private UpdateChecker? _updates;
    private System.Windows.Forms.Timer? _updateTimer;
    private bool _offering;

    /// <summary>True in the stand-alone app, which looks for updates by itself; inside Opsis the plugin is updated by Opsis and the window shows no update controls.</summary>
    public bool UpdatesEnabled => _updates is not null;

    /// <summary>The automatic check for a new version (once a day, never blocking the window); the user can turn it off in the settings.</summary>
    public bool AutoUpdateCheck
    {
        get => _settings.CheckForUpdates;
        set { _settings.CheckForUpdates = value; _settings.Save(_settingsPath); }
    }

    /// <summary>Turns on the update of the stand-alone app (the plugin inside Opsis never calls this): a check 30 seconds after the start and then every 24 hours. Never blocks the window.</summary>
    public void EnableUpdates(UpdateChecker? checker = null)
    {
        if (_updates is not null) return;
        _updates = checker ?? new UpdateChecker();
        _updateTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
        _updateTimer.Tick += async (_, _) => { _updateTimer.Interval = (int)TimeSpan.FromHours(24).TotalMilliseconds; if (_settings.CheckForUpdates) await CheckForUpdatesAsync(false); };
        _updateTimer.Start();
        Disposed += (_, _) => _updateTimer?.Dispose();
    }

    /// <summary>Looks for a newer version and, when there is one, offers it (Install / Later / Skip this version). <paramref name="manual"/>: the user asked, so every answer is shown.</summary>
    public async Task CheckForUpdatesAsync(bool manual)
    {
        if (_updates is null || _offering) return;
        _offering = true;
        try
        {
            var result = await _updates.CheckAsync(UpdateChecker.Current, manual);
            if (IsDisposed || Disposing) return;
            if (result.Outcome == UpdateOutcome.Available && result.Update is { } info)
            {
                var choice = UpdateUi.Offer(this, info);
                if (choice == UpdateChoice.Skip) _updates.Skip(info.Tag);
                else if (choice == UpdateChoice.Install && await UpdateUi.DownloadAndStartAsync(this, info)) Close();   // the installer is running: the app closes so its files can be replaced
            }
            else if (manual) UpdateUi.ShowManualResult(this, result);
        }
        finally { _offering = false; }
    }

    private void ShowGuide() => HelpDialog.ShowFor(this, 0);

    /// <summary>Opens the settings window (Ctrl+, or the gear of the side menu).</summary>
    public void ShowSettings() => SettingsDialog.ShowFor(this);

    /// <summary>The list of the keyboard shortcuts.</summary>
    public void ShowShortcuts() => MessageBox.Show(this, Loc.T("help.text"), Loc.T("menu.shortcuts"), MessageBoxButtons.OK, MessageBoxIcon.Information);

    /// <summary>Puts the look back to its defaults: system theme and automatic language. The country is kept.</summary>
    public void ResetLook()
    {
        ChooseTheme("system");
        ChooseLanguage("auto");
        Animations = false;
    }

    /// <summary>Restyles the window and the search box with the current palette, and the title bar with its dark or light mode.</summary>
    private void ApplyTheme()
    {
        var p = ChronikologioTheme.Palette;
        BackColor = p.Background;
        ForeColor = p.Text;
        _searchBar.Restyle();
        _rail.Invalidate();
        if (IsHandleCreated) TitleBar.Apply(Handle, p.IsDark);
        Invalidate(true);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        TitleBar.Apply(Handle, ChronikologioTheme.IsDark);
    }
}
