using Chronikologio.Core;
using Opsis.UI;

namespace Chronikologio.UI;

/// <summary>
/// The settings window: the look (theme), the country and language and, in the stand-alone app, the updates, in cards in the colours of the theme. Every change is applied at once
/// through the window and saved; there is no OK button to forget. "Reset" returns the look to its defaults, "Shortcuts" lists the keys.
/// </summary>
public sealed class SettingsDialog : Form
{
    private readonly MainWindow _window;
    private readonly FlatComboBox _theme = NewCombo(), _region = NewCombo(), _language = NewCombo();
    private readonly FlatCheckBox _animations = new() { AutoSize = true };
    private readonly Label _animationsLabel = new();
    private readonly FlatCheckBox _autoUpdate = new() { AutoSize = true };
    private readonly Opsis.UI.FlatButton _checkNow = new() { Width = 220, Height = 32 };
    private readonly Label _note = new() { AutoSize = false, Height = 34, Dock = DockStyle.Bottom, Tag = "muted" };
    private readonly Opsis.UI.FlatButton _reset = new() { Width = 190, Height = 34 };
    private readonly Opsis.UI.FlatButton _shortcuts = new() { Width = 170, Height = 34 };
    private readonly Opsis.UI.FlatButton _close = new() { Width = 110, Height = 34 };
    private readonly List<(Panel Card, Label Head)> _cards = new();
    private readonly List<Label> _labels = new();
    private readonly Label _lookLabel = new(), _regionLabel = new(), _languageLabel = new();
    private bool _filling;

    private readonly Dictionary<FlatComboBox, List<Choice>> _choices = new();

    // the combo boxes of Opsis.UI: drawn in the colours of the theme, no white boxes
    private static FlatComboBox NewCombo() => new() { Width = 300, Height = 32 };

