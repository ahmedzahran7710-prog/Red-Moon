#!/usr/bin/env python3
"""Package the complete maintained RedMoon source and CLEAN Windows runtime.

Never package an installed/user/CI runtime. Historical upstream source and old
patches stay in GitHub rather than presenting a second competing source tree in
the runnable distribution. The pinned upstream manifest and all licenses remain.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[1]
ENTRYPOINTS = ('RedMoon.AccountServer', 'RedMoon.GameServer', 'RedMoon.Admin')
REQUIRED_RUNTIME = tuple('server/' + name + suffix for name in ENTRYPOINTS
                         for suffix in ('.exe', '.dll', '.deps.json', '.runtimeconfig.json')) + (
    'server/RedMoon.Core.dll', 'server/RedMoon.Data.dll',
)
FORBIDDEN = ('.local', 'client-config', 'ci-profile.json', 'server/Logs', 'server/redmoon.stop',
             'server/RedMoon.AccountServer.ini', 'server/RedMoon.GameServer.ini',
             'server/Login.cfg', 'server/Shell.ini', 'server/libeay32.dll',
             'server/DB.dll', 'server/ServerCore.dll', 'server/DB.pdb', 'server/ServerCore.pdb') + tuple(
                 'server/' + name + suffix for name in ('LoginServer', 'MsgServer')
                 for suffix in ('.exe', '.dll', '.pdb', '.deps.json', '.runtimeconfig.json'))
# Keep a single maintained source tree; history/vendor remain available in Git.
EXCLUDED_PREFIXES = ('.github/', 'vendor/', 'canyon/', 'downloads/', 'audit/', 'tests/managed/', 'src/FoxConquer5517/')
EXCLUDED_FILES = {'tests/test_baseline.py', 'tools/package_launcher_hotfix.py', 'tests/test_launcher_hotfix.py'}
REFERENCE_MANIFEST = 'audit/fox5517-manifest.json'
VERIFICATION_FILES = ('integration-results.json', 'legacy-integration-results.json', 'migration-results.json')


def included_source(relative):
    return relative == REFERENCE_MANIFEST or (
        relative not in EXCLUDED_FILES and not relative.startswith(EXCLUDED_PREFIXES))


def validate_staging(staging):
    staging = Path(staging)
    for relative in REQUIRED_RUNTIME:
        if not (staging / relative).is_file():
            raise ValueError('Publish all three RedMoon entrypoints and shared assemblies first: ' + relative)
    for relative in FORBIDDEN:
        if (staging / relative).exists():
            raise ValueError('Never package a configured, mixed-name, or credential-bearing runtime: ' + relative)
    if any(staging.glob('.local-migration-*')):
        raise ValueError('Interrupted migration state must never be packaged')
    for file in staging.rglob('*'):
        if file.is_symlink():
            raise ValueError('Distribution staging must not contain symbolic links')


def copy_source(staging, source_root=ROOT):
    tracked = subprocess.check_output(['git', 'ls-files', '-z'], cwd=source_root).decode().split('\0')
    for relative in tracked:
        if not relative or not included_source(relative):
            continue
        source = source_root / relative
        if not source.is_file() or source.is_symlink():
            raise ValueError('Missing/unsafe tracked source: ' + relative)
        target = staging / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)


def copy_licenses(staging, source_root=ROOT):
    packages = {}
    for assets in (source_root / 'src').rglob('project.assets.json'):
        obj = json.loads(assets.read_text(encoding='utf-8-sig'))
        folders = [Path(s) for s in obj.get('packageFolders', {})]
        for key, item in obj.get('libraries', {}).items():
            if item.get('type') != 'package':
                continue
            name, version = key.rsplit('/', 1)
            for folder in folders:
                directory = folder / name.lower() / version
                if not directory.is_dir():
                    continue
                for file in directory.rglob('*'):
                    if file.is_file() and (file.suffix == '.nuspec' or 'license' in file.name.lower() or 'notice' in file.name.lower()):
                        dest = staging / 'third-party-licenses' / name / version / file.relative_to(directory)
                        dest.parent.mkdir(parents=True, exist_ok=True)
                        shutil.copyfile(file, dest)
                packages[key] = {'name': name, 'version': version,
                                 'package_url': f'https://www.nuget.org/packages/{name}/{version}'}
                break
    if not packages:
        raise ValueError('Restored dependency license metadata is missing; do not distribute without it')
    (staging / 'third-party-packages.json').write_text(
        json.dumps(sorted(packages.values(), key=lambda p: (p['name'], p['version'])), indent=2) + '\n', encoding='utf-8')


def copy_verification(staging, directory, head):
    for filename in VERIFICATION_FILES:
        source = directory / filename
        if not source.is_file() or source.stat().st_size > 40_000:
            raise ValueError('Missing/oversized verification report: ' + filename)
        report = json.loads(source.read_text(encoding='utf-8-sig'))
        if filename == 'migration-results.json':
            ok = (report.get('passed') is True and report.get('source_head') == head and
                  report.get('legacy_source_head') == 'a5932d55ce7160e8a010bbb9d57b634c51576de3')
        else:
            ok = all(report.get(key) is True for key in (
                'both_config_checks_passed', 'synthetic_login_and_redirect', 'both_servers_ready',
                'login_token_handoff', 'graceful_shutdown_passed'))
            if filename == 'integration-results.json':
                ok = ok and report.get('source_root') == head
            else:
                ok = ok and report.get('source_root') == 'a5932d55ce7160e8a010bbb9d57b634c51576de3'
        if not ok or report.get('real_client_executed') is not False:
            raise ValueError('Verification is missing, failed, mismatched or overclaims a real client: ' + filename)
        target = staging / 'docs/verification' / filename
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--staging', type=Path, required=True)
    parser.add_argument('--data', type=Path, required=True)
    parser.add_argument('--verification', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    staging = args.staging.resolve()
    if staging == ROOT or ROOT in staging.parents:
        raise ValueError('Delivery staging must be outside the Git checkout')
    if args.output.resolve() == staging or staging in args.output.resolve().parents:
        raise ValueError('Output ZIP must be outside staging')
    if args.output.exists():
        raise ValueError('Refusing to overwrite an existing delivery archive')
    dirty = subprocess.check_output(['git', 'status', '--porcelain', '--untracked-files=all'], cwd=ROOT, text=True)
    if dirty.strip():
        raise ValueError('Package only a clean fully tracked source checkout; untracked source could otherwise enter binaries but not the source archive')
    head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
    validate_staging(staging)
    copy_source(staging)
    shutil.copytree(args.data / 'runtime-data', staging / 'server', dirs_exist_ok=True)
    shutil.copytree(args.data / 'database', staging / 'database')
    shutil.copyfile(args.data / 'data-provenance.json', staging / 'data-provenance.json')
    copy_licenses(staging)
    copy_verification(staging, args.verification, head)
    validate_staging(staging)
    files = {file.relative_to(staging).as_posix(): hashlib.sha256(file.read_bytes()).hexdigest()
             for file in sorted(staging.rglob('*')) if file.is_file()}
    metadata = {'layout_version': 2, 'source_head': head, 'platform': 'win-x64', 'self_contained': True,
                'client_included': False, 'credentials_included': False, 'old_player_data_included': False,
                'source_solution': 'src/RedMoon.sln', 'existing_installation_entrypoint': 'Migrate-RedMoon.cmd',
                'entrypoints': list(ENTRYPOINTS)}
    (staging / 'bundle-manifest.json').write_text(json.dumps({**metadata, 'files': files}, indent=2) + '\n', encoding='utf-8')
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(args.output, 'x', zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for file in sorted(staging.rglob('*')):
            if file.is_file():
                archive.write(file, file.relative_to(staging).as_posix())
    report = {**metadata, 'file': args.output.name, 'bytes': args.output.stat().st_size,
              'sha256': hashlib.sha256(args.output.read_bytes()).hexdigest(),
              'csharp_source_files': len(list((staging / 'src').rglob('*.cs'))),
              'legacy_navigation_patch_included': False, 'pristine_vendor_available_in_git': True}
    args.output.with_suffix('.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
