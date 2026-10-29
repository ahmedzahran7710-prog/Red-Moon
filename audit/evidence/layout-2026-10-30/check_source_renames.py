"""One-off STATIC layout audit; run from repo root with the pinned base Git object present.

This is not a build or runtime test, and intentionally is not a forever-frozen
unit test for future gameplay development. It checks only this rename batch.
Output is .cache/refactor-static-review.json; no source is modified.
"""
from pathlib import Path
import subprocess,re,json,hashlib
base='a5932d55ce7160e8a010bbb9d57b634c51576de3'
moves=json.loads(Path('docs/reference/REDMOON-RENAMES.json').read_text())['moves']
oldfiles=subprocess.check_output(['git','ls-tree','-r','--name-only',base,'--','src'],text=True).splitlines()
oldfiles=[p for p in oldfiles if p.endswith('.cs')]
ns={'LoginServer.Network.LoginServer':'RedMoon.AccountServer.Network.ClientTransport','LoginServer.Network.MsgServer':'RedMoon.AccountServer.Network.GameLink','MsgServer.Network.GameServer':'RedMoon.GameServer.Network.ClientTransport','MsgServer.Network.LoginServer':'RedMoon.GameServer.Network.AccountLink','Core.Common.Enums':'RedMoon.Core.Common.Enums','ServerCore':'RedMoon.Core','LoginServer':'RedMoon.AccountServer','MsgServer':'RedMoon.GameServer','DB':'RedMoon.Data'}
pat=re.compile(r'(?<![\w.])(?:'+'|'.join(re.escape(k) for k in sorted(ns,key=len,reverse=True))+r')\b')
def rewrite(s):
 m=re.search(r'(?m)^(?:using |namespace )',s);pos=m.start() if m else 0;header,b=s[:pos],s[pos:]
 for a,v in [('ServerKernel.LoginServerAddress','ServerKernel.AccountServerAddress'),('ServerKernel.LoginServerPort','ServerKernel.AccountServerPort'),('ServerKernel.LoginServer','ServerKernel.AccountServerConnection'),('LoginClient LoginServer;','LoginClient AccountServerConnection;'),('string LoginServerAddress','string AccountServerAddress'),('int LoginServerPort','int AccountServerPort')]:b=b.replace(a,v)
 b=pat.sub(lambda m:ns[m.group()],b)
 for a,v in [('Login.cfg','RedMoon.AccountServer.ini'),('Shell.ini','RedMoon.GameServer.ini'),('"FoxConquer"','"RedMoon"'),('FoxConquer Project','Red Moon Classic+'),('Welcome to FoxConquer!','Welcome to Red Moon!'),('Welcome to World Conquer Online Server.','Welcome to Red Moon Classic+!'),('RedMoon.GameServer.Structures','RedMoon.GameServer.Gameplay'),('RedMoon.GameServer.Threads','RedMoon.GameServer.Runtime'),('"zfserver"','"RedMoon"'),('"Starting login server, please wait..."','"Starting RedMoon.AccountServer..."'),('"[Red Moon Classic+] Login Server"','"[RedMoon] Account Server"'),('"Starting server..."','"Starting RedMoon.GameServer..."')]:b=b.replace(a,v)
 return header+b
normalize=lambda s:'\n'.join(line.rstrip() for line in s.splitlines())
missing=[];different=[];targets=[]
for old in oldfiles:
 new=old
 for move in moves:
  if 'to' in move and (new==move['from'] or new.startswith(move['from']+'/')):new=move['to']+new[len(move['from']):]
 targets.append(new)
 if not Path(new).is_file():missing.append(new);continue
 before=subprocess.check_output(['git','show',base+':'+old]).decode('utf-8-sig')
 after=Path(new).read_bytes().decode('utf-8-sig')
 if normalize(rewrite(before))!=normalize(after):different.append(new)
actual={str(p) for p in Path('src').rglob('*.cs')}
report={'base':base,'maintained_csharp_files':len(oldfiles),'matched_declared_renames':len(oldfiles)-len(missing)-len(different),'missing':missing,'other_differences':different,'extra_csharp':sorted(actual-set(targets)),'scope':'Static lexical comparison after explicit namespace/field/config/branding renames and trailing-whitespace normalization. NOT a compilation or runtime proof.'}
print(json.dumps(report,indent=2))
report['reviewed_csharp_sha256']={p.as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(Path('src').rglob('*.cs'))}
Path('.cache').mkdir(exist_ok=True)
Path('.cache/refactor-static-review.json').write_text(json.dumps(report,indent=2)+'\n')
assert not missing and not different and actual==set(targets)
