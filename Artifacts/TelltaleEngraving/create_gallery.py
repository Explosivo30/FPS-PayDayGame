from pathlib import Path
from PIL import Image,ImageDraw,ImageFont,ImageChops,ImageStat
import json,re,base64,io
root=Path.cwd();out=root/'Artifacts/TelltaleEngraving';frames=out/'08';art=root/'Assets/Rendering/TelltaleEngraving'
checks=json.loads((out/'checks.json').read_text());assert 'Shader compilation errors: 0' in (frames/'validation.txt').read_text()
colorcheck=[]
def color(t):return [float(x) for x in re.search(r'_BaseColor: \{r: ([\d.e-]+), g: ([\d.e-]+), b: ([\d.e-]+), a: ([\d.e-]+)',t).groups()]
sourcecolors={f.stem:color(f.read_text()) for f in (root/'Assets/Art/Veterans/Materials').glob('*.mat')}
for f in (art/'Materials').glob('Part-*.mat'):
 c=color(f.read_text());matches=[name for name,source in sourcecolors.items() if max(abs(a-b) for a,b in zip(c,source))<1e-5];assert matches,f;colorcheck.append(matches[0])
assert len(colorcheck)==6
pngs=list(frames.glob('*.png'))
for f in pngs:
 im=Image.open(f).convert('RGB');im.load();assert max(ImageStat.Stat(im).stddev)>2,f
 # Detect the standard shader-error magenta render.
 assert sum(1 for r,g,b in im.resize((50,50)).getdata() if r>230 and b>230 and g<30)<300,f
checks['source_colors_preserved']=colorcheck;checks['validated_images']=len(pngs);(out/'checks.json').write_text(json.dumps(checks,indent=2))
validation='''# Validación realizada

Unity 6000.0.58f2 / URP 17.0.4 / NVIDIA RTX 3070, proyecto mínimo aislado con los paquetes locales del proyecto y el modelo Ariete real. El proyecto de gameplay completo no se ha recompilado en esta tarea.

- C# del sistema y shaders: la ejecución final terminó con código 0; cero errores de shader. Los mensajes ajenos de VisualStudioEditor sobre la ruta del editor externo no son fallos del shader; se conservan en el log.
- Modelo real: baseline original y material nuevo, mismas posiciones de cámara, geometría y luces en cada pareja.
- Colores: los seis valores BaseColor del modelo original se conservaron en los materiales nuevos. No hay una paleta crema/rojo obligatoria.
- Etapas: iluminación, rayado simple, cruce, contorno y grano generaron sus capturas y se inspeccionaron antes de añadir la siguiente etapa. Las capturas de etapas iniciales son el historial del estudio; el combinado final incluye el ajuste posterior de filtrado.
- 24 vistas de cámara; cuatro direcciones de luz; iluminación tenue y fuerte; distintos colores y configuraciones sobre esferas.
- Luz adicional: el render con punto difiere del render sin punto; Forward y Forward+ coinciden en este fixture.
- Trama: el render con hatching difiere del render sin hatching; el primer render de calibración y su repetición son iguales con cámara y luz fijas.
- Huesos: la malla de prueba SkinnedMeshRenderer produce imágenes distintas en reposo y con el hueso girado.
- Blendshape: se renderizó una deformación diferente y el compañero sincronizó su peso. Esto es un test sintético, no una actuación facial humana.
- Solapamiento: dos mallas skinned y sus cascos se renderizaron superpuestas; se revisó su profundidad visual.
- Distancia: se renderizó el fixture cerca y lejos, con y sin contorno.

checks.json contiene las diferencias numéricas. No se usa la diferencia de imagen como prueba de calidad artística, ausencia de parpadeo en cualquier caso o diversión.

## Pendiente

Rostro y cabello humanos, emociones faciales, tela de un personaje real, UV orientadas artísticamente, personajes con LOD, varias animaciones de producción, combate, GPU de gama baja y medición de coste GPU. No hay una nueva build jugable ni una garantía de rendimiento comercial. La adaptación completa del mundo a esta dirección artística no forma parte de este shader independiente.
'''
(out/'VALIDATION.md').write_text(validation,encoding='utf-8');(art/'VALIDATION.md').write_text(validation,encoding='utf-8')
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',22)
sheet=Image.new('RGB',(1560,1120),'#141922');d=ImageDraw.Draw(sheet)
for col,(folder,suffix,label) in enumerate([(frames,'original','Original'),(root/'Artifacts/WoodcutRelief','engraving','Woodcut Relief anterior'),(frames,'engraving','Telltale Engraving nuevo')]):
 for row,key in enumerate(['view-155','detail']):
  x=col*520+10;y=row*550+12;d.text((x,y),label,font=font,fill='#eee6da');im=Image.open(folder/f'{key}-{suffix}.png').convert('RGB').resize((500,500),Image.Resampling.LANCZOS);sheet.paste(im,(x,y+36))
