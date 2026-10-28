"""Small redacted failure context; never upload an initialized DB, vault or raw SQL."""
import json, os, re
from pathlib import Path
root=Path(os.environ['RUNNER_TEMP']);runtime=root/'redmoon-ci'
secrets=[]
vault=runtime/'.local/ci-secrets.json'
if vault.exists():secrets=[str(v) for v in json.loads(vault.read_text(encoding='utf-8-sig')).values()]
result={}
paths=[root/'maria.stdout.log',root/'maria.stderr.log',runtime/'.local/logs/LoginServer.stdout.log',runtime/'.local/logs/LoginServer.stderr.log',runtime/'.local/logs/MsgServer.stdout.log',runtime/'.local/logs/MsgServer.stderr.log']
for p in paths:
    if not p.exists():continue
    lines=p.read_text(encoding='utf-8-sig',errors='replace').splitlines()[-24:]
    clean=[]
    for line in lines:
        if re.search(r'(?i)(password\s*[=:]|identified by|INSERT INTO.*VALUES|runtime-secrets|TRANSFER_KEY|TRANSFER_SALT)',line):
            clean.append('[sensitive diagnostic line suppressed]');continue
        for secret in secrets:
            if secret:line=line.replace(secret,'[redacted]')
        clean.append(line[:280])
    result[p.name]='\n'.join(clean)[-2000:]
if result:
    out=root/'fox-evidence';out.mkdir(exist_ok=True)
    (out/'startup-diagnostics.json').write_text(json.dumps({'source_head':os.environ.get('GITHUB_SHA'),'redacted_ci_only_logs':result},indent=2)+'\n')
