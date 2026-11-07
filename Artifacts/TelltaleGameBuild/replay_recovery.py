import pathlib,json,re,subprocess
base=pathlib.Path.cwd();out=base/'Artifacts/TelltaleGameBuild/recovery';stage=out/'staged';backup=out/'before'
tracked=subprocess.check_output(['git','-c','safe.directory='+base.as_posix(),'ls-files','Assets/Scripts','Assets/VoronoirShred'],text=True).splitlines()
tracked={p for p in tracked if p.endswith('.cs')}
current={p:(base/p).read_text(encoding='utf-8-sig',errors='replace') for p in tracked if (base/p).exists()}
changes=[];fail=[];events=[]
def relative(p):
 p=p.replace('\\','/');p=re.sub(r'^D:/Unity/FPS-PayDayGame/','',p,flags=re.I);return p
for event in json.loads((out/'patches.json').read_text(encoding='utf-8')):
 if event['time']<'2026-10-15':continue
 patch=event['patch']
 for match in re.finditer(r'\*\*\* (Update|Add) File: ([^\n]+)\n(.*?)(?=\n\*\*\* (?:Update|Add|Delete|End)|\Z)',patch,re.S):
  kind,path,body=match.groups();path=relative(path)
  if path in current:events.append((event['time'],'patch',path,body,kind))
cmds=json.loads((out/'commands.json').read_text(encoding='utf-8'))
for index in [19,20,38]:
 c=cmds[index];s=c['command']
 for m in re.finditer(r"Set-TaskFile '([^']+)' @'\n(.*?)\n'@",s,re.S):
  path=relative(m[1]);
  if path in current:events.append((c['time'],'full',path,m[2]+'\n',''))
 # Here-string piped into Set-Content with an explicit literal path or $taskFile.
 var=re.search(r"\$taskFile\s*=\s*'([^']+)'",s)
 for m in re.finditer(r"@'\n(.*?)\n'@\s*\|\s*Set-Content[^\n]*",s,re.S):
  tail=m[0].split("'@")[-1];literal=re.search(r"(?:-LiteralPath|-Path)\s+'([^']+)'",tail)
  path=relative(literal[1] if literal else var[1] if var else '')
  if path in current:events.append((c['time'],'full',path,m[1]+'\n',''))
# Apply the inspected small historical string replacements in a staging folder only.
for index in [19,22,26,29,30]:events.append((cmds[index]['time'],'ps',str(index),cmds[index]['command'],''))
def apply(old,body,path,time):
 lines=body.splitlines();chunks=[];chunk=[]
 for line in lines:
  if line.startswith('@@'):
   if chunk:chunks.append(chunk)
   chunk=[]
  elif line=='*** End of File':continue
  else:chunk.append(line)
 if chunk:chunks.append(chunk)
 for chunk in chunks:
  before='\n'.join(x[1:] for x in chunk if x and x[0] in ' -')
  after='\n'.join(x[1:] for x in chunk if x and x[0] in ' +')
  if before and before in old:old=old.replace(before,after,1);changes.append((time,path))
  elif after and after in old:pass
  else:fail.append((time,path,before[:100]))
 return old
for time,kind,path,body,extra in sorted(events):
 if kind=='full':current[path]=body;changes.append((time,path))
 elif kind=='patch' and extra=='Update':current[path]=apply(current[path],body,path,time)
 elif kind=='ps':
  for p,text in current.items():
   dest=stage/p;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_text(text,encoding='utf-8')
  # All these commands only transform specific source text before launching old QA.
  if path=='19':body=body[:body.index('$taskEdits =')]
  else:body=body[:body.rfind('[IO.File]::WriteAllText')]+body[body.rfind('[IO.File]::WriteAllText'):].splitlines()[0]
  body='\n'.join(l for l in body.splitlines() if not l.startswith('Stop-Process'))
  body=body.replace('D:\\Unity\\FPS-PayDayGame',str(stage))
  body=body.replace('$PWD',"'"+str(stage)+"'")
  ps=out/f'staged-transform-{path}.ps1';ps.write_text(body,encoding='utf-8-sig',newline='\n')
  subprocess.run(['powershell','-NoProfile','-ExecutionPolicy','Bypass','-File',str(ps)],check=True,capture_output=True)
  for p in current:current[p]=(stage/p).read_text(encoding='utf-8-sig',errors='replace')
for p,text in current.items():
 original=(base/p).read_text(encoding='utf-8-sig',errors='replace')
 if text==original:continue
 dest=stage/p;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_text(text,encoding='utf-8')
 dest=backup/p;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_text(original,encoding='utf-8')
diffs=[p for p in current if current[p]!=(base/p).read_text(encoding='utf-8-sig',errors='replace')]
(out/'replay-report.json').write_text(json.dumps({'changed':diffs,'failed_hunks':fail,'applied':changes},indent=2),encoding='utf-8')
print('changed:',len(diffs),'failed historical contexts:',len(fail));print('\n'.join(diffs));print('FAILURES',json.dumps(fail,indent=2))




