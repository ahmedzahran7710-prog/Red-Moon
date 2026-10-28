"""Bundle source and fresh published outputs only, never a configured CI runtime."""
import argparse, hashlib, json, os, shutil, subprocess, zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
p = argparse.ArgumentParser()
p.add_argument('--staging', type=Path, required=True)
p.add_argument('--data', type=Path, required=True)
p.add_argument('--output', type=Path, required=True)
a = p.parse_args()
if not (a.staging / 'server/MsgServer.exe').is_file(): raise ValueError('Publish first')
for forbidden in ('.local', 'server/Logs', 'server/redmoon.stop'):
    if (a.staging / forbidden).exists(): raise ValueError('Never package a previously configured test/user runtime')
for path in subprocess.check_output(['git','ls-files','-z'],cwd=ROOT).decode().split('\0'):
    if not path or path.startswith('.github/') or path.startswith('audit/evidence/'): continue
    src=ROOT/path; target=a.staging/path
    if src.is_file():
        target.parent.mkdir(parents=True,exist_ok=True); shutil.copyfile(src,target)
shutil.copytree(a.data/'runtime-data',a.staging/'server',dirs_exist_ok=True)
shutil.copytree(a.data/'database',a.staging/'database')
shutil.copyfile(a.data/'data-provenance.json',a.staging/'data-provenance.json')
# Collect package license files and metadata from the verified restore cache.
licenses=[]
for assets in (ROOT/'src').rglob('project.assets.json'):
    obj=json.loads(assets.read_text(encoding='utf-8-sig'))
    folders=[Path(s) for s in obj.get('packageFolders',{})]
    for key,item in obj.get('libraries',{}).items():
        if item.get('type')!='package':continue
        name,version=key.rsplit('/',1)
        for folder in folders:
            directory=folder/name.lower()/version
            if not directory.is_dir(): continue
            for file in directory.rglob('*'):
                if file.is_file() and (file.suffix=='.nuspec' or 'license' in file.name.lower() or 'notice' in file.name.lower()):
                    dest=a.staging/'third-party-licenses'/name/version/file.relative_to(directory)
                    dest.parent.mkdir(parents=True,exist_ok=True); shutil.copyfile(file,dest)
            licenses.append({'name':name,'version':version,'package_url':f'https://www.nuget.org/packages/{name}/{version}'})
            break
unique={x['name']+'/'+x['version']:x for x in licenses}
(a.staging/'third-party-packages.json').write_text(json.dumps(list(unique.values()),indent=2)+'\n')
head=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()
files={str(f.relative_to(a.staging)).replace('\\','/'):hashlib.sha256(f.read_bytes()).hexdigest() for f in a.staging.rglob('*') if f.is_file()}
(a.staging/'bundle-manifest.json').write_text(json.dumps({'source_head':head,'platform':'win-x64','self_contained':True,'client_included':False,'credentials_included':False,'files':files},indent=2)+'\n')
a.output.parent.mkdir(parents=True,exist_ok=True)
with zipfile.ZipFile(a.output,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for f in sorted(a.staging.rglob('*')):
        if f.is_file():z.write(f,str(f.relative_to(a.staging)))
report={'source_head':head,'file':a.output.name,'bytes':a.output.stat().st_size,'sha256':hashlib.sha256(a.output.read_bytes()).hexdigest(),'self_contained':True,'client_included':False,'credentials_included':False}
a.output.with_suffix('.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
