from pathlib import Path
import base64,json,re
root=Path.cwd()
for folder,newpage in [('Artifacts/EngravingCharacter','comparacion-autonoma.html'),('Artifacts/PrintStyles','comparacion-autonoma.html')]:
 out=root/folder;p=out/'index.html';s=p.read_text(encoding='utf-8-sig')
 if 'const imageSources=' in s:continue
 images={f.name:'data:image/'+('gif' if f.suffix=='.gif' else 'png')+';base64,'+base64.b64encode(f.read_bytes()).decode('ascii') for f in out.iterdir() if f.suffix in ['.png','.gif'] and not f.name.startswith('warmup')}
 s=s.replace('<script>','<script>const imageSources='+json.dumps(images,separators=(',',':'))+';function imageURL(name){if(!imageSources[name])throw new Error("Imagen no incrustada: "+name);return imageSources[name]}\n',1)
 # URLs assembled by JavaScript now resolve to embedded images.
 s=s.replace('${key}-${kind}.png','${imageURL(key+\'-\'+kind+\'.png\')}').replace('${key}-${id}.png','${imageURL(key+\'-\'+id+\'.png\')}')
 for style in ['engraving','original','woodcut','risograph']:
  s=s.replace(".src=key+'-"+style+".png'", ".src=imageURL(key+'-"+style+".png')")
 # Static src/href media also work independently of page JavaScript.
 s=re.sub(r'(src|href)="([^"<>]+\.(?:png|gif))"',lambda m:m.group(1)+'="'+images.get(m.group(2),m.group(2))+'"',s)
 if 'EngravingCharacter' in folder:
  key='view-155';items=[('original','Original'),('risograph','Risograph Fantasy'),('engraving','Grabado de superficie')]
  s=s.replace('<div id="cards" class="cards"></div>','<div id="cards" class="cards">'+''.join('<div class="card"><h3>'+label+'</h3><img alt="'+label+'" src="'+images[key+'-'+style+'.png']+'"></div>' for style,label in items)+'</div>')
  s=s.replace('id="under" alt=', 'id="under" src="'+images[key+'-engraving.png']+'" alt=').replace('id="over" class="over" alt=', 'id="over" class="over" src="'+images[key+'-original.png']+'" alt=')
 else:
  key='RoboticForest-reactive';items=[('original','Original'),('risograph','Risograph'),('woodcut','Grabado inicial')]
  s=s.replace('<div class="grid" id="cards"></div>','<div class="grid" id="cards">'+''.join('<div class="card"><h3>'+label+'</h3><img src="'+images[key+'-'+style+'.png']+'" alt="'+label+'"></div>' for style,label in items)+'</div>')
  s=s.replace('id="under" alt=', 'id="under" src="'+images[key+'-woodcut.png']+'" alt=').replace('id="over" class="over" alt=', 'id="over" class="over" src="'+images[key+'-risograph.png']+'" alt=')
 p.write_text(s,encoding='utf-8');(out/newpage).write_text(s,encoding='utf-8')
 # Extract for browser-independent syntax checking without printing image data.
 (out/'gallery-script.js').write_text(re.search(r'<script>(.*?)</script>',s,re.S).group(1),encoding='utf-8')
 print(folder,': embedded',len(images),'images;',p.stat().st_size,'bytes; static initial view included')
