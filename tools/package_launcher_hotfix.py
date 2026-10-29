#!/usr/bin/env python3
"""Build a small, allowlisted path-fix ZIP; never include owner config/state/data."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[1]
NAMES = ('Setup-RedMoon', 'Start-RedMoon', 'Stop-RedMoon', 'Install-ClientConfig', 'Build-RedMoon')
FILES = tuple(name + '.cmd' for name in NAMES) + tuple('setup/' + name + '.ps1' for name in (*NAMES, 'Allow-Hamachi'))
FIX_ID = '2026-10-29-launcher-paths'
INSTRUCTIONS = '''# تصحيح مسار التشغيل — Red Moon

هذا تصحيح لرسالة Split-Path / Path is an empty string عند تشغيل Setup-RedMoon.cmd.

1. اقفل نافذة الخطأ. لا تحذف أي داتابيز أو ملفات server أو .local.
2. فك محتويات هذا ZIP فوق مجلد الحزمة الموجودة: F:\\AhmedZahran\\RedMoon\\RedMoon-Sorce
3. وافق على استبدال ملفات CMD والملفات الستة داخل setup فقط. لا تستبدل مجلد setup كله بآخر فارغ.
4. افتح Setup-RedMoon.cmd مرة أخرى. كلمة مرور DB تدخل في النافذة المحلية فقط، ولا تبعتها في المحادثة.
5. لو ظهر خطأ آخر، احتفظ برسالته وتوقف؛ لا تحذف قواعد أو تعاود الاستيراد يدويًا.

التصحيح لا يحتوي owner-profile.json أو Common.ps1 أو SQL أو binaries أو كلمات مرور أو vault.
لو عدّلت أيًا من سكربتات التشغيل بنفسك، احتفظ بنسخة منها قبل الاستبدال.
الإصلاح ينقل حساب المسار إلى جسم السكربت ويمرّر المسارات صراحة من CMD، ولا يغيّر منطق اللعبة.
'''


def build(output: Path):
    if (ROOT / "src/RedMoon.GameServer").is_dir():
        raise RuntimeError("Legacy launcher-only hotfix retired: the renamed runtime requires the full Windows bundle and Migrate-RedMoon.cmd. Do not overwrite an old installation with these launchers.")
    contents = {}
    for relative in FILES:
        contents[relative] = (ROOT / relative).read_bytes()
    head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
    manifest = {
        'fix_id': FIX_ID, 'source_head': head,
        'scope': '11 launcher/entrypoint files only; preserves owner-profile, Common, runtime, databases, and local secrets',
        'files': {name: hashlib.sha256(raw).hexdigest() for name, raw in sorted(contents.items())}
    }
    contents['HOTFIX-LAUNCHERS.md'] = INSTRUCTIONS.encode('utf-8')
    contents['launcher-hotfix-manifest.json'] = (json.dumps(manifest, indent=2) + '\n').encode('utf-8')
    output.parent.mkdir(parents=True, exist_ok=True)
    # STORE is intentional: cross-platform zlib versions must not alter this tiny ZIP.
    with zipfile.ZipFile(output, 'w', compression=zipfile.ZIP_STORED) as archive:
        for name, raw in sorted(contents.items()):
            info = zipfile.ZipInfo(name, (2026, 10, 29, 0, 0, 0))
            info.compress_type = zipfile.ZIP_STORED
            info.create_system = 3
            info.external_attr = 0o100644 << 16
            archive.writestr(info, raw)
    report = {'fix_id': FIX_ID, 'source_head': head, 'file': output.name,
              'bytes': output.stat().st_size, 'sha256': hashlib.sha256(output.read_bytes()).hexdigest(),
              'replaced_files': len(FILES), 'contains_credentials': False, 'contains_database_or_runtime': False}
    output.with_suffix('.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(build(args.output), indent=2))
