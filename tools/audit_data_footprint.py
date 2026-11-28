#!/usr/bin/env python3
"""Measure a lossless packaging candidate without modifying the source library."""
import argparse
import collections
import hashlib
import json
from pathlib import Path
import tempfile
import time
import zipfile

def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--data", type=Path, default=Path("data"))
    parser.add_argument("--output", type=Path, default=Path("audit-metrics/data-footprint.json"))
    args = parser.parse_args()
    root = args.data.resolve()
    if not root.is_dir():
        raise SystemExit("Data folder missing")
    start = time.perf_counter()
    files = sorted(p for p in root.rglob("*") if p.is_file())
    extensions = {".json", ".tsv", ".jpg", ".jpeg", ".png", ".webp"}
    shipped = [p for p in files if p.suffix.lower() in extensions and
               (p.suffix.lower() in {".json", ".tsv"} or p.is_relative_to(root / "images"))]
    groups = collections.defaultdict(list)
    totals = collections.defaultdict(lambda: {"files": 0, "bytes": 0, "allocated4KiBEstimate": 0})
    manifest = {}
    for p in shipped:
        size = p.stat().st_size
        entry = totals[p.suffix.lower()]
        entry["files"] += 1
        entry["bytes"] += size
        entry["allocated4KiBEstimate"] += ((size + 4095) // 4096) * 4096
        sha = digest(p)
        groups[sha].append(p)
        manifest[p.relative_to(root).as_posix()] = sha
    images = [p for p in shipped if p.suffix.lower() not in {".json", ".tsv"}]
    texts = [p for p in shipped if p.suffix.lower() in {".json", ".tsv"}]
    image_set = set(images)
    image_groups = {sha: [p for p in paths if p in image_set] for sha, paths in groups.items()}
    duplicates = sum(len(paths)-1 for paths in image_groups.values() if paths)
    duplicate_bytes = sum((len(paths)-1)*paths[0].stat().st_size for paths in image_groups.values() if paths)
    with tempfile.TemporaryDirectory() as work:
        text_zip = Path(work) / "texts.zip"
        image_zip = Path(work) / "media.zip"
        # Text is losslessly compressed; media already encoded as WebP/JPEG/PNG
        # is stored without recompression and indexed by its exact byte hash.
        with zipfile.ZipFile(text_zip, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as z:
            for p in texts:
                z.write(p, p.relative_to(root).as_posix())
        with zipfile.ZipFile(image_zip, "w", compression=zipfile.ZIP_STORED) as z:
            for sha, paths in image_groups.items():
                if paths:
                    z.write(paths[0], sha)
            z.writestr("manifest.json", json.dumps({p.relative_to(root).as_posix(): manifest[p.relative_to(root).as_posix()] for p in images}, ensure_ascii=False))
        with zipfile.ZipFile(text_zip) as z:
            for p in texts:
                key = p.relative_to(root).as_posix()
                if hashlib.sha256(z.read(key)).hexdigest() != manifest[key]:
                    raise RuntimeError(f"Text changed: {key}")
        with zipfile.ZipFile(image_zip) as z:
            index = json.loads(z.read("manifest.json"))
            for key, sha in index.items():
                if manifest[key] != sha:
                    raise RuntimeError(f"Image mapping changed: {key}")
            if len(index) != len(images):
                raise RuntimeError("Missing images")
            for sha, paths in image_groups.items():
                if paths and hashlib.sha256(z.read(sha)).hexdigest() != sha:
                    raise RuntimeError(f"Image changed: {sha}")
        report = {
            "filesInDataTree": len(files), "shippedFiles": len(shipped),
            "byExtension": dict(totals), "sourceBytes": sum(p.stat().st_size for p in shipped),
            "standalonePlusPluginDataBytesBeforeCompression": 2 * sum(p.stat().st_size for p in shipped),
            "exactDuplicateImages": duplicates, "exactDuplicateImageBytes": duplicate_bytes,
            "textArchiveBytes": text_zip.stat().st_size, "imageArchiveBytes": image_zip.stat().st_size,
            "archiveFileCount": 2, "allShippedBytesVerifiedBySha256": True,
            "elapsedSeconds": round(time.perf_counter()-start, 3),
            "note": "Prototype only; ZIP sizes are not estimates of Inno Setup size. No source file changed."
        }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))

if __name__ == "__main__":
    main()
