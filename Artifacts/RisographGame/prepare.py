from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import math,shutil,json
root=Path.cwd();out=root/'Artifacts/RisographGame'
colors=[('Menta reposo',(.03,.45,.25)),('Ambar aviso',(2,1,.02)),('Rojo activo',(3,.25,.02)),('Jade',(.075,.42,.29)),('Cobre',(.7,.28,.105)),('Marfil',(.65,.77,.65)),('Coral',(.95,.19,.39)),('Violeta',(.36,.22,.7))]
def srgb(v):v=min(max(v,0),1);return int(round(255*(12.92*v if v<=.0031308 else 1.055*v**(1/2.4)-.055)))
im=Image.new('RGB',(1280,360),'#151a22');d=ImageDraw.Draw(im);font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',18)
for i,(name,c) in enumerate(colors):
 d.rectangle((i*160+8,50,i*160+151,305),fill=tuple(map(srgb,c)));d.text((i*160+8,18),name,font=font,fill='#fff2d6')
im.save(out/'signals-source.png');(out/'signal-source-colors.json').write_text(json.dumps(colors,indent=2))
# Serialize material properties rather than merely the object identity.
p=root/'Assets/Rendering/RisographGame/Editor/RisographGameComparison.cs';s=p.read_text(encoding='utf-8-sig').replace('JsonUtility.ToJson(game,true)','EditorJsonUtility.ToJson(game,true)');p.write_text(s,encoding='utf-8')
lab=Path(r'C:\Users\Edgar.DESKTOP-BOONDKL\.codex\visualizations\2026\10\29\01a189e4-ccd6-73d2-a757-94fc2d2924b2\PrintStyleLab')
for f in (root/'Assets/Rendering/RisographGame').rglob('*'):
 if f.is_file():
  dest=lab/f.relative_to(root);dest.parent.mkdir(parents=True,exist_ok=True)
  if f.suffix=='.cs' and f.parent.name=='Editor':dest.write_text(f.read_text(encoding='utf-8-sig').replace('"Artifacts/RisographGame"','"D:/Unity/FPS-PayDayGame/Artifacts/RisographGame"').replace('"Artifacts/Gameplay-','"D:/Unity/FPS-PayDayGame/Artifacts/Gameplay-'),encoding='utf-8')
  else:shutil.copy2(f,dest)
