"""Tests for our import tooling, not gameplay tests."""
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
import fox_baseline as baseline


class BaselineTests(unittest.TestCase):
    def test_tracked_files_are_original(self):
        self.assertEqual(baseline.verify(baseline.ROOT / "vendor/FoxConquer5517")["verified_files"], 636)

    def test_all_csharp_and_projects_are_included(self):
        files = baseline.manifest()["files"]
        csharp = [f for f in files if f["path"].endswith(".cs")]
        projects = [f for f in files if f["path"].endswith(".csproj")]
        self.assertEqual(len(csharp), 615)
        self.assertEqual(len(projects), 4)
        self.assertTrue(all(f["tracked"] for f in csharp + projects))

    def test_omissions_are_explicit(self):
        omitted = [f for f in baseline.manifest()["files"] if not f["tracked"]]
        self.assertEqual(len(omitted), 174)
        self.assertTrue(all(f.get("excluded_reason") for f in omitted))
        self.assertFalse((baseline.ROOT / "vendor/FoxConquer5517/Database/accountzf.sql").exists())
        self.assertFalse((baseline.ROOT / "vendor/FoxConquer5517/Database/zf.sql").exists())

    def test_unsafe_names_rejected(self):
        for name in ("../file", "/file", "a/../../file", "C:/file", "a\\file", "./file", ""):
            with self.subTest(name=name), self.assertRaises(ValueError):
                baseline.safe_relative(name)

    def test_hashing(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "bytes"
            path.write_bytes(b"original\r\n\xef\xbb\xbf")
            self.assertEqual(baseline.digest(path), hashlib.sha256(path.read_bytes()).hexdigest())

    def test_materialize_cannot_write_to_vendor(self):
        with self.assertRaises(ValueError):
            baseline.materialize(baseline.ROOT / "vendor/FoxConquer5517", Path("missing.zip"))


if __name__ == "__main__":
    unittest.main()
