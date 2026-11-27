using System.Security.Cryptography;
using System.Text.Json;
using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public class PackedMediaTests
{
    [Fact]
    public void PackedImagesPreserveBytesAndRejectTraversalAndCorruption()
    {
        var root = Path.Combine(Path.GetTempPath(), "chron-media-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var bytes = new byte[] { 1, 2, 3, 4, 5 };
            var sha = Convert.ToHexString(SHA256.HashData(bytes));
            var index = new Dictionary<string, object>
            {
                ["saints/a.webp"] = new { Offset = 0, Length = bytes.Length, Sha256 = sha },
                ["saints/b.webp"] = new { Offset = 0, Length = bytes.Length, Sha256 = sha },
                ["bad.webp"] = new { Offset = 99, Length = 3, Sha256 = sha }
            };
            File.WriteAllBytes(Path.Combine(root, "media.pack"), bytes);
            File.WriteAllText(Path.Combine(root, "media.index"), JsonSerializer.Serialize(index));
            var store = DayStore.Load(root);
            ImageRef Ref(string file) => new(file, "caption", "creator", "Public Domain", "https://example.com");
            Assert.Equal(bytes, store.ReadImageBytes(Ref("saints/a.webp")));
            Assert.Equal(bytes, store.ReadImageBytes(Ref("saints/b.webp")));
            Assert.Throws<IOException>(() => store.ReadImageBytes(Ref("../a.webp")));
            Assert.Throws<IOException>(() => store.ReadImageBytes(Ref("bad.webp")));
            File.WriteAllBytes(Path.Combine(root, "media.pack"), new byte[] { 5, 4, 3, 2, 1 });
            Assert.Throws<IOException>(() => store.ReadImageBytes(Ref("saints/a.webp")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void AllPackedImagesMatchTheSourceLibraryWhenAvailable()
    {
        var root = Environment.GetEnvironmentVariable("CHRON_PACKED_DATA");
        if (string.IsNullOrEmpty(root)) return;
        var source = Environment.GetEnvironmentVariable("CHRON_SOURCE_DATA")
            ?? throw new InvalidOperationException("Missing original data root");
        var store = DayStore.Load(root);
        var original = DayStore.Load(source);
        foreach (var region in DayStore.Regions)
        {
            var packedRegion = DayStore.Load(root, region);
            var originalRegion = DayStore.Load(source, region);
            Assert.Equal(originalRegion.Errors.Count, packedRegion.Errors.Count);
            for (var date = new DateOnly(2024, 1, 1); date.Year == 2024; date = date.AddDays(1))
                foreach (var language in new[] { "el", "en" })
                    Assert.Equal(JsonSerializer.Serialize(originalRegion.GetDayIn(date.Month, date.Day, language)),
                        JsonSerializer.Serialize(packedRegion.GetDayIn(date.Month, date.Day, language)));
        }
        foreach (var file in Directory.EnumerateFiles(Path.Combine(source, "images"), "*", SearchOption.AllDirectories)
            .Where(p => new[] { ".webp", ".png", ".jpg", ".jpeg" }.Contains(Path.GetExtension(p).ToLowerInvariant())))
        {
            var name = Path.GetRelativePath(Path.Combine(source, "images"), file);
            var image = new ImageRef(name, "caption", "creator", "Public Domain", "https://example.com");
            Assert.Equal(original.ReadImageBytes(image), store.ReadImageBytes(image));
        }
    }
}
