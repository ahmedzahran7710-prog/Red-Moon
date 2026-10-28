#!/usr/bin/env python3
"""Produce clean NEW-installation data from the verified archive, outside Git.

Whitelist reference-content seed inserts; discard old accounts/player state.
No SQL execution, original edits, or changes to an owner's existing databases.
"""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import shutil
import fox_baseline

REFERENCE_TABLES = set('''cq_action cq_config cq_disdain cq_dyna_rank_type cq_dynanpc
cq_fuse cq_generator cq_goods cq_itemaddition cq_itemtype cq_levexp cq_lottery
cq_magictype cq_magictypeop cq_map cq_monster_magic cq_monstertype cq_npc cq_passway
cq_point_allot cq_portal cq_questtype cq_quiz cq_rebirth cq_refinery cq_region
cq_student_type cq_task cq_trap cq_traptype cq_tutor_battle_limit_type cq_tutor_type
cq_unlawful dyna_rank_type dynamic_rank_type'''.split())
SEED = re.compile(r'^INSERT INTO `([^`]+)` VALUES ', re.I)


def validate_schema_scope(text):
    if re.search(r'(?im)^\s*(?:USE\s|(?:CREATE|DROP|ALTER)\s+DATABASE\s)', text):
        raise ValueError('An installation seed must not select/create/drop another schema')
    if re.search(r'(?im)^\s*(?:UPDATE|INSERT\s+INTO|DELETE\s+FROM|ALTER\s+TABLE|DROP\s+TABLE|TRUNCATE\s+TABLE)\s+`?(?:zf|accountzf)`?\s*\.', text):
        raise ValueError('Cross-schema statement in a local installation seed')



def clean_dump(text, keep):
    kept, removed = Counter(), Counter()
    output = []
    for line in text.splitlines(keepends=True):
        match = SEED.match(line)
        if match:
            # This pinned Navicat format has one complete VALUES statement per line.
            if not line.rstrip().endswith(';'):
                raise ValueError('Unexpected multi-line seed statement; stop rather than incompletely redact')
            table = match.group(1)
            if table not in keep:
                removed[table] += 1
                continue
            kept[table] += 1
        elif line.startswith('INSERT INTO '):
            raise ValueError('Unexpected top-level INSERT format')
        # Stored procedure bodies are indented; keep their definitions, not execute them.
        output.append(line)
    return ''.join(output), dict(kept), dict(removed)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--source', type=Path, required=True)
    p.add_argument('--dest', type=Path, required=True)
    args = p.parse_args()
    fox_baseline.verify(args.source, full=True)
    dest = args.dest.resolve()
    if dest.exists() and any(dest.iterdir()):
        raise ValueError('Output must be a new empty staging directory, never an existing installation')
    if fox_baseline.ROOT in dest.parents and (fox_baseline.ROOT / '.cache').resolve() not in dest.parents:
        raise ValueError('Generated datasets inside this repo belong only in .cache/')
    (dest / 'database').mkdir(parents=True, exist_ok=True)
    report = {'upstream_commit': fox_baseline.COMMIT, 'source_modified': False, 'databases_imported': False, 'seed_policy': 'reference-table allowlist; no old accounts or player-owned records', 'files': {}}
    for source_name, output_name, keep in [('accountzf.sql', 'account.sql', set()), ('zf.sql', 'game.sql', REFERENCE_TABLES)]:
        raw = (args.source / 'Database' / source_name).read_text(encoding='utf-8-sig')
        cleaned, kept, removed = clean_dump(raw, keep)
        if source_name == 'zf.sql':
            cleaned += '\n-- Fresh-install state only: no former guild ownership; original templates retained.\nUPDATE `cq_dynanpc` SET `ownerid`=0, `life`=`maxlife`;\n'
        output = dest / 'database' / output_name
        output.write_text(cleaned, encoding='utf-8', newline='\n')
        report['files'][output_name] = {'kept_seed_statements': kept, 'removed_seed_statements': removed, 'sha256': fox_baseline.digest(output)}
    shutil.copyfile(args.source / 'Database/fix_cq_levexp.sql', dest / 'database/fix_cq_levexp.sql')
    # The upstream optional message patch explicitly targets zf.cq_action. Never
    # execute it against an owner's existing zf when installing fresh Red Moon schemas.
    messages = ("UPDATE `cq_action` SET `param`='Welcome to Red Moon Classic+! Report issues during local testing.' WHERE `id`=102;\n"
                "UPDATE `cq_action` SET `param`='Red Moon local test build. Please use the tested matching client.' WHERE `id`=104;\n")
    (dest / 'database/cq_action_mod.sql').write_text(messages, encoding='utf-8', newline='\n')
    report['optional_message_patch'] = 'Replaced upstream zf-qualified message UPDATEs with unqualified Red Moon text for the selected NEW schema only'
    for sql in (dest / 'database').glob('*.sql'):
        validate_schema_scope(sql.read_text(encoding='utf-8-sig'))
    shutil.copytree(args.source / 'RequiredFiles', dest / 'runtime-data')
    report['runtime_assets'] = 'Original RequiredFiles only; no native DLLs or upstream credential examples'
    (dest / 'data-provenance.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'destination': str(dest), 'clean_sql_files': 4, 'accounts_imported': 0,
                      'original_account_seeds_removed': report['files']['account.sql']['removed_seed_statements'].get('account', 0),
                      'original_character_seeds_removed': report['files']['game.sql']['removed_seed_statements'].get('cq_user', 0)}, indent=2))


if __name__ == '__main__':
    main()
