from pathlib import Path
from PIL import Image,ImageChops,ImageStat,ImageDraw,ImageFont
import shutil,json,re,math
root=Path.cwd();out=root/'Artifacts/RisographGame';art=root/'Assets/Rendering/RisographGame';lab=Path(r'C:\Users\Edgar.DESKTOP-BOONDKL\.codex\visualizations\2026\10\29\01a189e4-ccd6-73d2-a757-94fc2d2924b2\PrintStyleLab')
for f in (lab/'Assets/Rendering/RisographGame').rglob('*'):
 if f.is_file():
  target=art/f.relative_to(lab/'Assets/Rendering/RisographGame');target.parent.mkdir(parents=True,exist_ok=True)
  if f.suffix=='.cs' and target.exists():continue
  if f.name=='README.md':continue
  shutil.copy2(f,target)
assert 'New game Risograph shader errors: 0' in (out/'validation.txt').read_text()
checks={'shader_errors':0,'fresh_character_views':17,'archived_fps_frames':3}
for key in ['fps-RoboticForest','fps-LaboratoryFloor','fps-CrystalGarden','character-155']:
 for style in ['riso-old','riso-game']:
  d=ImageChops.difference(Image.open(out/f'{key}-original.png').convert('RGB'),Image.open(out/f'{key}-{style}.png').convert('RGB'));value=sum(ImageStat.Stat(d).mean)/3;assert value>2;checks[key+' '+style+' mean_delta_255']=value
assert ImageChops.difference(Image.open(out/'fps-RoboticForest-riso-game.png'),Image.open(out/'repeat-riso-game.png')).getbbox() is None;checks['fixed_input_repeat_identical']=True
colors={}
for mode,file in [('source','signals-source.png'),('fantasy','signals-riso-old.png'),('game','signals-riso-game.png')]:
 im=Image.open(out/file).convert('RGB');colors[mode]=[ImageStat.Stat(im.crop((i*160+35,110,i*160+125,240))).mean for i in range(8)]
checks['signal_chart_distances_game_rgb']=[math.dist(colors['game'][i],colors['game'][j]) for i,j in [(0,1),(1,2),(0,2)]];assert min(checks['signal_chart_distances_game_rgb'])>35
checks['signal_chart_error_source_game']=[math.dist(a,b) for a,b in zip(colors['source'][:3],colors['game'][:3])];checks['signal_chart_error_source_fantasy']=[math.dist(a,b) for a,b in zip(colors['source'][:3],colors['fantasy'][:3])]
for a,b in zip(checks['signal_chart_error_source_game'],checks['signal_chart_error_source_fantasy']):assert a<b
for f in out.glob('*.png'):
 im=Image.open(f).convert('RGB');im.load();assert max(ImageStat.Stat(im).stddev)>3
 assert sum(1 for r,g,b in im.resize((40,40)).getdata() if r>230 and b>230 and g<30)<200
(out/'checks.json').write_text(json.dumps(checks,indent=2));(out/'color-checks.json').write_text(json.dumps(colors,indent=2))
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',23)
sheet=Image.new('RGB',(1680,1220),'#121a22');d=ImageDraw.Draw(sheet)
for col,(style,title) in enumerate([('riso-old','Risograph Fantasy · anterior'),('riso-game','Risograph Game · HELIX'),('telltale','Telltale Engraving')]):
 for row,key in enumerate(['character-155','detail']):
  x=col*560+10;y=row*600+12;d.text((x,y),title,font=font,fill='#f1e5cc');im=Image.open(out/f'{key}-{style}.png').convert('RGB').resize((540,540),Image.Resampling.LANCZOS);sheet.paste(im,(x,y+38))
sheet.save(out/'comparison-characters.png')
sheet=Image.new('RGB',(1800,1155),'#121a22');d=ImageDraw.Draw(sheet)
for row,level in enumerate(['RoboticForest','LaboratoryFloor','CrystalGarden']):
 for col,(style,title) in enumerate([('original','Original archivado'),('riso-old','Risograph Fantasy'),('riso-game','Risograph Game HELIX')]):
  x=col*600+8;y=row*382+8;d.text((x,y),title,font=font,fill='#f1e5cc');im=Image.open(out/f'fps-{level}-{style}.png').convert('RGB').resize((584,329),Image.Resampling.LANCZOS);sheet.paste(im,(x,y+35))
