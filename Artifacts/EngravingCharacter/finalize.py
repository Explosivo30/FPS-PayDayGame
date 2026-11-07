from pathlib import Path
from PIL import Image,ImageDraw,ImageFont,ImageChops,ImageStat
import shutil,json,re,zipfile
root=Path.cwd();out=root/'Artifacts/EngravingCharacter';lab=Path(r'C:\Users\Edgar.DESKTOP-BOONDKL\.codex\visualizations\2026\10\29\01a189e4-ccd6-73d2-a757-94fc2d2924b2\PrintStyleLab')
# Transfer only this task's independent preview assets and preserve their internal GUIDs.
source=lab/'Assets/Rendering/EngravingCharacter'
for f in source.rglob('*'):
 if f.is_file():
  dst=root/'Assets/Rendering/EngravingCharacter'/f.relative_to(source);dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(f,dst)
# Preserve main editor code's portable relative output; transfer its generated meta files only.
for f in (lab/'Assets/Editor/EngravingCharacter').glob('*.meta'):shutil.copy2(f,root/'Assets/Editor/EngravingCharacter'/f.name)
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',22)
small=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',18)
styles=[('original','Original · material del juego'),('risograph','Risograph Fantasy · pantalla'),('engraving','Grabado · material de superficie')]
sheet=Image.new('RGB',(1536,1080),'#161921');d=ImageDraw.Draw(sheet)
d.text((20,12),'MISMO ARIETE / MISMA CAMARA / MISMA LUZ',font=font,fill='#f3e8cc')
for row,key in enumerate(['view-155','detail']):
 for col,(style,label) in enumerate(styles):
  im=Image.open(out/f'{key}-{style}.png').convert('RGB');im=im.resize((492,492),Image.Resampling.LANCZOS)
  x=16+col*508;y=53+row*510;sheet.paste(im,(x,y+24));d.text((x,y),label,font=small,fill='#f3e8cc')
sheet.save(out/'comparison.png')
frames=[]
for i in range(24):
 frame=Image.new('RGB',(1024,550),'#161921');d=ImageDraw.Draw(frame)
 for col,(style,label) in enumerate([styles[0],styles[2]]):
  d.text((14+512*col,9),label,font=small,fill='#f3e8cc')
  im=Image.open(out/f'orbit-{i:02d}-{style}.png').convert('RGB').resize((512,512),Image.Resampling.LANCZOS);frame.paste(im,(512*col,38))
 frames.append(frame)
seq=frames+frames[-2:0:-1]
seq[0].save(out/'orbit-comparison.gif',save_all=True,append_images=seq[1:],duration=90,loop=0,optimize=False)
# Validate renders, real lighting response and material color transfer.
checks={};sizes=[]
for f in out.glob('*.png'):
 im=Image.open(f);im.load();assert max(ImageStat.Stat(im.convert('RGB')).stddev)>4,f.name;sizes.append({'file':f.name,'size':im.size})
for k in ['view-155','detail']:
 a=Image.open(out/f'{k}-original.png').convert('RGB');b=Image.open(out/f'{k}-engraving.png').convert('RGB');checks[k+' difference']=sum(ImageStat.Stat(ImageChops.difference(a,b)).mean)/3/255
 assert checks[k+' difference']>.005
for i in [1,2,3]:
 a=Image.open(out/'light-0-engraving.png').convert('RGB');b=Image.open(out/f'light-{i}-engraving.png').convert('RGB');checks[f'light {i} difference']=sum(ImageStat.Stat(ImageChops.difference(a,b)).mean)/3/255
 assert checks[f'light {i} difference']>.001
