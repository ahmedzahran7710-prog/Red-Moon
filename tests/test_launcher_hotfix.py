import hashlib
import json
from pathlib import Path
import re
import sys
import tempfile
import unittest
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'tools'))
import package_launcher_hotfix as hotfix


class LauncherHotfixTests(unittest.TestCase):
    def test_defaults_are_deferred_to_script_body(self):
        for relative in hotfix.FILES:
            if not relative.endswith('.ps1'):
                continue
            text = (hotfix.ROOT / relative).read_text(encoding='utf-8-sig')
            self.assertIn("[string]$Root = ''", text, relative)
            self.assertNotIn('$PSScriptRoot', text, relative)
            self.assertIn('$MyInvocation.MyCommand.Path', text, relative)
            self.assertIn('[string]::IsNullOrWhiteSpace($Root)', text, relative)
            self.assertIn('#requires -Version 5.1', text, relative)
            if '[string]$ProfilePath' in text:
                self.assertIn("[string]$ProfilePath = ''", text)
                self.assertIn('[string]::IsNullOrWhiteSpace($ProfilePath)', text)

    def test_cmds_pass_non_trailing_slash_root_and_system_host(self):
        for name in hotfix.NAMES:
            raw = (hotfix.ROOT / (name + '.cmd')).read_bytes()
            text = raw.decode('ascii')
            self.assertIn('-Root "%~dp0."', text)
            self.assertIn('setlocal DisableDelayedExpansion', text)
            self.assertIn('%SystemRoot%\\System32\\WindowsPowerShell\\v1.0\\powershell.exe', text)
            self.assertIn('exit /b %REDMOON_EXIT%', text)
            self.assertNotIn(b'\n', raw.replace(b'\r\n', b''), 'CMD must retain CRLF')
            if name in ('Setup-RedMoon', 'Install-ClientConfig'):
                self.assertIn('-ProfilePath "%~dp0setup\\owner-profile.json"', text)

    def test_zip_has_only_allowed_replacements_and_exact_bytes(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / 'fix.zip'
            report = hotfix.build(output)
            self.assertLess(report['bytes'], 100_000)
            self.assertEqual(report['replaced_files'], 11)
            with zipfile.ZipFile(output) as archive:
                self.assertEqual(set(archive.namelist()), set(hotfix.FILES) | {'HOTFIX-LAUNCHERS.md', 'launcher-hotfix-manifest.json'})
                manifest = json.loads(archive.read('launcher-hotfix-manifest.json'))
                for name in hotfix.FILES:
                    raw = archive.read(name)
                    self.assertEqual(raw, (hotfix.ROOT / name).read_bytes())
                    self.assertEqual(hashlib.sha256(raw).hexdigest(), manifest['files'][name])
                for name in archive.namelist():
                    self.assertNotRegex(name, r'(^|/)(\.local|owner-profile\.json|Common\.ps1|server|database)(/|$)')

    def test_zip_is_reproducible_for_same_source_head(self):
        with tempfile.TemporaryDirectory() as directory:
            a = Path(directory) / 'first.zip'; b = Path(directory) / 'second.zip'
            hotfix.build(a); hotfix.build(b)
            self.assertEqual(a.read_bytes(), b.read_bytes())
