using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public sealed class HistoryLinkTests
{
    [Theory]
    [InlineData("event:03-25:1821:1234abcd")]
    [InlineData("birth:02-29:-100:01234567")]
    [InlineData("death:12-31:2020:abcdef01")]
    [InlineData("event:01-01:x:12345678")]
    public void ExistingIdRoundTripsAsPortableLink(string id)
    {
        Assert.True(HistoryLink.IsValidId(id));
        Assert.True(HistoryLink.TryParse(id,out var raw));
        Assert.Equal(id,raw);
        Assert.True(HistoryLink.TryParse(HistoryLink.Create(id),out var linked));
        Assert.Equal(id,linked);
    }

    [Theory]
    [InlineData("event:02-30:1821:1234abcd")]
    [InlineData("event:01-01:0:1234abcd")]
    [InlineData("event:01-01:+1821:1234abcd")]
    [InlineData("event:01-01:1821:1234ABCZ")]
    [InlineData("chronikologio://entry/../test")]
    [InlineData("chronikologio://entry/event%3A03-25%3A1821%3A1234abcd?run=1")]
    [InlineData("chronikologio://entry/event%253A03-25%253A1821%253A1234abcd")]
    [InlineData("file:///C:/test")]
    public void InvalidLinkCannotResolve(string text)
        => Assert.False(HistoryLink.TryParse(text,out _));

    [Theory]
    [InlineData("https://example.org/source",true)]
    [InlineData("https://example.org:443/source",true)]
    [InlineData("https://user@example.org/source",false)]
    [InlineData("https://example.org:8443/source",false)]
    [InlineData("http://example.org/source",false)]
    [InlineData("file:///C:/test",false)]
    [InlineData("javascript:alert(1)",false)]
    public void SourceNavigationAcceptsOnlyHttpsWithoutCredentialsOrCustomPort(string text,bool expected)
        => Assert.Equal(expected,HistoryLink.SourceUri(text) is not null);
}
