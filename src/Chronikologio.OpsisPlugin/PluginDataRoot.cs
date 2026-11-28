using Microsoft.Win32;

namespace Chronikologio.OpsisPlugin;

/// <summary>Installer-managed plugin shares the installed app library. An independent
/// plugin update with its own data keeps that data when the app version differs.</summary>
internal static class PluginDataRoot
{
    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{B7C4E2A1-5D3F-4E8A-9C61-2F0A8D7E4B93}_is1";
    private const string Format = "chronikologio-pack-v1";

    private static bool HasPack(string root) =>
        File.Exists(Path.Combine(root, "texts.pack")) &&
        File.Exists(Path.Combine(root, "media.pack")) &&
        File.Exists(Path.Combine(root, "media.index"));

    private static bool Compatible(string appDir)
    {
        var path = Path.Combine(appDir, "library-format.txt");
        return File.Exists(path) && new FileInfo(path).Length <= 128 &&
            File.ReadAllText(path).Trim() == Format;
    }

    internal static string Select(string local, string appDir, string installedVersion, string pluginVersion)
    {
        var shared = Path.Combine(appDir, "data");
        if (!HasPack(shared) || !Compatible(appDir)) return local;
        if (installedVersion == pluginVersion) return shared;
        // A plugin distributed independently has its own complete library.
        if (HasPack(local)) return local;
        // App-only upgrades may keep the existing plugin when the data format is unchanged.
        return shared;
    }

    internal static string Resolve(string pluginDir, string version)
    {
        var local = Path.Combine(pluginDir, "data");
        if (!File.Exists(Path.Combine(pluginDir, "shared-library.txt"))) return local;
        try
        {
            using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var installed = machine.OpenSubKey(UninstallKey);
            if (installed?.GetValue("DisplayVersion") is string installedVersion &&
                installed.GetValue("InstallLocation") is string appDir && Path.IsPathFullyQualified(appDir))
                return Select(local, appDir, installedVersion, version);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException) { }
        return local;
    }
}
