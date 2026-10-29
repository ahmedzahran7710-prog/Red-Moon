"""Packaging and byte-inventory checks. Fixtures are inert, never executed."""
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'tools'))
import package_windows as bundle
import verify_windows_bundle as verifier


class WindowsBundleTests(unittest.TestCase):
    def populate(self, root):
        for relative in bundle.REQUIRED_RUNTIME:
            file = root / relative
            file.parent.mkdir(parents=True, exist_ok=True)
            file.write_bytes(b'inert file-format-independent unit fixture; not an executable')

    def test_required_redmoon_entrypoints_and_dependencies(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            with self.assertRaisesRegex(ValueError, 'Publish all'):
                bundle.validate_staging(root)
            self.populate(root)
            bundle.validate_staging(root)
            (root / 'server/RedMoon.Data.dll').unlink()
            with self.assertRaisesRegex(ValueError, 'RedMoon.Data.dll'):
                bundle.validate_staging(root)

    def test_configured_or_mixed_staging_is_refused(self):
        for forbidden in bundle.FORBIDDEN:
            with self.subTest(forbidden=forbidden), tempfile.TemporaryDirectory() as d:
                root = Path(d)
                self.populate(root)
                target = root / forbidden
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(b'local-only marker')
                with self.assertRaisesRegex(ValueError, 'Never package'):
                    bundle.validate_staging(root)

    def test_interrupted_migration_staging_is_refused(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d); self.populate(root)
            (root / '.local-migration-test').mkdir()
            with self.assertRaisesRegex(ValueError, 'Interrupted migration'):
                bundle.validate_staging(root)

    def test_distribution_has_one_source_tree_and_keeps_provenance(self):
        for name in ('src/RedMoon.GameServer/Program.cs', 'setup/Common.ps1', 'Migrate-RedMoon.cmd',
                     'licenses/FoxConquer5517-MIT.txt', 'audit/fox5517-manifest.json'):
            self.assertTrue(bundle.included_source(name), name)
        for name in ('vendor/FoxConquer5517/MsgServer/Program.cs', 'downloads/RedMoon-Source-Layout.zip',
                     'audit/evidence/old.json', 'tests/managed/FoxBaseline.Probes.csproj',
                     'tools/package_launcher_hotfix.py', 'tests/test_baseline.py', '.github/workflows/redmoon-repair.yml'):
            self.assertFalse(bundle.included_source(name), name)

    def archive(self, path, extra=None, tamper=False):
        files = {name: b'inert unit fixture' for name in set(bundle.REQUIRED_RUNTIME) | set(verifier.REQUIRED_SOURCE)}
        if extra: files[extra] = b'unsafe fixture'
        manifest = {'layout_version': 2, 'source_head': 'fixture-head', 'credentials_included': False,
                    'files': {name: hashlib.sha256(raw).hexdigest() for name, raw in files.items()}}
        if tamper: files['README.md'] = b'changed after hashing'
        with zipfile.ZipFile(path, 'w') as archive:
            for name, raw in files.items(): archive.writestr(name, raw)
            archive.writestr('bundle-manifest.json', json.dumps(manifest))

    def test_manifest_and_safe_extraction(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d); path = root / 'bundle.zip'; self.archive(path)
            report = verifier.verify(path, 'fixture-head', root / 'extracted')
            self.assertTrue(report['all_member_hashes_match'])
            self.assertTrue((root / 'extracted/src/RedMoon.sln').is_file())
            with self.assertRaisesRegex(ValueError, 'NEW directory'):
                verifier.verify(path, 'fixture-head', root / 'extracted')

    def test_hash_mismatch_is_rejected_before_extraction(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d); path = root / 'bundle.zip'; self.archive(path, tamper=True)
            with self.assertRaisesRegex(ValueError, 'SHA256 mismatch'):
                verifier.verify(path, 'fixture-head', root / 'extracted')
            self.assertFalse((root / 'extracted').exists())

    def test_unsafe_and_legacy_archive_members_refused(self):
        for extra in ('../outside', 'C:/outside', 'x\\outside', 'downloads/RedMoon-Source-Layout.zip', '.local/installed.json'):
            with self.subTest(extra=extra), tempfile.TemporaryDirectory() as d:
                path = Path(d) / 'bundle.zip'; self.archive(path, extra=extra)
                with self.assertRaises(ValueError): verifier.verify(path, 'fixture-head')

    def test_wrong_source_commit_refused(self):
        with tempfile.TemporaryDirectory() as d:
            path = Path(d) / 'bundle.zip'; self.archive(path)
            with self.assertRaisesRegex(ValueError, 'source commit'):
                verifier.verify(path, 'another-head')

    def test_failed_or_missing_runtime_verification_refused(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            with self.assertRaisesRegex(ValueError, 'Missing'):
                bundle.copy_verification(root / 'stage', root, 'head')
            (root / 'integration-results.json').write_text(json.dumps({'both_servers_ready': False}))
            with self.assertRaisesRegex(ValueError, 'failed'):
                bundle.copy_verification(root / 'stage', root, 'head')
