from pathlib import Path
from PIL import Image,ImageDraw,ImageFont,ImageChops,ImageStat
import base64,io,json,shutil,re,zipfile
root=Path.cwd();out=root/'Artifacts/WoodcutRelief';old=root/'Artifacts/EngravingCharacter'
lab=Path(r'C:\Users\Edgar.DESKTOP-BOONDKL\.codex\visualizations\2026\10\29\01a189e4-ccd6-73d2-a757-94fc2d2924b2\PrintStyleLab')
for f in (lab/'Assets/Rendering/WoodcutRelief').rglob('*'):
 if f.is_file():
  dst=root/'Assets/Rendering/WoodcutRelief'/f.relative_to(lab/'Assets/Rendering/WoodcutRelief');dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(f,dst)
for f in (lab/'Assets/Editor/WoodcutRelief').glob('*.meta'):shutil.copy2(f,root/'Assets/Editor/WoodcutRelief'/f.name)
ref=Path(r'C:\Users\EDGAR~1.DES\AppData\Local\Temp\codex-clipboard-6a2a01b0-91b5-4916-9a5b-42d269e613ba.png');shutil.copy2(ref,out/'reference.png')
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',24)
sheet=Image.new('RGB',(1440,1460),'#171a20');d=ImageDraw.Draw(sheet)
for col,(folder,title) in enumerate([(old,'ANTERIOR · lineas oscuras'),(out,'NUEVO · tinta y tallado en claro')]):
 x=col*720;d.text((x+16,12),title,font=font,fill='#f3dec2')
 im=Image.open(folder/'view-155-engraving.png').convert('RGB').resize((704,704),Image.Resampling.LANCZOS);sheet.paste(im,(x+8,50))
 im=Image.open(folder/'detail-engraving.png').convert('RGB').resize((704,704),Image.Resampling.LANCZOS);sheet.paste(im,(x+8,756))
sheet.save(out/'comparison.png')
def url(path):
 im=Image.open(path).convert('RGB');buf=io.BytesIO();im.save(buf,format='WEBP',lossless=True,method=4);return 'data:image/webp;base64,'+base64.b64encode(buf.getvalue()).decode()
keys=['view-155','view-180','view-210','detail']+[f'light-{i}' for i in range(4)]+[f'orbit-{i:02d}' for i in range(24)]
images={}
for key in keys:
 for name,folder in [('previous',old),('relief',out)]:images[key+'-'+name]=url(folder/f'{key}-engraving.png')