    public SettingsDialog(MainWindow window)
    {
        _window = window;
        Font = new Font("Segoe UI", 10f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Padding = new Padding(16);
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };

        var look = AddCard(Loc.T("settings.group.look"), (_lookLabel, _theme), (_animationsLabel, _animations));
        var calendar = AddCard(Loc.T("settings.group.calendar"), (_regionLabel, _region), (_languageLabel, _language));
        var updates = window.UpdatesEnabled ? AddCard(Loc.T("settings.group.updates"), (new Label(), _autoUpdate), (new Label(), _checkNow)) : null;
        ClientSize = new Size(560, 426 + (updates is null ? 0 : 140));

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 56, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 10, 0, 0) };
        buttons.Controls.Add(_close);
        buttons.Controls.Add(_reset);
        buttons.Controls.Add(_shortcuts);
        // dock order: the last added is laid out first, so the card that must be on top is added last
        var stack = new Panel { Dock = DockStyle.Fill };
        look.Dock = DockStyle.Top; calendar.Dock = DockStyle.Top;
        if (updates is not null) { updates.Dock = DockStyle.Top; stack.Controls.Add(updates); stack.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 12 }); }
        stack.Controls.Add(calendar);
        stack.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 12 });   // the gap between the cards
        stack.Controls.Add(look);
        Controls.Add(stack);
        Controls.Add(_note);
        Controls.Add(buttons);

        Fill();
        _theme.SelectedIndexChanged += (_, _) => { if (!_filling && Chosen(_theme) is { } c) _window.ChooseTheme(c.Key); };
        _region.SelectedIndexChanged += (_, _) => { if (!_filling && Chosen(_region) is { } c) { _window.ChooseRegion(c.Key); Fill(); } };
        _language.SelectedIndexChanged += (_, _) => { if (!_filling && Chosen(_language) is { } c) { _window.ChooseLanguage(c.Key); } };
        _animations.CheckedChanged += (_, _) => { if (!_filling) _window.Animations = _animations.Checked; };
        _autoUpdate.CheckedChanged += (_, _) => { if (!_filling) _window.AutoUpdateCheck = _autoUpdate.Checked; };
        _checkNow.Click += async (_, _) => await _window.CheckForUpdatesAsync(true);
        _reset.Click += (_, _) => { _window.ResetLook(); Fill(); };
        _shortcuts.Click += (_, _) => _window.ShowShortcuts();
        _close.Click += (_, _) => Close();
        ActiveControl = _close;   // no combo box starts with its text selected
        Loc.Changed += ApplyTexts;
        ChronikologioTheme.Changed += Restyle;
        FormClosed += (_, _) => { Loc.Changed -= ApplyTexts; ChronikologioTheme.Changed -= Restyle; };
        ApplyTexts();
        Restyle();
    }

    private sealed record Choice(string Key, string Text)
    {
        public override string ToString() => Text;
    }

    private Panel AddCard(string heading, params (Label Label, Control Control)[] rows)
    {
        var card = new Panel { Height = 28 + 12 + 10 + rows.Length * 46 + 4, Padding = new Padding(16, 12, 16, 10), Tag = "card" };   // the heading, the padding and the full height of every row (the combo boxes were cut at the bottom)
        var head = new Label { Text = heading, AutoSize = false, Dock = DockStyle.Top, Height = 28, Font = new Font("Segoe UI", 11f, FontStyle.Bold) };
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = rows.Length };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < rows.Length; i++)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            var label = rows[i].Label;
            label.AutoSize = false; label.Dock = DockStyle.Fill; label.TextAlign = ContentAlignment.MiddleLeft;
            _labels.Add(label);
            table.Controls.Add(label, 0, i);
            rows[i].Control.Anchor = AnchorStyles.Left;
            table.Controls.Add(rows[i].Control, 1, i);
        }
        card.Controls.Add(table);
        card.Controls.Add(head);
        _cards.Add((card, head));
        card.Margin = new Padding(0, 0, 0, 12);
        return card;
    }

    private Choice? Chosen(FlatComboBox box) =>
        _choices.TryGetValue(box, out var list) && box.SelectedIndex >= 0 && box.SelectedIndex < list.Count ? list[box.SelectedIndex] : null;

    private void Choices(FlatComboBox box, IEnumerable<Choice> choices, string current)
    {
        var list = choices.ToList();
        _choices[box] = list;
        var index = Math.Max(0, list.FindIndex(c => c.Key == current));
        box.SetItems(list.Select(c => c.Text), index);
    }

    private void Fill()
    {
        _filling = true;
        try
        {
            Choices(_theme, ChronikologioTheme.Names.Select(n => new Choice(n, ChronikologioTheme.DisplayName(n))), ChronikologioTheme.Name);
            Choices(_region, Countries.All.Select(c => new Choice(c.Region, Loc.T("region." + c.Region))), _window.Region);
            Choices(_language, new[] { "auto", "el", "en" }.Select(c => new Choice(c, Loc.T("language." + c))), _window.LanguageSetting);
            _animations.Checked = _window.Animations;
            _autoUpdate.Checked = _window.AutoUpdateCheck;
        }
        finally { _filling = false; }
    }

    private void ApplyTexts()
    {
        Text = Loc.T("settings.title");
        _cards[0].Head.Text = Loc.T("settings.group.look");
        _cards[1].Head.Text = Loc.T("settings.group.calendar");
        if (_cards.Count > 2) _cards[2].Head.Text = Loc.T("settings.group.updates");
        _lookLabel.Text = Loc.T("settings.theme");
        _animationsLabel.Text = Loc.Language == "en" ? "Animation" : "Κίνηση";
        _animations.Text = Loc.Language == "en" ? "Decorative animation (more CPU)" : "Κινούμενα γραφικά (περισσότερη CPU)";
        _regionLabel.Text = Loc.T("settings.region");
        _languageLabel.Text = Loc.T("settings.language");
        _autoUpdate.Text = Loc.T("upd.menu.auto");
        _checkNow.Text = Loc.T("upd.menu.check");
        _note.Text = Loc.T("settings.note");
        _reset.Text = Loc.T("settings.reset");
        _shortcuts.Text = Loc.T("menu.shortcuts");
        _close.Text = Loc.T("settings.close");
        Fill();
    }

    private void Restyle()
    {
        var p = ChronikologioTheme.Palette;
        BackColor = p.Background;
        ForeColor = p.Text;
        foreach (var (card, head) in _cards) { card.BackColor = p.Surface; card.ForeColor = p.Text; head.BackColor = p.Surface; head.ForeColor = p.Accent; }
        foreach (var l in _labels) { l.BackColor = p.Surface; l.ForeColor = p.Text; }
        _animations.BackColor = p.Surface; _animations.ForeColor = p.Text;
        _autoUpdate.BackColor = p.Surface; _autoUpdate.ForeColor = p.Text;
        _note.BackColor = p.Background; _note.ForeColor = p.TextMuted;
        foreach (var table in _cards.SelectMany(c => c.Card.Controls.OfType<TableLayoutPanel>())) table.BackColor = p.Surface;
        Invalidate(true);
    }

    /// <summary>Opens the window over its owner and returns when it is closed.</summary>
    public static void ShowFor(MainWindow owner)
    {
        using var dialog = new SettingsDialog(owner);
        AppIcon.Inherit(dialog, owner); dialog.ShowDialog(owner);
    }

    // test helpers
    public IReadOnlyList<string> Themes => _choices[_theme].Select(c => c.Key).ToList();
    public void Pick(string which, string key)
    {
        FlatComboBox box = which switch { "theme" => _theme, "region" => _region, _ => _language };
        box.SelectedIndex = _choices[box].FindIndex(c => c.Key == key);
    }
    public void Reset() => _reset.PerformClick();
}