sheet.save(out/'comparison-fps.png')
readme='''# Risograph Game — adaptación a HELIX y comparación

Se han usado dos agentes: uno auditó paleta y funciones de color; otro implementó el shader independiente. El agente principal corrigió la importación ShaderLab, integró la pasada de cámara, renderizó, revisó la comparación y verificó los resultados. La segunda revisión detectó un baseline Fantasy que había perdido sus uniformes tras guardar assets; el generador ahora los reaplica antes de cada dibujo. Los archivos entregados se volvieron a renderizar con el Fantasy real activo.

## Abrir

Artifacts/RisographGame/index.html: todas las imágenes están incrustadas. Se puede elegir cámara, vista y estilos, mover la separación y girar el personaje. comparison-characters.png compara Fantasy, Game y Telltale. comparison-fps.png compara original, Fantasy y Game en tres capturas del juego.

En Unity: Assets/Rendering/RisographGame/GameStyleComparison.unity, Play. 1 Original, 2 Risograph Game, 3 Telltale, 4 Woodcut Relief. Arrastrar para orbitar, rueda para zoom, Espacio para giro y L para luz. El visor selecciona temporalmente los pipelines de prueba y restaura los anteriores al desactivarse.

## Cambios para este juego

- Índigo oscuro, teal de máquina, cobre y papel crema, partiendo de los materiales del juego.
- Recuperación de medios .24 y papel .16, frente a .72 y .55 en Fantasy: se conservan más diferencias entre planos.
- Grano .018, desgaste .002 y desfase cero. Fantasy utilizaba .065, .012 y .45 respectivamente.
- Retención de color .28 y forma tonal .30; recuperación adicional del original en bordes y colores saturados luminosos.
- La carta de colores derivados de RootPulse conserva separados menta, ámbar y rojo/naranja en este caso. El test es sintético, con radiancia recortada a SDR y sin bloom; no es una prueba de reacción del jugador.

La variante sigue siendo visiblemente risograph. Las retenciones generan tonos adicionales: no pretende limitar matemáticamente la salida a cuatro colores exactos.

## Shader y material

Shader: Hidden/Torbellino/RisographGame, archivo RisographGame.shader. Material editable: Materials/HELIX Game.mat. Las tintas son vectores sRGB; para scripts usar SetVector. Todos los controles están en el Inspector.

Para probar en otro renderer, duplicarlo, añadir Risograph Game Renderer Feature y asignar el material. Desactivar el Risograph anterior en ese renderer para evitar encadenar ambos. No se han reemplazado los renderers originales ni los perfiles globales. El feature nuevo procesa una vez al resolver el destino final de la pila de cámaras, después del postprocesado, sin necesitar profundidad ni normales. No se ha ensayado una partida real de dos cámaras con esta versión.

La Screen Space Overlay UI queda fuera de la pasada por su orden de dibujo habitual. La UI renderizada por cámara puede recibir el filtro; no existe una identificación semántica de interfaz. El visor usa OnGUI sobre el render y la comparación de personaje no tiene HUD.

## Diferencia entre técnicas

Risograph Fantasy y Game son efectos de cámara. Telltale Engraving y Woodcut Relief son materiales de superficie sobre el modelo 3D. No se ha simulado Telltale o Woodcut aplicando un filtro a un PNG.

Los personajes se han renderizado de nuevo con idéntica geometría, cámara, luz y ambiente para cada estilo. La columna Game usa el feature real de cámara; Fantasy se procesa con su shader real y preset guardado sobre el fotograma original exacto. La escena interactiva ofrece los cuatro modos nuevos/original/materiales; Fantasy está disponible en la página como referencia.

Las vistas FPS proceden de capturas archivadas del 21/10/2026 (bosque, laboratorio y jardín, con armas y sin HUD). Sobre ellas solo se comparan original y los dos risographs. No representan una partida nueva ni certifican el estado actual de esas escenas.

## Qué está verificado

Unity 6000.0.58f2 / URP 17.0.4 / RTX 3070: shaders compilados sin errores; render de personaje desde tres ángulos, detalle, 12 posiciones de cámara y contraluz; tres entradas FPS reales archivadas; carta sintética; repetición del mismo fotograma idéntica. checks.json registra diferencias y colores medios. La validación se realizó en el proyecto mínimo aislado usado para los estudios anteriores, con los paquetes y assets locales del juego. No se recompiló el proyecto jugable completo.

## Límites y coste

La conservación de color es una heurística RGB. No sabe qué es enemigo, peligro, interacción o emisión; puede conservar pintura decorativa y fallar con avisos oscuros. Requiere revisar los avisos reales durante una partida. No hay una garantía de accesibilidad cromática ni de lectura en combate.

La trama está fijada a la pantalla. No tiene la adherencia a la malla de Telltale/Woodcut. La textura de impresión puede moverse sobre la geometría al girar; hay que evaluarlo jugando. No se han medido FPS ni tiempos GPU.

Lecturas por fragmento en el código: una de color; cuatro vecinas si Edge Preservation está activo; una adicional si Registration es mayor que cero. El preset usa cinco lecturas y cero desfase. No se convierte este recuento en una promesa de rendimiento.
'''
(out/'README.md').write_text(readme,encoding='utf-8');(art/'GUIDE.md').write_text(readme,encoding='utf-8')
agentreadme=(art/'README.md').read_text(encoding='utf-8-sig').replace('El shader aún requiere compilación, render y revisión visual por el agente principal.','El shader fue compilado y renderizado por el agente principal; GUIDE.md contiene la validación y los límites.').replace('Asignar a un FullScreenPassRendererFeature independiente, después del postprocesado, Fetch Color Buffer activado, pass 0. En una pila de cámaras debe ejecutarse una sola vez al resolver el target final. Un feature genérico instalado en cada cámara podría procesar más de una vez: la integración debe gestionar la pila.','Se entrega RisographGameRendererFeature, que utiliza el material después del postprocesado y solo al resolver el destino final de la pila. Consultar GUIDE.md para probarlo sin alterar los renderers originales.')
(art/'README.md').write_text(agentreadme,encoding='utf-8');print(json.dumps(checks,indent=2))
