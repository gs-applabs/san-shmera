#!/usr/bin/env python3
"""Build a byte-exact indexed media pack in a disposable published data folder."""
import argparse
import hashlib
import json
from pathlib import Path
import tempfile
import zipfile

EXTENSIONS = {".webp", ".png", ".jpg", ".jpeg"}

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("data", type=Path)
    parser.add_argument("--remove-loose", action="store_true", help="Only for a disposable publish output")
    args = parser.parse_args()
    root = args.data.resolve()
    images = root / "images"
    files = sorted(p for p in images.rglob("*") if p.is_file() and p.suffix.lower() in EXTENSIONS)
    if not files:
        raise SystemExit("No source images found")
    if args.remove_loose and root == (Path(__file__).resolve().parents[1] / "data"):
        raise SystemExit("Refusing to remove source repository images")
    texts = sorted(p for p in root.rglob("*") if p.is_file() and p.suffix.lower() in {".json", ".tsv"})
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
                data = path.read_bytes()
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
                z.write(path, path.relative_to(root).as_posix())
        with zipfile.ZipFile(text_pack) as z:
            if len(z.infolist()) != len(texts):
                raise RuntimeError("Missing or repeated text path")
            for path in texts:
                if hashlib.sha256(z.read(path.relative_to(root).as_posix())).digest() != hashlib.sha256(path.read_bytes()).digest():
                    raise RuntimeError(f"Text integrity check failed: {path}")
        pack.replace(root / "media.pack")
        idx.replace(root / "media.index")
        text_pack.replace(root / "texts.pack")
    if args.remove_loose:
        for path in files + texts:
            path.unlink()
        for directory in sorted((p for p in images.rglob("*") if p.is_dir()), key=lambda p: len(p.parts), reverse=True):
            if not any(directory.iterdir()):
                directory.rmdir()
    print(json.dumps({"originalImages": len(files), "uniqueImages": len(by_hash), "originalBytes": original_bytes,
        "packBytes": (root / "media.pack").stat().st_size, "indexBytes": (root / "media.index").stat().st_size,
        "textFiles": len(texts), "originalTextBytes": text_bytes, "packedTextBytes": (root / "texts.pack").stat().st_size,
        "verified": True, "removedLooseCopies": args.remove_loose}, indent=2))

if __name__ == "__main__":
    main()