sheet.save(out/'comparison.png')
def data(path):
 im=Image.open(path).convert('RGB');buf=io.BytesIO();im.save(buf,'WEBP',lossless=True,method=4);return 'data:image/webp;base64,'+base64.b64encode(buf.getvalue()).decode()
images={}
for key in ['view-155','view-180','view-210','detail']+[f'light-{i}' for i in range(4)]+[f'orbit-{i:02d}' for i in range(24)]:
 for suffix in ['original','engraving']:images[key+'-'+suffix]=data(frames/f'{key}-{suffix}.png')
for key in ['presets-colors','presets-backlight','presets-dark','presets-bright','cloth-close','cloth-close-no-hatch','skinned-rest','skinned-bent','skinned-blendshape','skinned-overlap','skinned-far','forward-point','forwardplus-point']:images[key]=data(frames/(key+'.png'))
html='''<!doctype html><html lang="es"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Telltale Engraving · material 3D</title><style>:root{color-scheme:dark;font:16px system-ui;background:#141922;color:#ede9df}body{max-width:1400px;margin:auto;padding:30px}h1{font-size:42px;margin:12px 0}p{line-height:1.6;max-width:1000px;color:#bcc4ce}a{color:#edb786}button,select{padding:9px 12px;font:inherit;color:inherit;background:#2a3341;border:1px solid #586575;border-radius:7px}nav{display:flex;gap:16px;flex-wrap:wrap;align-items:center;margin:24px 0}.cards{display:grid;grid-template-columns:1fr 1fr;gap:18px}.cards img{width:100%}.stage{position:relative;max-width:1000px;margin:auto;line-height:0}.stage img{width:100%;display:block}.stage .over{position:absolute;inset:0;clip-path:inset(0 50% 0 0)}input{width:100%}section{margin:36px 0}.tag{letter-spacing:.15em;font-size:12px;color:#9bb8cf}details img{width:100%}@media(max-width:760px){.cards{grid-template-columns:1fr}body{padding:16px}}</style><div class="tag">NUEVO SISTEMA / MATERIAL DE PERSONAJE</div><h1>Telltale Engraving</h1><p>Más novela gráfica, colores originales y grabado discreto en sombras. El shader se aplica directamente al modelo 3D. No usa postprocesado ni filtros de cámara. Esta es una versión separada de Woodcut Relief.</p>
<nav><label>Vista <select id="view"><option value="view-155">Tres cuartos</option><option value="view-180">Frontal</option><option value="view-210">Otro lado</option><option value="detail">Detalle</option><option value="light-0">Luz 1</option><option value="light-1">Luz 2</option><option value="light-2">Luz 3</option><option value="light-3">Luz 4</option></select></label><button id="play">Girar cámara</button><span id="frame"></span><a href="README.md">Guía</a><a href="TelltaleEngraving.zip">Fuentes y escenas</a></nav>
<div class="cards"><div><h3>Original</h3><img id="original" src="ORIGINAL" alt="Original"></div><div><h3>Telltale Engraving</h3><img id="engraving" src="NEW" alt="Telltale"></div></div>
<section><h2>Activar el material sobre el mismo personaje</h2><p>Izquierda: original. Derecha: nuevo material. Misma geometría, cámara y luz en cada pareja.</p><div class="stage"><img id="under" src="NEW" alt="Nuevo material"><img id="over" class="over" src="ORIGINAL" alt="Original"></div><input id="wipe" type="range" min="0" max="100" value="50" aria-label="Comparación del shader"></section>
<details><summary>Comparar también con Woodcut Relief</summary><p>La columna Woodcut Relief es la captura archivada del estudio anterior; no es una repetición con el mismo entorno de iluminación del ensayo nuevo.</p><img src="SHEET" alt="Original, Woodcut Relief anterior y Telltale nuevo"></details>
<section><h2>Presets y pruebas</h2><p>Skin, Hair, Cloth, Armor, Accessory y Low son configuraciones independientes. La calibración de color usa esferas, no personajes humanos.</p><nav><select id="fixture"><option value="presets-colors">Colores y presets</option><option value="cloth-close">Trama en sombra · cerca</option><option value="cloth-close-no-hatch">Misma vista · sin trama</option><option value="presets-backlight">Contraluz</option><option value="presets-dark">Luz tenue</option><option value="presets-bright">Luz fuerte</option><option value="skinned-rest">Malla con huesos · reposo</option><option value="skinned-bent">Malla con huesos · deformada</option><option value="skinned-blendshape">Deformación de blendshape</option><option value="skinned-overlap">Solapamiento</option><option value="skinned-far">Distancia</option><option value="forward-point">Forward · luz adicional</option><option value="forwardplus-point">Forward+ · misma luz</option></select></nav><img id="fixtureImage" style="width:100%" src="FIXTURE" alt="Fixture de validación"></section>
<section><h2>Qué está entregado</h2><p>Shader principal modular, atlas de trama, contorno geométrico, presets, escena del personaje y escena de huesos/blendshape. Materiales y renderers originales del proyecto conservados. Shader y C# compilados en Unity 6000.0.58f2 / URP 17.0.4; renders realizados en RTX 3070.</p><p>Escena: Assets/Rendering/TelltaleEngraving/TelltaleComparison.unity. En Play: 1 original, 2 nuevo; ratón para orbitar, rueda para zoom, Espacio para giro, L para luz. El menú puede crear una copia de otro personaje con materiales nuevos.</p><h2>Qué falta validar</h2><p>El personaje real probado es Ariete. La prueba de animación usa una malla sintética con dos huesos y un blendshape. No se han validado un rostro humano, pelo, tela real, combate ni tiempos GPU. Las UV deben orientarse según las formas; no existe detección automática de anatomía. El resultado artístico queda para tu revisión.</p><p><a href="VALIDATION.md">Validación y límites</a> · <a href="TECHNICAL.md">Arquitectura</a> · <a href="PERFORMANCE.md">Coste y configuración reducida</a></p></section>
<script>const images=DATA;let timer,index=0;function show(key){for(const id of ['original','engraving'])document.getElementById(id).src=images[key+'-'+id];document.querySelector('#under').src=images[key+'-engraving'];document.querySelector('#over').src=images[key+'-original']}document.querySelector('#view').onchange=e=>{clearInterval(timer);timer=null;document.querySelector('#play').textContent='Girar cámara';show(e.target.value)};document.querySelector('#wipe').oninput=e=>document.querySelector('#over').style.clipPath=`inset(0 ${100-e.target.value}% 0 0)`;document.querySelector('#fixture').onchange=e=>document.querySelector('#fixtureImage').src=images[e.target.value];document.querySelector('#play').onclick=()=>{if(timer){clearInterval(timer);timer=null;document.querySelector('#play').textContent='Girar cámara';return}document.querySelector('#play').textContent='Pausar';timer=setInterval(()=>{index=(index+1)%24;show('orbit-'+String(index).padStart(2,'0'));document.querySelector('#frame').textContent=(index+1)+' / 24'},150)};</script></html>'''
replacements={'ORIGINAL':images['view-155-original'],'NEW':images['view-155-engraving'],'SHEET':data(out/'comparison.png'),'FIXTURE':images['presets-colors'],'DATA':json.dumps(images,separators=(',',':'))}
html=re.sub(r'\b(ORIGINAL|NEW|SHEET|FIXTURE|DATA)\b',lambda m:replacements[m.group(0)],html)
(out/'index.html').write_text(html,encoding='utf-8');print(json.dumps({'images':len(pngs),'colors':colorcheck,'page_bytes':len(html.encode())}))
