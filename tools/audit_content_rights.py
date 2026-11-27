#!/usr/bin/env python3
"""Read-only inventory of declared image rights and saint-source dependencies.
Not legal clearance: labels are unverified claims; inspect original pages/rights.
"""
import argparse
from collections import Counter, defaultdict
import json
from pathlib import Path, PurePosixPath
import re
from urllib.parse import urlsplit

FORMATS = {"eventimages": 8, "saintimages": 7, "personimages": 9}
IMAGE_SUFFIXES = {".webp", ".png", ".jpg", ".jpeg"}
PERMITTED_LABEL = re.compile(r"(?:public domain|cc0(?: 1\.0)?|cc by (?:1\.0|2\.0|2\.5|3\.0|4\.0))", re.I)

def audit(root):
    images = root / "images"
    declarations = defaultdict(list)
    licenses, hosts, issues = Counter(), Counter(), []
    saint_rows, saint_placeholders = 0, 0
    for p in sorted(images.rglob("*.tsv")):
        expected = next((v for k, v in FORMATS.items() if p.name.startswith(k)), None)
        if expected is None:
            continue
        for line_no, line in enumerate(p.read_text(encoding="utf-8-sig").splitlines(), 1):
            if not line.strip() or line.startswith("#"):
                continue
            fields = line.split("|")
            where = f"{p.relative_to(root).as_posix()}:{line_no}"
            if len(fields) != expected:
                issues.append({"where": where, "reason": "malformed-row"})
                continue
            file, caption, creator, license_, url = (x.strip() for x in fields[-5:])
            path = PurePosixPath(file.replace("\\", "/"))
            if path.is_absolute() or ".." in path.parts or ":" in file or not file:
                issues.append({"where": where, "reason": "unsafe-path"})
                continue
            key = path.as_posix()
            try:
                parsed = urlsplit(url)
                host = (parsed.hostname or "").lower()
                valid_url = parsed.scheme == "https" and bool(host) and parsed.username is None
            except ValueError:
                host, valid_url = "", False
            licenses[license_ or "(missing)"] += 1
            hosts[host or "(missing)"] += 1
            declarations[key].append((where, creator, license_, url))
            if not creator or not PERMITTED_LABEL.fullmatch(license_) or not valid_url:
                issues.append({"where": where, "file": key, "reason": "unrecognized-or-incomplete-rights-metadata"})
            if creator.lower().startswith(("unknown", "anonymous", "άγνωστ")):
                issues.append({"where": where, "file": key, "reason": "creator-unknown-needs-review"})
            if host not in {"commons.wikimedia.org", "images.nasa.gov", "www.nasa.gov", "nasa.gov", "history.nasa.gov"}:
                issues.append({"where": where, "file": key, "reason": "source-host-needs-review"})
    actual = {p.relative_to(images).as_posix() for p in images.rglob("*") if p.is_file() and p.suffix.lower() in IMAGE_SUFFIXES}
    for key in sorted(actual - declarations.keys()):
        issues.append({"file": key, "reason": "no-image-tsv-declaration-check-json-or-other-manifests"})
    for key in sorted(declarations.keys() - actual):
        issues.append({"file": key, "reason": "declared-file-not-found"})
    for key, refs in sorted(declarations.items()):
        if len({(creator, license_, url) for _, creator, license_, url in refs}) > 1:
            issues.append({"file": key, "reason": "conflicting-rights-declarations"})
    for p in sorted((root / "saints").glob("saints-syn-*.tsv")):
        for line in p.read_text(encoding="utf-8-sig").splitlines():
            if not line.strip() or line.startswith("#"):
                continue
            saint_rows += 1
            if "Ο βίος δεν έχει ακόμη γραφτεί" in line:
                saint_placeholders += 1
    return {
        "status": "inventory-only-not-legal-clearance",
        "imageFiles": len(actual),
        "uniqueDeclaredImagePaths": len(declarations),
        "imageDeclarations": sum(map(len, declarations.values())),
        "declaredLicenseCounts": dict(sorted(licenses.items())),
        "declaredSourceHosts": dict(sorted(hosts.items())),
        "saintsSynaxarionRowsRequiringDatasetRightsReview": saint_rows,
        "saintRowsWithExplicitUnwrittenLife": saint_placeholders,
        "issueCounts": dict(sorted(Counter(i["reason"] for i in issues).items())),
        "issues": issues,
        "limitations": [
            "No external rights pages were fetched or independently verified.",
            "A declared license is not proof of permission, jurisdictional public domain or image relevance.",
            "Images declared in JSON or other manifests require separate inventory.",
            "Source facts, selection/database rights, captions and original summaries need independent review."
        ]
    }

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--data", type=Path, default=Path("data"))
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    report = audit(args.data)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({k: v for k,v in report.items() if k != "issues"}, ensure_ascii=False, indent=2))
