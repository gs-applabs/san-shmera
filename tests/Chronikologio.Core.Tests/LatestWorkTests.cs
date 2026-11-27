using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public sealed class LatestWorkTests
{
    [Fact]
    public void OneThousandRequestsKeepOnlyNewestWaitingValueAndDrainOnStop()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var values = new System.Collections.Concurrent.ConcurrentQueue<string>();
        using var work = new LatestWork<string>(value =>
        {
            values.Enqueue(value);
            if (value == "first") { entered.Set(); release.Wait(TimeSpan.FromSeconds(10)); }
        });
        work.Submit("first");
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            for (var i = 0; i < 1000; i++) work.Submit(i.ToString());
        }
        finally { release.Set(); }
        work.Dispose();
        work.Submit("ignored");
        Assert.Equal(new[] { "first", "999" }, values.ToArray());
    }
}
