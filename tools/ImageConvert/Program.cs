using System.Text.RegularExpressions;
using SkiaSharp;

// Converts the photographs and icons of data\images to WebP (smaller files, same look) and rewrites the references in the data files.
// Usage: dotnet run --project tools/ImageConvert -c Release -- <data folder> [--max 900] [--quality 80]
// Every .jpg/.jpeg/.png under <data>\images becomes .webp (no side longer than --max pixels); the original is deleted after the new file was written
// and read back. The names in eventimages*.tsv, saintimages*.tsv and the month JSON files are changed to the new names.

if (args.Length == 0 || !Directory.Exists(args[0])) { Console.Error.WriteLine("usage: ImageConvert <data folder> [--max 900] [--quality 80]"); return 2; }
var data = Path.GetFullPath(args[0]);
var max = 900; var quality = 80;
for (var i = 1; i < args.Length - 1; i++)
{
    if (args[i] == "--max") max = int.Parse(args[i + 1]);
    if (args[i] == "--quality") quality = int.Parse(args[i + 1]);
}
var images = Path.Combine(data, "images");
var renames = new Dictionary<string, string>(StringComparer.Ordinal);
long before = 0, after = 0;

foreach (var file in Directory.EnumerateFiles(images, "*.*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
{
    var ext = Path.GetExtension(file).ToLowerInvariant();
    if (ext is not (".jpg" or ".jpeg" or ".png")) continue;
    var target = Path.ChangeExtension(file, ".webp");
    try
    {
        before += new FileInfo(file).Length;
        using (var image = SKBitmap.Decode(file) ?? throw new InvalidDataException("cannot read the image"))
        {
            var bitmap = image;
            if (Math.Max(image.Width, image.Height) > max)
            {
                var scale = max / (double)Math.Max(image.Width, image.Height);
                bitmap = image.Resize(new SKImageInfo(Math.Max(1, (int)Math.Round(image.Width * scale)), Math.Max(1, (int)Math.Round(image.Height * scale))), new SKSamplingOptions(SKCubicResampler.Mitchell));
            }
            using var picture = SKImage.FromBitmap(bitmap);
            using var encoded = picture.Encode(SKEncodedImageFormat.Webp, quality) ?? throw new InvalidDataException("cannot write WebP");
            File.WriteAllBytes(target, encoded.ToArray());
            if (!ReferenceEquals(bitmap, image)) bitmap.Dispose();
        }
        using (var check = SKBitmap.Decode(target)) { if (check is null || check.Width < 1 || check.Height < 1) throw new InvalidDataException("empty image"); }
        after += new FileInfo(target).Length;
        File.Delete(file);
        renames[Path.GetRelativePath(images, file).Replace('\\', '/')] = Path.GetRelativePath(images, target).Replace('\\', '/');
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"kept {file}: {ex.Message}");
        if (File.Exists(target)) File.Delete(target);
    }
}

// references: exact relative paths in the data files (never the URLs of the sources, which are not in this form)
var changedFiles = 0;
foreach (var file in Directory.EnumerateFiles(data, "*.*", SearchOption.AllDirectories).Where(f => f.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
{
    var text = File.ReadAllText(file);
    var updated = text;
    foreach (var (oldName, newName) in renames)
        updated = Regex.Replace(updated, @"(?<![A-Za-z0-9_/.-])" + Regex.Escape(oldName) + @"(?![A-Za-z0-9_])", newName);
    if (updated != text) { File.WriteAllText(file, updated, new System.Text.UTF8Encoding(text.StartsWith('﻿'))); changedFiles++; }
}
Console.WriteLine($"{renames.Count} images converted, {before / 1048576.0:F1} MB -> {after / 1048576.0:F1} MB, references changed in {changedFiles} files");
return 0;
