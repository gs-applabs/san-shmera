#!/usr/bin/env python3
"""Synthetic regression tests: no network or modification of the real library."""
import json
from pathlib import Path
import tempfile
import unittest
from audit_content_rights import audit

class RightsAuditTests(unittest.TestCase):
    def test_tsv_json_and_unverified_rights_are_distinct(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            images = root / "images"
            images.mkdir()
            for name in ("a.webp", "b.webp", "orphan.webp"):
                (images / name).write_bytes(b"fixture")
            (images / "saintimages-test.tsv").write_text(
                "01-01|Saint|a.webp|caption|Unknown author|Public Domain|https://commons.wikimedia.org/wiki/File:A.jpg\n",
                encoding="utf-8")
            (root / "data.json").write_text(json.dumps({"days":[{"images":[
                {"file":"b.webp","caption":"test","creator":"Author","license":"CC BY 4.0","sourceUrl":"https://commons.wikimedia.org/wiki/File:B.jpg"},
                {"file":"a.webp","caption":"test","creator":"Other author","license":"CC0 1.0","sourceUrl":"https://commons.wikimedia.org/wiki/File:A.jpg"}
            ]}]}),encoding="utf-8")
            saints = root / "saints"
            saints.mkdir()
            (saints / "saints-syn-01.tsv").write_text(
                "01-01|Saint|Ο βίος δεν έχει ακόμη γραφτεί||1\n",encoding="utf-8")
            before = {p: p.read_bytes() for p in root.rglob("*") if p.is_file()}
            report = audit(root)
            self.assertEqual(report["imageFiles"],3)
            self.assertEqual(report["uniqueDeclaredImagePaths"],2)
            self.assertEqual(report["newlyMatchedJsonImagePaths"],1)
            self.assertEqual(report["issueCounts"]["creator-unknown-needs-review"],1)
            self.assertEqual(report["issueCounts"]["conflicting-rights-declarations"],1)
            self.assertEqual(report["issueCounts"]["no-image-declaration-check-other-manifests"],1)
            self.assertEqual(report["saintsSynaxarionRowsRequiringDatasetRightsReview"],1)
            unknown = next(x for x in report["issues"] if x["reason"] == "creator-unknown-needs-review")
            self.assertEqual(unknown["sourceUrl"],"https://commons.wikimedia.org/wiki/File:A.jpg")
            self.assertEqual(report["datasetRightsReview"][0]["status"],"permission-not-established")
            self.assertEqual(report["sourceReviewQueueCount"],1)
            self.assertEqual(report["sourceReviewQueue"][0]["status"],"pending")
            self.assertEqual(report["sourceReviewQueue"][0]["files"],["a.webp"])
            self.assertEqual(before,{p: p.read_bytes() for p in root.rglob("*") if p.is_file()})

if __name__ == "__main__":
    unittest.main()
