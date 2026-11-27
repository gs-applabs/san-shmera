using Microsoft.Win32;

namespace Chronikologio.OpsisPlugin;

/// <summary>Installer-managed plugin shares the installed app library only when the versions match.
/// A separately updated plugin can always fall back to its own bundled data.</summary>
internal static class PluginDataRoot
{
    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{B7C4E2A1-5D3F-4E8A-9C61-2F0A8D7E4B93}_is1";

    internal static string Resolve(string pluginDir, string version)
    {
        var local = Path.Combine(pluginDir, "data");
        if (!File.Exists(Path.Combine(pluginDir, "shared-library.txt"))) return local;
        try
        {
            using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var installed = machine.OpenSubKey(UninstallKey);
            if (installed?.GetValue("DisplayVersion") is string installedVersion &&
                installedVersion == version && installed.GetValue("InstallLocation") is string appDir &&
                Path.IsPathFullyQualified(appDir))
            {
                var shared = Path.Combine(appDir, "data");
                if (File.Exists(Path.Combine(shared, "texts.pack")) &&
                    File.Exists(Path.Combine(shared, "media.pack")) &&
                    File.Exists(Path.Combine(shared, "media.index")))
                    return shared;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException) { }
        return local;
    }
}
