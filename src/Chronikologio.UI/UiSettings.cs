using System.Text.Json;
using Chronikologio.Core;

namespace Chronikologio.UI;

/// <summary>What the user chose about the look of the app: the theme and whether the menu bar is shown (Ctrl+M). Stored in %AppData%\Chronikologio\settings.json.</summary>
public sealed class UiSettings
{
    public string Theme { get; set; } = "system";

    /// <summary>Opt-in decorative animations; static graphics keep the same colours and artwork.</summary>
    public bool Animations { get; set; }

    /// <summary>The stand-alone app looks for a newer version in the official releases (at most once a day). The Opsis plugin never does: Opsis updates its plugins.</summary>
    public bool CheckForUpdates { get; set; } = true;

    /// <summary>"gr" = the calendar of the Church of Greece (default), "cy" = the calendar of the Church of Cyprus.</summary>
    public string Region { get; set; } = "gr";

    /// <summary>The language of the interface: "auto" (the language of the country, the default), "el" or "en". Independent of the country when chosen.</summary>
    public string Language { get; set; } = "auto";

    /// <summary>Size of the window when it was last closed (client area), and whether it was maximised.</summary>
    public int Width { get; set; } = 1120;
    public int Height { get; set; } = 740;
    public bool Maximized { get; set; }

    /// <summary>The smallest window that still fits the three layouts (rail, two cards and the small calendar).</summary>
    public const int MinWidth = 1080;
    public const int MinHeight = 580;


    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Chronikologio", "settings.json");

    /// <summary>The saved settings, or the defaults when the file is missing or damaged.</summary>
    public static UiSettings Load(string? path = null)
    {
        try
        {
            path ??= DefaultPath;
            if (!File.Exists(path) || new FileInfo(path).Length > 64 * 1024) return new UiSettings();
            var s = JsonSerializer.Deserialize<UiSettings>(File.ReadAllText(path)) ?? new UiSettings();
            if (!ChronikologioTheme.Names.Contains(s.Theme)) s.Theme = "system";
            if (!DayStore.Regions.Contains(s.Region)) s.Region = "gr";
            if (s.Language is not ("auto" or "el" or "en")) s.Language = "auto";
            s.Width = Math.Clamp(s.Width, MinWidth, 4000);
            s.Height = Math.Clamp(s.Height, MinHeight, 3000);
            return s;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new UiSettings();
        }
    }

    public void Save(string? path = null)
    {
        string? temp = null;
        try
        {
            path = Path.GetFullPath(path ?? DefaultPath);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            if (File.Exists(path) && new FileInfo(path).Length <= 64 * 1024 && File.ReadAllText(path) == json) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream)) writer.Write(json);
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // The settings are a convenience: failing to save must never stop the app.
        }
        finally
        {
            if (temp is not null)
                try { File.Delete(temp); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}
