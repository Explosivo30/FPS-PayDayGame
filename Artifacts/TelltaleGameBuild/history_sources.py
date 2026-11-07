from recover_inventory import read
import pathlib,json,re
base=pathlib.Path(r'C:/Users/Edgar.DESKTOP-BOONDKL/.codex/sessions')
out=pathlib.Path('Artifacts/TelltaleGameBuild/history');out.mkdir(exist_ok=True)
files=list((base/'2026/10/18').glob('*'))+list((base/'2026/10/27').glob('*'))+list((base/'2026/10/28').glob('*'))+list((base/'2026/09/07').glob('*'))
records=[];inventory={}
for p in files:
 try:data=read(p)
 except Exception as e:print(p.name,str(e));continue
 found=set()
 for line in data.splitlines():
  try:o=json.loads(line)
  except:continue
  q=o.get('payload',{})
  if q.get('type') not in ('function_call','custom_tool_call'):continue
  s=q.get('input',q.get('arguments',''))
  if not any(t in s for t in ('GunController.cs','RangedRobotCombat.cs','EnemySignals.cs','BaseGun.cs','PlayerStateMachine.cs','RewardCard.cs','RobotPresentation.cs','FractureThis.cs','ImpactDetonator.cs','RootPulse.cs','GameManager.cs','TowerSession.cs','EnemyNavigation.cs','EnemyPerception.cs','MeleeRobotCombat.cs')):continue
  if not any(t in s for t in ('apply_patch','Set-Content','write_text','WriteAllText')):continue
  records.append({'time':o.get('timestamp'),'file':p.name,'payload':q});found.update(re.findall(r'[\w.-]+\.cs',s))
 if found:inventory[p.name]=sorted(found)
records.sort(key=lambda x:x['time'] or '')
(out/'calls.json').write_text(json.dumps(records,ensure_ascii=False),encoding='utf-8')
(out/'inventory.json').write_text(json.dumps(inventory,indent=2),encoding='utf-8')
print('calls',len(records));print(json.dumps(inventory,indent=2))
