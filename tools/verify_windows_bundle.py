#!/usr/bin/env python3
"""Verify and optionally extract a new bundle's exact inventory; no executable/SQL runs."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import stat
import zipfile

from package_windows import REQUIRED_RUNTIME, FORBIDDEN

REQUIRED_SOURCE = (
    'START-HERE.md', 'README.md', 'Migrate-RedMoon.cmd', 'Start-RedMoon.cmd', 'Stop-RedMoon.cmd',
    'Open-RedMoon-Source.cmd', 'setup/Migrate-RedMoon.ps1', 'setup/Common.ps1',
    'setup/Start-RedMoon.ps1', 'setup/Stop-RedMoon.ps1', 'src/RedMoon.sln', 'src/Directory.Build.props',
    'src/global.json', 'licenses/FoxConquer5517-MIT.txt',
    'src/RedMoon.AccountServer/RedMoon.AccountServer.csproj',
    'src/RedMoon.GameServer/RedMoon.GameServer.csproj', 'src/RedMoon.Core/RedMoon.Core.csproj',
    'src/RedMoon.Data/RedMoon.Data.csproj', 'src/RedMoon.Admin/RedMoon.Admin.csproj',
    'server/GameMap.dat', 'database/account.sql', 'database/game.sql',
    'third-party-packages.json', 'data-provenance.json',
    'docs/verification/integration-results.json', 'docs/verification/migration-results.json',
)


def verify(archive_path, expected_head=None, extract=None):
    archive_path = Path(archive_path)
    if extract is not None:
        extract = Path(extract).resolve()
        if extract.exists():
            raise ValueError('Extraction must use a NEW directory, never an installed folder')
    with zipfile.ZipFile(archive_path) as archive:
        members = archive.infolist()
        names = [item.filename for item in members]
        if len(names) != len(set(names)) or len(names) != len(set(n.casefold() for n in names)):
            raise ValueError('Duplicate or case-colliding Windows archive paths')
        for item in members:
            parts = PurePosixPath(item.filename).parts
            if (not parts or '..' in parts or PurePosixPath(item.filename).is_absolute()
                    or '\\' in item.filename or ':' in item.filename or stat.S_ISLNK(item.external_attr >> 16)):
                raise ValueError('Unsafe archive path')
        manifest = json.loads(archive.read('bundle-manifest.json'))
        if manifest.get('layout_version') != 2 or manifest.get('credentials_included') is not False:
            raise ValueError('Not a clean organized RedMoon layout-v2 bundle')
        if expected_head is not None and manifest.get('source_head') != expected_head:
            raise ValueError('Bundle source commit does not match the tested commit')
        inventory = manifest['files']
        if set(names) != set(inventory) | {'bundle-manifest.json'}:
            raise ValueError('ZIP inventory differs from its manifest')
        missing = (set(REQUIRED_RUNTIME) | set(REQUIRED_SOURCE)) - set(names)
        if missing:
            raise ValueError('Missing source/runtime/verification: ' + ', '.join(sorted(missing)))
        for name in names:
            if any(name == bad or name.startswith(bad + '/') for bad in FORBIDDEN):
                raise ValueError('Configured runtime data must not ship: ' + name)
            if name.startswith(('downloads/', 'vendor/', 'canyon/', 'src/FoxConquer5517/', '.local-migration-')):
                raise ValueError('Obsolete/duplicate source or migration state in bundle')
        for name, expected in inventory.items():
            if hashlib.sha256(archive.read(name)).hexdigest() != expected:
                raise ValueError('Archive member SHA256 mismatch: ' + name)
        if extract is not None:
            extract.mkdir(parents=True)
            for name in names:
                target = extract / name
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(archive.read(name))
    return {'source_head': manifest['source_head'], 'passed': True, 'verified_files': len(names),
            'sha256': hashlib.sha256(archive_path.read_bytes()).hexdigest(),
            'bytes': archive_path.stat().st_size, 'all_member_hashes_match': True,
            'contains_credentials': False, 'real_client_executed': False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive', type=Path, required=True)
    parser.add_argument('--head', required=True)
    parser.add_argument('--extract', type=Path)
    parser.add_argument('--report', type=Path, required=True)
    args = parser.parse_args()
    report = verify(args.archive, args.head, args.extract)
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
