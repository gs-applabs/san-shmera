using System.IO.Compression;

namespace Chronikologio.Core;

/// <summary>One read-only source for the original JSON/TSV files, on disk or in a lossless ZIP.</summary>
internal sealed class TextLibrary : IDisposable
{
    private const long MaxEntryBytes = 32 * 1024 * 1024;
    private const long MaxLibraryBytes = 128 * 1024 * 1024;
    private readonly ZipArchive? _archive;
    private readonly Dictionary<string, ZipArchiveEntry> _entries = new(StringComparer.Ordinal);
    public IReadOnlyList<string> Files { get; }

    public TextLibrary(string root)
    {
        var path = Path.Combine(root, "texts.pack");
        if (!File.Exists(path))
        {
            Files = Directory.Exists(root)
                ? Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                    .Where(IsText).Order(StringComparer.Ordinal).ToArray()
                : Array.Empty<string>();
            return;
        }

        _archive = ZipFile.OpenRead(path);
        try
        {
            long total = 0;
            foreach (var entry in _archive.Entries)
            {
                if (!IsText(entry.FullName) || MediaArchive.Key(entry.FullName) is not { } key ||
                    entry.Length < 0 || entry.Length > MaxEntryBytes ||
                    (total += entry.Length) > MaxLibraryBytes || _entries.Count >= 10000)
                    throw new InvalidDataException("Invalid or oversized text library entry.");
                var virtualPath = Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar));
                if (!_entries.TryAdd(virtualPath, entry)) throw new InvalidDataException("Duplicate text entry.");
            }
            Files = _entries.Keys.Order(StringComparer.Ordinal).ToArray();
        }
        catch { _archive.Dispose(); throw; }
    }

    private static bool IsText(string path) =>
        Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(path).Equals(".tsv", StringComparison.OrdinalIgnoreCase);

    private Stream Open(string file) => _archive is null ? File.OpenRead(file) : _entries[file].Open();

    public string ReadText(string file)
    {
        using var reader = new StreamReader(Open(file), detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    public IEnumerable<string> ReadLines(string file)
    {
        using var reader = new StreamReader(Open(file), detectEncodingFromByteOrderMarks: true);
        while (reader.ReadLine() is { } line) yield return line;
    }

    public void Dispose() => _archive?.Dispose();
}
