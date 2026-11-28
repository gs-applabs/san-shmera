#!/usr/bin/env python3
"""Build a byte-exact indexed media pack in a disposable published data folder."""
import argparse
import hashlib
import json
import os
import stat
from pathlib import Path
import tempfile
import zipfile

EXTENSIONS = {".webp", ".png", ".jpg", ".jpeg"}

def check_unlinked(path):
    info = path.lstat()
    if stat.S_ISLNK(info.st_mode) or getattr(info, "st_file_attributes", 0) & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400):
        raise SystemExit(f"Refusing linked/reparse entry: {path}")
    return info

def inventory(root):
    # Check directory entries before os.walk descends into them.
    files, directories = [], []
    def fail(error):
        raise error
    for current, dirs, names in os.walk(root, topdown=True, followlinks=False, onerror=fail):
        directory = Path(current)
        check_unlinked(directory)
        for name in sorted(dirs):
            child = directory / name
            if not stat.S_ISDIR(check_unlinked(child).st_mode):
                raise SystemExit(f"Non-directory: {child}")
            directories.append(child)
        for name in sorted(names):
            child = directory / name
            if not stat.S_ISREG(check_unlinked(child).st_mode):
                raise SystemExit(f"Non-regular file: {child}")
            files.append(child)
    return files, directories

def read_bounded(path, limit):
    check_unlinked(path)
    with path.open("rb") as stream:
        data = stream.read(limit + 1)
    if len(data) > limit:
        raise RuntimeError(f"File exceeds supported size: {path}")
    return data

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("data", type=Path)
    parser.add_argument("--cleanup-manifest", type=Path, help="List exact original paths and SHA-256 for safe upgrades")
    parser.add_argument("--remove-loose", action="store_true", help="Only for a disposable publish output")
    args = parser.parse_args()
    root = Path(os.path.abspath(args.data))
    if not root.is_dir():
        raise SystemExit("Data folder missing")
    for component in [*reversed(root.parents), root]:
        check_unlinked(component)
    root = root.resolve()
    images = root / "images"
    source = (Path(__file__).resolve().parents[1] / "data").resolve()
    if args.remove_loose and (root == source or root.is_relative_to(source) or source.is_relative_to(root)):
        raise SystemExit("Refusing to remove files in or around the source repository data")
    all_files, directories = inventory(root)
    files = sorted(p for p in all_files if p.is_relative_to(images) and p.suffix.lower() in EXTENSIONS)
    if not files:
        raise SystemExit("No source images found")
    texts = sorted(p for p in all_files if p.suffix.lower() in {".json", ".tsv"})
    if not texts:
        raise SystemExit("No source texts found")
    if len(texts) > 10000 or sum(p.stat().st_size for p in texts) > 128 * 1024 * 1024 or any(p.stat().st_size > 32 * 1024 * 1024 for p in texts):
        raise SystemExit("Text library exceeds supported bounds")
    text_bytes = sum(p.stat().st_size for p in texts)
    index, by_hash = {}, {}
    original_bytes = 0
    with tempfile.TemporaryDirectory(dir=root) as work:
        pack = Path(work) / "media.pack"
        idx = Path(work) / "media.index"
        with pack.open("wb") as stream:
            for path in files:
                data = read_bounded(path, 32 * 1024 * 1024)
                if not 0 < len(data) <= 32 * 1024 * 1024:
                    raise RuntimeError(f"Image exceeds supported size: {path}")
                original_bytes += len(data)
                sha = hashlib.sha256(data).hexdigest()
                if sha not in by_hash:
                    by_hash[sha] = {"Offset": stream.tell(), "Length": len(data), "Sha256": sha}
                    stream.write(data)
                index[path.relative_to(images).as_posix()] = by_hash[sha]
        idx.write_text(json.dumps(index, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
        if idx.stat().st_size > 16 * 1024 * 1024:
            raise RuntimeError("Media index exceeds supported size")
        with pack.open("rb") as stream:
            for entry in by_hash.values():
                stream.seek(entry["Offset"])
                if hashlib.sha256(stream.read(entry["Length"])).hexdigest() != entry["Sha256"]:
                    raise RuntimeError("Media integrity check failed")
        if len(index) != len(files):
            raise RuntimeError("Missing or repeated media path")
        text_pack = Path(work) / "texts.pack"
        with zipfile.ZipFile(text_pack, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as z:
            for path in texts:
                info = zipfile.ZipInfo(path.relative_to(root).as_posix(), date_time=(1980, 1, 1, 0, 0, 0))
                info.compress_type = zipfile.ZIP_DEFLATED
                info.external_attr = 0o100644 << 16
                info.create_system = 3
                # Explicit level is needed with a caller-provided ZipInfo.
                z.writestr(info, read_bounded(path, 32 * 1024 * 1024), compress_type=zipfile.ZIP_DEFLATED, compresslevel=9)
        with zipfile.ZipFile(text_pack) as z:
            if len(z.infolist()) != len(texts):
                raise RuntimeError("Missing or repeated text path")
            for path in texts:
                if hashlib.sha256(z.read(path.relative_to(root).as_posix())).digest() != hashlib.sha256(read_bounded(path, 32 * 1024 * 1024)).digest():
                    raise RuntimeError(f"Text integrity check failed: {path}")
        pack.replace(root / "media.pack")
        idx.replace(root / "media.index")
        text_pack.replace(root / "texts.pack")
    if args.cleanup_manifest:
        manifest = args.cleanup_manifest
        manifest.parent.mkdir(parents=True, exist_ok=True)
        lines = []
        for path in files + texts:
            relative = path.relative_to(root).as_posix()
            if "|" in relative or "\r" in relative or "\n" in relative:
                raise RuntimeError(f"Invalid manifest path: {relative}")
            with path.open("rb") as stream:
                lines.append(relative + "|" + hashlib.file_digest(stream, "sha256").hexdigest())
        manifest.write_text("\n".join(lines) + "\n", encoding="utf-8-sig")
    if args.remove_loose:
        for path in files + texts:
            path.unlink()
        for directory in sorted((p for p in directories if p != images and p.is_relative_to(images)), key=lambda p: len(p.parts), reverse=True):
            if not any(directory.iterdir()):
                directory.rmdir()
    print(json.dumps({"originalImages": len(files), "uniqueImages": len(by_hash), "originalBytes": original_bytes,
        "packBytes": (root / "media.pack").stat().st_size, "indexBytes": (root / "media.index").stat().st_size,
        "textFiles": len(texts), "originalTextBytes": text_bytes, "packedTextBytes": (root / "texts.pack").stat().st_size,
        "verified": True, "removedLooseCopies": args.remove_loose}, indent=2))

if __name__ == "__main__":
    main()