checks['validated_png_count']=len(sizes);checks['orbit_views']=24;checks['light_directions']=4
(out/'image-validation.json').write_text(json.dumps(checks,indent=2))
readme='''# Grabado de personaje: revisión de la primera propuesta

El primer Woodcut era un filtro sobre pantalla. No correspondía a la referencia del usuario. Esta revisión es un material de superficie aplicado a una copia de Ariete, con el mismo modelo, cámara, colores base y luz del original.

## Abrir la comparación

index.html: original, risograph Fantasy existente y nuevo grabado; permite elegir ángulo, detalle, mover el deslizador y reproducir una órbita de 24 vistas. comparison.png es la lámina estática. orbit-comparison.gif muestra original y grabado girando, lado a lado.

El risograph se aplica con su shader real y preset RisographFantasy.asset sobre cada render original. El grabado se renderiza directamente desde la malla, no desde esa imagen. No son ilustraciones generadas ni retoques que simulen el efecto.

## Archivos Unity

Assets/Rendering/EngravingCharacter/EngravedInk.shader: color y textura base, cuatro regiones de iluminación, tramas acumulativas y desgaste estable en la superficie. La imagen es 3D y responde a la dirección de la luz.

EngravedOutline.shader: casco invertido de geometría con contorno de ancho variable. Su ancho se expresa a 1080p; la trama interior no usa coordenadas de pantalla.

EngravingCharacterComparison.unity: escena aislada. Abre y entra en Play: 1 Original, 2 Grabado, arrastrar para orbitar, rueda para zoom, Espacio para giro automático, mantener L para mover la luz. La escena no tiene gameplay. Su controlador usa el pipeline de prueba al entrar en Play y restaura el anterior al desactivarse.

EngravingCharacterBuild.cs: Tools > Rendering > Engraving > Generate character comparison. Requiere guardar la escena abierta y disponer de Ariete.fbx, sus materiales y el risograph existente. No modifica los prefabs ni materiales originales. Solo genera los assets de esta prueba.

Los renderers y pipelines habituales del juego no se han sustituido. La nueva escena se entrega para comparar antes de decidir su integración.

## Orientación del rayado

ArtistUV: las líneas siguen el desarrollo UV del personaje; es el modo adecuado para orientar a mano trazos de cara, pelo y ropa por islas UV. HatchTiling configura escala y ángulo. No adivina la anatomía ni detecta mechones y pliegues.

CurvedObject: guía cilíndrica en coordenadas locales para pistones, ejes y piezas redondas. HatchAxis, HatchOrigin y WrapCount definen el flujo. Los giros se cuantizan para mantener la continuidad angular. Las tapas usan proyección plana.

PlanarObject: proyección por cara local para placas. Evita proyectar un cilindro sobre una pechera plana. El modelo de esta prueba usa una selección explícita por pieza. La densidad de los materiales se adapta a la escala de importación del FBX.

Para mallas deformables, ArtistUV sigue la superficie animada. CurvedObject/PlanarObject utilizan la posición local de la malla rígida por defecto; para deformación estable necesitan coordenadas de reposo horneadas en UV3 y activar Use baked rest position. Esta prueba NO valida animación esquelética ni incluye un horneador de UV3. El contorno adicional también debe compartir huesos y pesos al usar SkinnedMeshRenderer. La escena suministrada usa MeshRenderer.

## Qué se ha validado

Unity 6000.0.58f2 / URP 17.0.4 / RTX 3070. Dos shaders nuevos compilados sin errores; render real de 24 vistas, tres encuadres, un detalle y cuatro direcciones de luz. image-validation.json registra diferencias de imagen y respuesta a luz. La compilación se hizo en el proyecto mínimo aislado del estudio anterior, usando los paquetes locales del proyecto, no la escena jugable principal.

## Distancia respecto a la referencia

Es un prototipo técnico corregido sobre un robot angular. No demuestra el acabado de piel, cabello ni tela medieval de la referencia. No se ha modelado ni importado el personaje de la imagen. No hay extracción automática de curvatura principal ni trazos artísticos por anatomía: para acercarse a esa referencia hace falta un personaje orgánico, UV orientadas y dirección de los trazos por material. Tampoco se han medido FPS ni validado lectura durante combate.

Mantiene marfil, naranja y grafito originales; no se ha cambiado el naranja a rojo para simular que se ha conseguido la ilustración. El acabado se concentra en sombras, contornos y tramas adheridas al modelo. Materiales oscuros siguen oscuros: no se ha dibujado un mapa de grabado inverso para pelo.

## Referencias consultadas

- The Stamm, Hatch Shader: https://www.the-stamm.com/wordpress/portfolio/hatch-shader/ — niveles de iluminación con tramas, referencia técnica; no se han reutilizado sus texturas.
- ZLZ, Halftone: https://zlz-studio.github.io/features/Halftone/ — diferencia entre Surface y Screen y filtrado de patrones pequeños. No se ha copiado su implementación.
- Artículo indicado por el usuario (contexto visual): https://80.lv/articles/achieving-surreal-stylized-pencil-drawn-comic-look-for-ue5-environment — su experimento usa postprocesado; aquí la revisión usa material de superficie.
'''
(out/'README.md').write_text(readme,encoding='utf-8');(root/'Assets/Rendering/EngravingCharacter/README.txt').write_text(readme,encoding='utf-8')
print(json.dumps(checks,indent=2))
