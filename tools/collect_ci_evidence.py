#!/usr/bin/env python3
"""Maintainer utility: SHA-verified, chunked diagnostic reports via GitHub API."""
import argparse,base64,gzip,hashlib,json,subprocess
from collections import defaultdict
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('run');p.add_argument('--repo',default='ahmedzahran7710-prog/Red-Moon');a=p.parse_args()
def gh(*args):return json.loads(subprocess.check_output(['gh',*args],text=True))
r=gh('run','view',a.run,'--repo',a.repo,'--json','databaseId,headSha,jobs,status,conclusion,url')
out=Path(__file__).resolve().parents[1]/'.cache'/('ci-'+a.run);out.mkdir(parents=True,exist_ok=True)
(out/'metadata.json').write_text(json.dumps(r,indent=2)+'\n')
def display(name,data):
    if 'Executed' in data:
        print('COMPONENTS:',data['Executed'],data['Passed'],data['Failed'],data['Framework'])
        for test in data['Results']:
            if test['Status']!='PASS':print(test)
    else:print(name,json.dumps(data,ensure_ascii=False)[:16000])
for job in r['jobs']:
    print('JOB:',job['name'],job['status'],job['conclusion'])
    print('Non-success steps:',[(s['name'],s['conclusion']) for s in job['steps'] if s['conclusion'] not in ('success','')])
    j=gh('api',f'repos/{a.repo}/actions/jobs/{job["databaseId"]}')
    endpoint=j['check_run_url'].removeprefix('https://api.github.com/')+'/annotations?per_page=100'
    chunks=defaultdict(list)
    for note in gh('api',endpoint):
        if note['title'].startswith('FOX_EVIDENCE_'):
            try:e=json.loads(note['message'])
            except json.JSONDecodeError:
                print('UNREADABLE old transport:',note['title'],len(note['message']));continue
            assert e['run_id']==a.run and e['head_sha']==r['headSha']
            if e['evidence_schema']==3:chunks[e['file']].append(e);continue
            if e['evidence_schema']==2:
                raw=base64.b64decode(e['payload_base64'],validate=True);assert hashlib.sha256(raw).hexdigest()==e['file_sha256']
                data=json.loads(raw.decode('utf-8-sig'));(out/(str(job['databaseId'])+'-'+e['file'])).write_bytes(raw)
            else:data=e['payload']
            display(e['file'],data)
        elif note['annotation_level']=='failure':
            print('ERROR:',note['path'],note['start_line'],note['message'][:2500])
    for name,parts in chunks.items():
        parts.sort(key=lambda e:e['part']);first=parts[0]
        assert [e['part'] for e in parts]==list(range(first['parts'])), 'Incomplete evidence chunks'
        assert all(e['file_sha256']==first['file_sha256'] for e in parts)
        raw=gzip.decompress(base64.b64decode(''.join(e['payload_chunk'] for e in parts),validate=True))
        assert len(raw)<100000 and hashlib.sha256(raw).hexdigest()==first['file_sha256']
        (out/(str(job['databaseId'])+'-'+name)).write_bytes(raw)
        (out/(str(job['databaseId'])+'-'+name+'.envelopes.json')).write_text(json.dumps(parts,indent=2)+'\n')
        display(name,json.loads(raw.decode('utf-8-sig')))
