from recover_inventory import read
import pathlib,json
base=pathlib.Path(r'C:/Users/Edgar.DESKTOP-BOONDKL/.codex/sessions/2026/10')
matches=[]
for p in base.rglob('*.jsonl*'):
 if p.stat().st_size<700000:continue
 if '/18/' in p.as_posix() or '/27/' in p.as_posix() or '/28/' in p.as_posix() or '/30/' in p.as_posix():continue
 data=read(p)
 # Scan only sessions with source integration evidence for the missing API.
 if 'ShotSequence' not in data and 'BindRig' not in data:continue
 calls=[]
 for line in data.splitlines():
  try:o=json.loads(line)
  except:continue
  q=o.get('payload',{})
  if q.get('type') not in ('function_call','custom_tool_call'):continue
  s=q.get('input',q.get('arguments',''))
  if any(t in s for t in ('apply_patch','Set-Content','write_text','WriteAllText')) and any(t in s for t in ('ShotSequence','BindRig','RewardKind','IsTargetLocked','DeathPresentationOverride','HasVeteranModel','AimDirection')):
   calls.append({'time':o.get('timestamp'),'file':p.name,'payload':q})
 if calls:
  matches+=calls;print(p.name,len(calls),flush=True)
pathlib.Path('Artifacts/TelltaleGameBuild/history/extra-calls.json').write_text(json.dumps(matches,ensure_ascii=False),encoding='utf-8')
