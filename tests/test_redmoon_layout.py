"""Layout/deployment contracts for the maintained source, not gameplay claims."""
import hashlib
import json
from pathlib import Path
import re
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
PROJECTS = ('RedMoon.AccountServer', 'RedMoon.GameServer', 'RedMoon.Data', 'RedMoon.Core', 'RedMoon.Admin')


def relocated(path):
    for move in json.loads((ROOT / 'docs/reference/REDMOON-RENAMES.json').read_text())['moves']:
        if 'to' in move and (path == move['from'] or path.startswith(move['from'] + '/')):
            path = move['to'] + path[len(move['from']):]
    return path


class RedMoonLayoutTests(unittest.TestCase):
    def test_all_five_real_projects_and_references_exist(self):
        actual = sorted(p.stem for p in (ROOT / 'src').rglob('*.csproj'))
        self.assertEqual(actual, sorted(PROJECTS))
        for name in PROJECTS:
            project = ROOT / 'src' / name / (name + '.csproj')
            tree = ET.parse(project)
            self.assertEqual(tree.findtext('.//TargetFramework'), 'net10.0')
            for reference in tree.findall('.//ProjectReference'):
                self.assertTrue((project.parent / reference.attrib['Include'].replace('\\', '/')).is_file(), reference.attrib)
        props = ET.parse(ROOT / 'src/Directory.Build.props')
        self.assertEqual(props.findtext('.//AssemblyName'), '$(MSBuildProjectName)')
        self.assertEqual(props.findtext('.//RootNamespace'), '$(MSBuildProjectName)')

    def test_single_working_solution_has_real_project_names(self):
        self.assertEqual(list((ROOT / 'src').rglob('*.sln')), [ROOT / 'src/RedMoon.sln'])
        solution = (ROOT / 'src/RedMoon.sln').read_text()
        for name in PROJECTS:
            self.assertIn('"' + name + '", "' + name + '\\' + name + '.csproj"', solution)
        self.assertNotIn('FoxConquer5517', solution)
        self.assertFalse((ROOT / 'src/FoxConquer5517').exists())

    def test_no_original_csharp_source_is_lost_during_moves(self):
        manifest = json.loads((ROOT / 'audit/fox5517-manifest.json').read_text())
        original = [f for f in manifest['files'] if f['path'].endswith('.cs')]
        self.assertEqual(len(original), 615)
        mapped = [relocated('src/FoxConquer5517/' + f['path']) for f in original]
        self.assertEqual(len(set(mapped)), len(mapped))
        for relative in mapped:
            self.assertTrue((ROOT / relative).is_file(), relative)

    def test_upstream_license_bytes_preserved(self):
        manifest = json.loads((ROOT / 'audit/fox5517-manifest.json').read_text())
        license_record = next(f for f in manifest['files'] if f['path'] == 'LICENSE')
        self.assertEqual(hashlib.sha256((ROOT / 'licenses/FoxConquer5517-MIT.txt').read_bytes()).hexdigest(), license_record['sha256'])

    def test_namespace_roots_are_owned_or_explicit_compatibility_dependencies(self):
        for file in (ROOT / 'src').rglob('*.cs'):
            for namespace in re.findall(r'(?m)^namespace ([\w.]+)', file.read_text(encoding='utf-8-sig')):
                self.assertTrue(namespace == 'RedMoon' or namespace.startswith('RedMoon.') or
                                namespace == 'IniParser' or namespace.startswith('IniParser.') or
                                namespace == 'CO2_CORE_DLL' or namespace.startswith('CO2_CORE_DLL.'), (file, namespace))
        self.assertTrue((ROOT / 'src/RedMoon.Core/ThirdParty/IniParser').is_dir())
        self.assertTrue((ROOT / 'src/RedMoon.Core/Compatibility/CO2Core').is_dir())
        self.assertTrue((ROOT / 'src/RedMoon.GameServer/Gameplay/Entities/Character.cs').is_file())
        self.assertTrue((ROOT / 'src/RedMoon.GameServer/Runtime/Workers').is_dir())

    def test_publish_and_start_use_new_names_together(self):
        for path in ('setup/Build-RedMoon.ps1', 'tools/ci/publish-runtime.ps1'):
            text = (ROOT / path).read_text()
            for name in ('RedMoon.AccountServer', 'RedMoon.GameServer', 'RedMoon.Admin'):
                self.assertIn(name + '/' + name + '.csproj', text)
            self.assertNotIn('FoxConquer5517/', text)
        start = (ROOT / 'setup/Start-RedMoon.ps1').read_text()
        for name in ('RedMoon.AccountServer', 'RedMoon.GameServer'):
            self.assertIn("Start-OwnedServer '" + name + "'", start)
            self.assertIn(name + '.stdout.log', start)
        self.assertIn('Migrate-RedMoon.cmd', start)
        common = (ROOT / 'setup/Common.ps1').read_text()
        self.assertIn("'RedMoon.AccountServer.ini'", common)
        self.assertIn("'RedMoon.GameServer.ini'", common)
        # Legacy names are only recognized under explicit READ-ONLY migration compatibility.
        self.assertIn('[switch]$AllowLegacy', common)

    def test_migration_never_invokes_sql_or_stops_processes(self):
        text = (ROOT / 'setup/Migrate-RedMoon.ps1').read_text()
        self.assertNotRegex(text, r'\b(?:Invoke-Mysql|Invoke-PrivateProcess|Start-Process|Stop-Process|Save-LocalSecrets)\b|\.Kill\(')
        self.assertIn('Load-LocalSecrets $PreviousRoot $false', text)
        self.assertIn('Read-OwnedProcessRecords $record -AllowLegacy', text)
        self.assertIn('non-overlapping folders', text)
        self.assertIn("Join-Path $Root '.local'", text)
        self.assertIn('ReparsePoint', text)
        self.assertIn('[IO.Directory]::Move', text)
        self.assertIn("'server/RedMoon.AccountServer.ini'", text)
        self.assertIn("'server/RedMoon.GameServer.ini'", text)

    def test_workflows_stay_manual_only(self):
        workflows = list((ROOT / '.github/workflows').glob('*.yml'))
        if not workflows:
            self.skipTest('The runnable source bundle omits CI metadata; policy is checked in the Git checkout.')
        for file in workflows:
            text = file.read_text()
            trigger = text.split('\non:\n', 1)[1].split('\npermissions:', 1)[0]
            self.assertIn('workflow_dispatch:', trigger)
            self.assertNotRegex(trigger, r'(?m)^  (push|pull_request|pull_request_target|schedule|workflow_run):')
