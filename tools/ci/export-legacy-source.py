#!/usr/bin/env python3
"""Export our reviewed pre-layout source for disposable migration CI; never checkout another branch."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import subprocess
import tarfile
import tempfile

LEGACY_COMMIT = 'a5932d55ce7160e8a010bbb9d57b634c51576de3'
PREFIXES = ('src/FoxConquer5517/', 'src/RedMoon.Admin/', 'setup/')
FILES = {'src/global.json', 'tools/ci/publish-runtime.ps1', 'tools/ci/test-setup.ps1'}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--destination', type=Path, required=True)
    parser.add_argument('--repository', required=True)
    args = parser.parse_args()
    dest = args.destination.resolve()
    if dest.exists():
        raise ValueError('Legacy CI export destination must not already exist')
    if args.repository != 'ahmedzahran7710-prog/Red-Moon':
        raise ValueError('This regression targets the reviewed private RedMoon baseline only')
    repo = Path(__file__).resolve().parents[2]
    if dest == repo or repo in dest.parents:
        raise ValueError('Export old source outside the working checkout; never switch its branch')
    files = {}
    with tempfile.TemporaryFile() as downloaded:
        # GH_TOKEN is supplied through the CI environment, never arguments or files.
        subprocess.run(['gh', 'api', f'repos/{args.repository}/tarball/{LEGACY_COMMIT}'], stdout=downloaded, check=True)
        downloaded.seek(0)
        with tarfile.open(fileobj=downloaded, mode='r:gz') as archive:
            for member in archive:
                parts = PurePosixPath(member.name).parts
                if len(parts) < 2:
                    continue
                relative = PurePosixPath(*parts[1:]).as_posix()
                if relative not in FILES and not relative.startswith(PREFIXES):
                    continue
                if member.isdir():
                    continue
                if not member.isfile() or '..' in parts or relative.startswith('/') or '\\' in relative:
                    raise ValueError('Unsafe source archive member')
                file = dest / relative
                if file.exists():
                    raise ValueError('Duplicate source archive member')
                raw = archive.extractfile(member).read()
                file.parent.mkdir(parents=True, exist_ok=True)
                file.write_bytes(raw)
                files[relative] = hashlib.sha256(raw).hexdigest()
    for relative in FILES | {'src/FoxConquer5517/LoginServer/LoginServer.csproj', 'src/FoxConquer5517/MsgServer/MsgServer.csproj'}:
        if relative not in files:
            raise ValueError('Incomplete reviewed legacy source export: ' + relative)
    (dest / 'legacy-source.json').write_text(json.dumps({'source_head': LEGACY_COMMIT, 'files': files}, indent=2) + '\n')
    print(json.dumps({'source_head': LEGACY_COMMIT, 'files_exported': len(files), 'branch_switched': False}))


if __name__ == '__main__':
    main()
