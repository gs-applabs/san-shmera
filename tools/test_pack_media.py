#!/usr/bin/env python3
"""Lossless pack safety regression using disposable synthetic files."""
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import zipfile

SCRIPT = Path(__file__).with_name("pack_media.py")

class PackMediaTests(unittest.TestCase):
    def run_pack(self, root, *args):
        return subprocess.run([sys.executable,str(SCRIPT),str(root),*args],capture_output=True,text=True)

    def test_lossless_dedup_and_reproducible_zip(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)/"stage"
            images = root/"images"
            images.mkdir(parents=True)
            data = b"synthetic-image"
            (images/"a.webp").write_bytes(data)
            (images/"b.webp").write_bytes(data)
            text = b"# fixture\n"+b"alpha beta "*1000
            (root/"sample.tsv").write_bytes(text)
            first = self.run_pack(root)
            self.assertEqual(first.returncode,0,first.stderr)
            index=json.loads((root/"media.index").read_text(encoding="utf-8"))
            self.assertEqual(index["a.webp"],index["b.webp"])
            self.assertEqual((root/"media.pack").read_bytes(),data)
            first_zip = (root/"texts.pack").read_bytes()
            second = self.run_pack(root,"--remove-loose")
            self.assertEqual(second.returncode,0,second.stderr)
            self.assertEqual(first_zip,(root/"texts.pack").read_bytes())
            with zipfile.ZipFile(root/"texts.pack") as z:
                self.assertEqual(z.read("sample.tsv"),text)
                self.assertEqual(z.getinfo("sample.tsv").date_time,(1980,1,1,0,0,0))
            self.assertFalse((images/"a.webp").exists())
            self.assertFalse((root/"sample.tsv").exists())

    def test_linked_root_and_descendant_are_refused(self):
        with tempfile.TemporaryDirectory() as tmp:
            base=Path(tmp)
            root=base/"stage"
            images=root/"images"
            images.mkdir(parents=True)
            original=images/"a.webp"
            original.write_bytes(b"fixture")
            (root/"sample.tsv").write_text("fixture",encoding="utf-8")
            alias=base/"alias"
            try:
                alias.symlink_to(root,target_is_directory=True)
            except (OSError,NotImplementedError):
                self.skipTest("directory symlinks unavailable")
            result=self.run_pack(alias,"--remove-loose")
            self.assertNotEqual(result.returncode,0)
            self.assertIn("linked/reparse",result.stderr)
            self.assertTrue(original.exists())
            (images/"linked").symlink_to(base,target_is_directory=True)
            result=self.run_pack(root,"--remove-loose")
            self.assertNotEqual(result.returncode,0)
            self.assertIn("linked/reparse",result.stderr)
            self.assertTrue(original.exists())

if __name__=="__main__":
    unittest.main()
