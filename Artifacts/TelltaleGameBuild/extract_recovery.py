from recover_inventory import read
import pathlib,json,re
base=pathlib.Path(r'C:/Users/Edgar.DESKTOP-BOONDKL/.codex/sessions')
ps=list((base/'2026/10/18').glob('*01a150f5*'))+list((base/'2026/09/07').glob('*01a07c3d*'))+list((base/'2026/10/02').glob('*01a0fc35*'))
calls=[]
for p in ps:
 for line in read(p).splitlines():
  try:o=json.loads(line)
  except:continue
  q=o.get('payload',{})
  if q.get('type') not in ('function_call','custom_tool_call'):continue
  s=q.get('input',q.get('arguments',''))
  if not any(t in s for t in ('apply_patch','Set-Content','write_text','WriteAllText')):continue
  calls.append((o.get('timestamp',''),s,p.name))
calls.sort()
out=pathlib.Path('Artifacts/TelltaleGameBuild/recovery');out.mkdir(exist_ok=True)
patches=[];commands=[]
for time,s,file in calls:
 for match in re.finditer(r'tools\.apply_patch\(("(?:\\.|[^"\\])*")\)',s):
  try:patch=json.loads(match[1])
  except:continue
  if '\\n' in patch and '\n' not in patch:patch=patch.replace('\\n','\n')
  patches.append({'time':time,'source':file,'patch':patch})
 for match in re.finditer(r'cmd\s*:\s*("(?:\\.|[^"\\])*")',s):
  try:cmd=json.loads(match[1])
  except:continue
  if '\\n' in cmd and '\n' not in cmd:cmd=cmd.replace('\\n','\n')
  if any(x in cmd for x in ('Set-Content','WriteAllText','write_text')):commands.append({'time':time,'source':file,'command':cmd})
(out/'patches.json').write_text(json.dumps(patches,ensure_ascii=False),encoding='utf-8')
(out/'commands.json').write_text(json.dumps(commands,ensure_ascii=False),encoding='utf-8')
print(len(patches),'patches',len(commands),'commands')
for i,c in enumerate(commands):
 s=c['command']
 if any(n in s for n in ('RobotPresentation.cs','RangedRobotCombat.cs','GameManager.cs','GunController.cs','EnemySignals.cs','MeleeRobotPresentation.cs')):
  (out/f'command-{i:03}.ps1').write_text(s,encoding='utf-8');print(i,c['time'],s[:100].replace('\n',' '))
