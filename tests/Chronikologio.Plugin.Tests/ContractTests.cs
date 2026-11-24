using System.Security.Cryptography;
using Chronikologio.OpsisPlugin;
using OpsisNewsCrawler.Plugin.Abstractions;
using Xunit;

namespace Chronikologio.Plugin.Tests;

/// <summary>Plugin contract 1.0.0.0 of Opsis 2.6.0: the shared contract DLL is the canonical one, and the plugin offers the update source and takes the host settings.</summary>
public class ContractTests
{
    private const string CanonicalSha256 = "514f2a3eafa57770afd0f4b76cfe26397cfecd84edce06f071716fa0d9f30afe";

    private static string RepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Chronikologio.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(new[] { dir!.FullName }.Concat(parts).ToArray());
    }

    [Fact]
    public void TheContractDllInLibsIsTheCanonicalOne()
    {
        var path = RepoFile("libs", "OpsisNewsCrawler.Plugin.Abstractions.dll");
        Assert.Equal(CanonicalSha256, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant());
        Assert.Equal(new Version(1, 0, 0, 0), typeof(IOpsisNewsCrawlerPlugin).Assembly.GetName().Version);
    }

    [Fact]
    public void ThePluginImplementsTheNewInterfacesAndPointsToItsReleases()
    {
        var plugin = new ChronikologioPlugin();
        Assert.IsAssignableFrom<IPluginUpdateSource>(plugin);
        Assert.IsAssignableFrom<IPluginHostSettingsAware>(plugin);
        var source = ((IPluginUpdateSource)plugin).GetUpdateSource();
        Assert.StartsWith("gs-applabs/", source.Repository);
        Assert.EndsWith(".zip", source.AssetName);
        Assert.Equal("Chronikologio-Plugin.zip", source.AssetName);
    }


    [Fact]
    public void TheHostSettingsAreStored()
    {
        var plugin = new ChronikologioPlugin();
        Assert.False(plugin.HostManagesUpdates);
        plugin.SetHostSettings(new PluginHostSettings("el", true, "high", 100, true));
        Assert.True(plugin.HostManagesUpdates);
    }
}