for key in keys[:4]:images[key+'-original']=url(out/f'{key}-original.png')
refurl=url(out/'reference.png')
html='''<!doctype html><html lang="es"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Woodcut Relief · shader nuevo</title><style>:root{font:16px system-ui;color-scheme:dark;background:#171a20;color:#f3e6d0}body{max-width:1400px;margin:auto;padding:28px}h1{font-size:42px}p{line-height:1.6;max-width:1000px;color:#c6c0bc}button,select{font:inherit;padding:10px;background:#30343b;color:inherit;border:1px solid #666;border-radius:6px}nav{display:flex;gap:16px;align-items:center;flex-wrap:wrap;margin:24px 0}.cards{display:grid;grid-template-columns:1fr 1fr;gap:16px}.cards img{width:100%}.stage{position:relative;line-height:0;max-width:1000px;margin:auto}.stage img{width:100%;display:block}.stage .over{position:absolute;inset:0;clip-path:inset(0 50% 0 0)}input{width:100%}.reference{max-width:950px;width:100%}a{color:#f0b082}section{margin:36px 0}@media(max-width:800px){.cards{grid-template-columns:1fr}body{padding:16px}}</style>
<h1>Woodcut Relief</h1><p>Shader nuevo de material: masas de tinta azul oscura, cortes claros dentro de las sombras, surcos con extremos afinados, desgaste irregular y un preset de crema y rojo teja. La trama permanece en la superficie del modelo.</p><details><summary>Ver la referencia que has pasado</summary><img class="reference" src="REFURL" alt="Referencia del usuario"></details>
<nav><label>Vista <select id="view"><option value="view-155">Tres cuartos</option><option value="view-180">Frontal</option><option value="view-210">Otro lado</option><option value="detail">Detalle</option><option value="light-0">Luz 1</option><option value="light-1">Luz 2</option><option value="light-2">Luz 3</option><option value="light-3">Luz 4</option></select></label><button id="play">Girar modelo</button><span id="frame"></span></nav><div class="cards"><div><h3>Shader anterior</h3><img id="previous" src="BEFORE" alt="Shader anterior"></div><div><h3>Woodcut Relief nuevo</h3><img id="relief" src="AFTER" alt="Nuevo shader"></div></div>
<section><h2>Comparar sobre el mismo modelo</h2><p>Izquierda: anterior. Derecha: nuevo. Misma geometría, cámara y luz.</p><div class="stage"><img id="under" src="AFTER" alt="Nuevo"><img id="over" class="over" src="BEFORE" alt="Anterior"></div><input id="wipe" type="range" min="0" max="100" value="50" aria-label="Separacion de shaders"></section>
<section><h2>Qué cambia</h2><p>El anterior dibujaba líneas oscuras sobre casi todos los materiales. Este usa tres operaciones de impresión: deja papel y color en luces, acumula tinta en medios y sombras, y abre trazos claros en las zonas oscuras. El desgaste y el ancho de los surcos varían sobre la superficie.</p><p>El preset remapea los tonos cálidos hacia rojo teja. Palette Strength permite recuperar los colores originales. Ink Strength, Carving, Gouge Width, Print Wear y Density regulan las masas de tinta y el tallado.</p><h2>Hasta dónde llega esta prueba</h2><p>El resultado sigue siendo Ariete, un robot de placas. No reproduce la anatomía, el pelo ni los pliegues del personaje de referencia. El shader ofrece UV orientadas o guías locales por pieza; no calcula automáticamente el flujo anatómico. El parecido artístico queda por valorar contigo.</p><p>Compilado y renderizado en Unity 6000.0.58f2 / URP 17 con RTX 3070, en el proyecto aislado de pruebas. Sin errores de shader. Se han generado varias vistas y direcciones de luz. No hay validación de combate, FPS ni animación esquelética.</p><p>Escena editable: Assets/Rendering/WoodcutRelief/WoodcutReliefComparison.unity. En Play: 1 original, 2 shader nuevo, ratón para orbitar, rueda para zoom. No se han sustituido materiales ni renderers del juego.</p><a href="README.txt">Instrucciones</a></section>
<script>const images=IMAGEDATA;let index=0,timer;function show(key){for(const name of ['previous','relief'])document.getElementById(name).src=images[key+'-'+name];document.querySelector('#under').src=images[key+'-relief'];document.querySelector('#over').src=images[key+'-previous']}document.querySelector('#view').onchange=e=>{clearInterval(timer);timer=null;document.querySelector('#play').textContent='Girar modelo';show(e.target.value)};document.querySelector('#wipe').oninput=e=>document.querySelector('#over').style.clipPath=`inset(0 ${100-e.target.value}% 0 0)`;document.querySelector('#play').onclick=()=>{if(timer){clearInterval(timer);timer=null;document.querySelector('#play').textContent='Girar modelo';return}document.querySelector('#play').textContent='Pausar';timer=setInterval(()=>{index=(index+1)%24;show('orbit-'+String(index).padStart(2,'0'));document.querySelector('#frame').textContent=(index+1)+' / 24'},130)};</script></html>'''
html=html.replace('REFURL',refurl).replace('BEFORE',images['view-155-previous']).replace('AFTER',images['view-155-relief']).replace('IMAGEDATA',json.dumps(images,separators=(',',':')))
(out/'index.html').write_text(html,encoding='utf-8')
readme='''WOODCUT RELIEF — shader nuevo

Abrir index.html: imagenes incrustadas, anterior/nuevo, detalle, luces y giro. La referencia del usuario se incluye en reference.png.

Assets/Rendering/WoodcutRelief/WoodcutRelief.shader es un material nuevo. No modifica el shader anterior. El preset usa tinta azul oscura, papel crema y rojo teja. PaletteStrength=0 conserva los colores base. InkStrength controla tinta; Carving abre surcos claros en las zonas oscuras. Gouge Width, Broken Ink y Print Wear controlan marcas e imperfecciones. Density se expresa en unidades locales de la malla: los materiales generados compensan la escala de importacion del FBX.

ArtistUV permite orientar las lineas con las islas UV. CurvedObject usa coordenadas cilindricas locales por pieza; PlanarObject se usa en placas. No existe extraccion automatica de curvatura anatomica. Las marcas tienen longitud y ancho variables; aparecen por niveles de luz y por pigmento oscuro. En mallas deformables debe usarse ArtistUV o coordenadas de reposo horneadas. Esta prueba valida mallas rigidas; no piel, pelo, tela ni animacion esqueletica.

Escena: Assets/Rendering/WoodcutRelief/WoodcutReliefComparison.unity. En Play, 1 original / 2 nuevo. Ratón para orbitar; rueda para zoom; Espacio para giro; L para mover la luz. El visor comparte el controlador y contorno del estudio EngravingCharacter. Usa temporalmente el pipeline de prueba y lo restaura al desactivarse. No es una nueva escena de gameplay.

Generar de nuevo: Tools > Rendering > Engraving > Generate woodcut relief comparison. Guarda antes la escena abierta. Solo escribe esta carpeta de prueba y sus renders.

Validacion: shader compilado sin errores y renders de 24 posiciones de camara y 4 direcciones de luz, Unity 6000.0.58f2 / URP 17.0.4 / RTX 3070 en el laboratorio aislado. No hay pruebas de FPS, partida ni deformacion esqueletica. La semejanza con la referencia es una decision artistica pendiente de revision; el robot no sustituye al personaje organico de la imagen.
'''
(out/'README.txt').write_text(readme,encoding='utf-8');(root/'Assets/Rendering/WoodcutRelief/README.txt').write_text(readme,encoding='utf-8')
checks={}
for key in keys:
 im=Image.open(out/f'{key}-engraving.png');im.load();assert max(ImageStat.Stat(im).stddev)>5
checks['view_count']=24;checks['shader_errors']=0;checks['html_bytes']=(out/'index.html').stat().st_size
(out/'image-checks.json').write_text(json.dumps(checks,indent=2));print(checks)
