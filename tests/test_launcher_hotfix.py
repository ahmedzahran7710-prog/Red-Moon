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

    def test_legacy_hotfix_refuses_renamed_runtime(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / 'fix.zip'
            with self.assertRaisesRegex(RuntimeError, 'full Windows bundle'):
                hotfix.build(output)
            self.assertFalse(output.exists())

    def test_migration_uses_the_same_safe_entrypoint_conventions(self):
        text = (hotfix.ROOT / 'setup/Migrate-RedMoon.ps1').read_text(encoding='utf-8-sig')
        self.assertIn("[string]$Root = ''", text)
        self.assertNotIn('$PSScriptRoot', text)
        self.assertIn('$MyInvocation.MyCommand.Path', text)
        raw = (hotfix.ROOT / 'Migrate-RedMoon.cmd').read_bytes()
        self.assertIn(b'-Root "%~dp0."', raw)
        self.assertNotIn(b'\n', raw.replace(b'\r\n', b''))
