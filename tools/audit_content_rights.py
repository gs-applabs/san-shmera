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
    def add(where, values):
        file, caption, creator, license_, url = (x.strip() if isinstance(x, str) else "" for x in values)
        path = PurePosixPath(file.replace("\\", "/"))
        if path.is_absolute() or ".." in path.parts or ":" in file or not file:
            issues.append({"where": where, "reason": "unsafe-path"})
            return
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
        evidence = {"where": where, "file": key, "creator": creator, "declaredLicense": license_, "sourceUrl": url}
        if not creator or not PERMITTED_LABEL.fullmatch(license_) or not valid_url:
            issues.append({**evidence, "reason": "unrecognized-or-incomplete-rights-metadata"})
        if creator.lower().startswith(("unknown", "anonymous", "άγνωστ")):
            issues.append({**evidence, "reason": "creator-unknown-needs-review"})
        if host not in {"commons.wikimedia.org", "images.nasa.gov", "www.nasa.gov", "nasa.gov", "history.nasa.gov"}:
            issues.append({**evidence, "reason": "source-host-needs-review"})

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
            add(where, fields[-5:])
    tsv_paths_before_json = set(declarations)
    # Existing day JSON files contain the original hand-curated image references.
    def walk(value, location):
        if isinstance(value, dict):
            if "file" in value and any(k in value for k in ("creator", "license", "sourceUrl")):
                add(location, [value.get(k) for k in ("file", "caption", "creator", "license", "sourceUrl")])
            for key, child in value.items():
                walk(child, f"{location}/{key}")
        elif isinstance(value, list):
            for i, child in enumerate(value):
                walk(child, f"{location}/{i}")
    for p in sorted(root.rglob("*.json")):
        try:
            walk(json.loads(p.read_text(encoding="utf-8-sig")), p.relative_to(root).as_posix())
        except (ValueError, UnicodeError) as ex:
            issues.append({"where": p.relative_to(root).as_posix(), "reason": "invalid-json", "detail": str(ex)})
    actual = {p.relative_to(images).as_posix() for p in images.rglob("*") if p.is_file() and p.suffix.lower() in IMAGE_SUFFIXES}
    for key in sorted(actual - declarations.keys()):
        issues.append({"file": key, "reason": "no-image-declaration-check-other-manifests"})
    for key in sorted(declarations.keys() - actual):
        issues.append({"file": key, "reason": "declared-file-not-found"})
    for key, refs in sorted(declarations.items()):
        if len({(creator, license_, url) for _, creator, license_, url in refs}) > 1:
            issues.append({"file": key, "reason": "conflicting-rights-declarations", "declarations": [{"where": w, "creator": c, "declaredLicense": l, "sourceUrl": u} for w,c,l,u in refs]})
    for p in sorted((root / "saints").glob("saints-syn-*.tsv")):
        for line in p.read_text(encoding="utf-8-sig").splitlines():
            if not line.strip() or line.startswith("#"):
                continue
            saint_rows += 1
            if "Ο βίος δεν έχει ακόμη γραφτεί" in line:
                saint_placeholders += 1
    # A source may be referenced by multiple local images. Review the source once,
    # but preserve every file/record and never infer approval from a matching URL.
    source_queue = {}
    for issue in issues:
        if issue.get("reason") not in {"creator-unknown-needs-review", "unrecognized-or-incomplete-rights-metadata", "source-host-needs-review"}:
            continue
        url = issue.get("sourceUrl") or ""
        key = url or "missing-source:" + issue.get("file", issue["where"])
        item = source_queue.setdefault(key, {"sourceUrl": url, "files": set(), "reasons": set(), "declaredLicenses": set(), "status": "pending", "priority": "standard"})
        if issue.get("file"):
            item["files"].add(issue["file"])
        item["reasons"].add(issue["reason"])
        label = issue.get("declaredLicense", "")
        item["declaredLicenses"].add(label)
        if issue["reason"] != "creator-unknown-needs-review" or label.casefold().startswith("cc by"):
            item["priority"] = "high"
    review_queue = []
    for key, item in sorted(source_queue.items()):
        review_queue.append({**item, "files": sorted(item["files"]), "reasons": sorted(item["reasons"]), "declaredLicenses": sorted(item["declaredLicenses"])})
    review_queue.sort(key=lambda i: (i["priority"] != "high", i["sourceUrl"], i["files"]))
    return {
        "status": "inventory-only-not-legal-clearance",
        "sourceReviewQueueCount": len(review_queue),
        "sourceReviewHighPriorityCount": sum(i["priority"] == "high" for i in review_queue),
        "sourceReviewQueue": review_queue,
        "imageFiles": len(actual),
        "uniqueDeclaredImagePaths": len(declarations),
        "newlyMatchedJsonImagePaths": len(set(declarations) - tsv_paths_before_json),
        "imageDeclarations": sum(map(len, declarations.values())),
        "declaredLicenseCounts": dict(sorted(licenses.items())),
        "declaredSourceHosts": dict(sorted(hosts.items())),
        "saintsSynaxarionRowsRequiringDatasetRightsReview": saint_rows,
        "datasetRightsReview": [{
            "source": "https://www.synaxarion.gr/",
            "scope": "saints-syn-*.tsv",
            "status": "permission-not-established",
            "reviewed": "2026-11-27",
            "note": "The publisher describes its own permission for reproducing church publications; no general redistribution grant for this application was established. Check database rights separately from individual date/name facts."
        }],
        "saintRowsWithExplicitUnwrittenLife": saint_placeholders,
        "issueCounts": dict(sorted(Counter(i["reason"] for i in issues).items())),
        "issues": issues,
        "limitations": [
            "No external rights pages were fetched or independently verified.",
            "A declared license is not proof of permission, jurisdictional public domain or image relevance.",
            "TSV and JSON declarations are inventoried; other manifests may require separate review.",
            "Source facts, selection/database rights, captions and original summaries need independent review.",
            "Unknown author alone does not establish infringement; verify original work date, jurisdiction and rights basis."
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
    print(json.dumps({k: v for k,v in report.items() if k not in {"issues", "sourceReviewQueue"}}, ensure_ascii=False, indent=2))
