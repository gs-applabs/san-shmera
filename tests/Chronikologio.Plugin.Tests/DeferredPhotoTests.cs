using Chronikologio.Core;
using Chronikologio.UI;
using Xunit;

namespace Chronikologio.Plugin.Tests;

[Collection("theme")]
public sealed class DeferredPhotoTests
{
    [Fact]
    public void CardDefersDecodingAndReleasesImageWithoutChangingLayout()
    {
        using var tile = new EventTile("1900","Test","Description","Source",null,null,null);
        var reference = new ImageRef("example.webp","Caption","Author","CC0","https://example.com/image","portrait");
        int reads = 0;
        tile.DeferPhoto(() => { reads++; return (new Bitmap(100,200),reference,reference.File); }, reference);
        tile.FitTo(280);
        Assert.Equal(0,reads);
        Assert.True(tile.WantsPhoto);
        Assert.False(tile.HasPhoto);
        Assert.True(tile.LoadVisiblePhoto());
        Assert.False(tile.LoadVisiblePhoto());
        Assert.Equal(1,reads);
        var height = tile.Height;
        tile.ReleaseOffscreenPhoto();
        Assert.False(tile.HasPhoto);
        Assert.Equal(height,tile.Height);
        Assert.True(tile.LoadVisiblePhoto());
        Assert.Equal(2,reads);
        Assert.Equal(height,tile.Height);
    }

    [Fact]
    public void MissingPhotoIsNotRetriedAndDoesNotLeaveEmptySpace()
    {
        using var tile = new EventTile("1900","Test","Description","Source",null,null,null);
        var reference = new ImageRef("missing.webp","Caption","Author","CC0","https://example.com/image","photo");
        tile.FitTo(280);
        var height = tile.Height;
        int reads = 0;
        tile.DeferPhoto(() => { reads++; return (null,null,null); },reference);
        Assert.True(tile.Height > height);
        Assert.True(tile.LoadVisiblePhoto());
        Assert.False(tile.LoadVisiblePhoto());
        Assert.Equal(1,reads);
        Assert.False(tile.WantsPhoto);
        Assert.Equal(height,tile.Height);
    }
}
