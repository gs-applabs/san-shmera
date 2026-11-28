using System.Security.Cryptography;
using System.Text.Json;

namespace Chronikologio.Core;

/// <summary>Read-only, byte-exact image archive. No extraction or persistent cache.</summary>
internal sealed class MediaArchive
{
    internal const int MaxImageBytes = 32 * 1024 * 1024;
    private const int MaxIndexBytes = 16 * 1024 * 1024;
    private sealed record Entry(long Offset, int Length, string Sha256);
    private readonly string _pack;
    private readonly Dictionary<string, Entry> _index;

    private MediaArchive(string pack, Dictionary<string, Entry> index)
    {
        _pack = pack;
        _index = index;
    }

    internal static MediaArchive? Open(string root)
    {
        var pack = Path.Combine(root, "media.pack");
        var index = Path.Combine(root, "media.index");
        if (!File.Exists(pack) || !File.Exists(index)) return null;
        using var stream = File.OpenRead(index);
        if (stream.Length > MaxIndexBytes) throw new IOException("Media index exceeds size limit.");
        var entries = JsonSerializer.Deserialize<Dictionary<string, Entry>>(stream)
            ?? throw new IOException("Missing media index.");
        return new MediaArchive(pack, entries);
    }

    internal static string? Key(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains(':')) return null;
        var normalized = path.Replace('\\', '/');
        if (normalized.Split('/').Any(p => p is "" or "." or "..")) return null;
        return normalized;
    }

    internal byte[]? Read(string key)
    {
        if (!_index.TryGetValue(key, out var entry)) return null;
        if (entry is null || entry.Offset < 0 || entry.Length <= 0 || entry.Length > MaxImageBytes ||
            entry.Sha256 is not { Length: 64 } || !entry.Sha256.All(Uri.IsHexDigit))
            throw new IOException("Invalid media index entry.");
        using var stream = File.OpenRead(_pack);
        if (entry.Offset > stream.Length || entry.Length > stream.Length - entry.Offset)
            throw new IOException("Media archive entry outside file.");
        stream.Position = entry.Offset;
        var bytes = new byte[entry.Length];
        stream.ReadExactly(bytes);
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Media archive checksum mismatch.");
        return bytes;
    }
}
