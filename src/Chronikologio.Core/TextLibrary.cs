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

    private long _bytesRead;
    private Stream Open(string file)
    {
        if (_archive is null)
        {
            var stream = File.OpenRead(file);
            if (stream.Length > MaxEntryBytes) { stream.Dispose(); throw new InvalidDataException("Oversized loose text file."); }
            return new BoundedRead(stream, MaxEntryBytes, Account);
        }
        var entry = _entries[file];
        return new BoundedRead(entry.Open(), entry.Length, Account);
    }

    private void Account(int count)
    {
        // Limit actual decompressed bytes, even if a corrupt archive reports a smaller length.
        _bytesRead += count;
        // A text file may be read more than once for different indexes, so allow
        // repeated reads while keeping total work finite.
        if (_bytesRead > 4 * MaxLibraryBytes) throw new InvalidDataException("Text library read budget exceeded.");
    }

    private sealed class BoundedRead(Stream source, long limit, Action<int> account) : Stream
    {
        private long _read;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _read; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset,count));
        public override int Read(Span<byte> buffer)
        {
            if (buffer.IsEmpty) return 0;
            var count = source.Read(buffer[..(int)Math.Min(buffer.Length, limit - _read + 1)]);
            if (count > limit - _read) throw new InvalidDataException("Actual text entry exceeds declared size or limit.");
            _read += count;
            account(count);
            return count;
        }
        protected override void Dispose(bool disposing) { if (disposing) source.Dispose(); base.Dispose(disposing); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

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
