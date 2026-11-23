using System.Diagnostics;
using Chronikologio.OpsisPlugin;
using OpsisNewsCrawler.Plugin.Abstractions;
using Xunit;

namespace Chronikologio.Plugin.Tests;

public sealed class PluginDllVersionTests
{
    [Fact]
    public void PluginAssemblyAndFileVersionAre1_0_0_0()
    {
        var dll = typeof(ChronikologioPlugin).Assembly;
        Assert.Equal(new Version(1, 0, 0, 0), dll.GetName().Version);
        Assert.Equal("1.0.0.0", FileVersionInfo.GetVersionInfo(dll.Location).FileVersion);
        Assert.Equal("0.9.0", new ChronikologioPlugin().Version);
    }

    [Fact]
    public void ContractRemainsTheHostsCompatibleAbi()
    {
        Assert.Equal(new Version(0, 8, 0, 0), typeof(IOpsisNewsCrawlerPlugin).Assembly.GetName().Version);
    }
}
