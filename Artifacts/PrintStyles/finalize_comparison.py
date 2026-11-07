from pathlib import Path
import shutil,re,json
from PIL import Image,ImageDraw,ImageFont,ImageStat
root=Path.cwd();out=root/'Artifacts/PrintStyles'
lab=Path(r'C:\Users\Edgar.DESKTOP-BOONDKL\.codex\visualizations\2026\10\29\01a189e4-ccd6-73d2-a757-94fc2d2924b2\PrintStyleLab')
# Keep the project GUIDs already copied into the lab. Check before transferring references.
guidmap={} 
for name in ['WoodcutEngraving.shader.meta','WoodcutRendererFeature.cs.meta']:
 old=re.search(r'guid: (\w+)',(lab/'Assets/Art/PrintStyles'/name).read_text()).group(1)
 new=re.search(r'guid: (\w+)',(root/'Assets/Art/PrintStyles'/name).read_text()).group(1)
 guidmap[old]=new
for name in ['Woodcut.mat','Woodcut.mat.meta','Woodcut Renderer.asset','Woodcut Renderer.asset.meta']:
 text=(lab/'Assets/Art/PrintStyles'/name).read_text(encoding='utf-8-sig')
 for old,new in guidmap.items():text=text.replace(old,new)
 (root/'Assets/Art/PrintStyles'/name).write_text(text,encoding='utf-8')
(root/'Assets/Art/PrintStyles/RisographStudy.shader.meta').unlink(missing_ok=True)
# Delete only the temporary preparation script written by this task; the generator stays in Assets.
(out/'prepare_lab.py').unlink(missing_ok=True)
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',22)
small=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',17)
sheet=Image.new('RGB',(1620,800),'#11121a');d=ImageDraw.Draw(sheet)
d.text((24,14),'HELIX / RISOGRAPH FANTASY vs GRABADO DE TINTA',font=font,fill='#f4edde')
keys=['RoboticForest-reactive','CrystalGarden-reactive']
styles=[('original','Original archivado'),('risograph','Risograph · Fantasy'),('woodcut','Woodcut · Engraving')]
report=[]
for row,key in enumerate(keys):
 for col,(kind,label) in enumerate(styles):
  path=out/f'{key}-{kind}.png';im=Image.open(path).convert('RGB');im.load()
  assert min(im.size)>100
  stats=ImageStat.Stat(im);assert max(stats.stddev)>5,'Blank output'
  report.append({'file':path.name,'size':im.size,'stddev':stats.stddev})
  x=20+col*535;y=56+row*365
  d.text((x,y),label,font=small,fill='#e9b681');im.thumbnail((520,310));sheet.paste(im,(x,y+34))
  d.text((x,y+333),'Bosque robotico' if row==0 else 'Jardin de cristal',font=small,fill='#bcb9c9')
sheet.save(out/'comparison.png')
# Validate all gallery inputs, including the two additional scenarios.
for key in ['RoboticForest-reactive','CrystalGarden-reactive','pistol-impact','overview']:
 for kind,_ in styles:
  im=Image.open(out/f'{key}-{kind}.png');im.verify()
(out/'image-checks.json').write_text(json.dumps(report,indent=2))
readme='''# Risograph Fantasy frente a Woodcut / Engraving

Abre index.html en tu navegador. Incluye cuatro escenas, los tres resultados por escena, un deslizador A/B y vistas a tamaño real. comparison.png resume bosque y jardín.

## Qué se entrega

- Shader nuevo: Assets/Art/PrintStyles/WoodcutEngraving.shader y PrintCommon.hlsl.
- Material ajustable: Assets/Art/PrintStyles/Woodcut.mat.
- Integración URP 17 Render Graph: WoodcutRendererFeature.cs. Una pasada al final de la pila de cámaras, después del postprocesado.
- Renderer independiente: Woodcut Renderer.asset. No se ha asignado al pipeline activo.
- Generador: Tools > HELIX > Print styles > Generate comparison.
- Comparador: shader existente Hidden/Torbellino/Risograph, con los parámetros guardados en RisographFantasy.asset, sin sustituir su código. risograph-settings.json registra los ajustes usados.

## Probar en el juego

1. Duplica el renderer que usa la cámara y asígnalo a un pipeline de prueba o añádelo a la lista de renderers del pipeline, seleccionándolo únicamente en la cámara de prueba. El renderer independiente suministrado es una alternativa básica sin las otras features personalizadas del proyecto.
2. En ese renderer de prueba, desactiva Risograph y añade Woodcut Renderer Feature. Asigna Woodcut.mat en Material. No dejes ambos efectos encadenados para comparar.
3. Conserva la configuración original para volver al risograph. No se han modificado los renderers originales ni los perfiles globales.
4. Screen Space Overlay UI queda fuera de esta pasada; armas y UI dibujadas por cámaras sí pueden recibir el efecto.

## Ajustes

Effect strength mezcla con el original; el preset usa 0.85. Line spacing controla el espacio entre líneas, referido a 1080p (valor inicial 6). Paper tint e Ink tint son vectores sRGB; Color stain retiene color original (0.35). Paper grain controla textura estática y Luminance edges el borde por contraste de luminancia. No se solicitan profundidad ni normales adicionales.

Es un shader de pantalla completa: rayado diagonal y cruzado según luminancia, curvatura leve de los surcos, tinta sólida en sombras y papel crema. Los trazos no siguen la geometría o anatomía; ese acabado requeriría datos de superficie y otra técnica. El patrón está anclado a la pantalla. No hay animación aleatoria del papel, pero falta evaluar su comportamiento con cámara en movimiento.

## Opinión artística

Prefiero Risograph Fantasy como dirección principal: violetas, rosas y crema construyen una identidad más distintiva y luminosa. Woodcut vuelve la maquinaria más severa y da buenas masas de sombra, pero el rayado reduce la separación por color y carga la imagen. Lo reservaría como alternativa para una dirección más oscura o para imágenes de presentación. Ninguno resuelve por sí solo la jerarquía visual de enemigos, avisos y objetivos.

## Validación y límites

Los PNG se procesaron con las pasadas HLSL reales sobre cuatro capturas archivadas del proyecto: bosque, jardín, impacto de pistola y fundición. No se han generado con IA ni retocado para simular el shader. Cada tríada parte del mismo fotograma; puede incluir armas y señales ya dibujadas en él. Esas capturas no demuestran el estado actual de las escenas.

Unity 6000.0.58f2, URP 17.0.4, NVIDIA RTX 3070: cero errores de compilación de ambos shaders. Se verificaron doce PNG y sus dimensiones. El proyecto principal presentaba errores C# ajenos al efecto; se compiló y procesó en un proyecto mínimo aislado que usa los paquetes locales de este proyecto. No se han corregido ni alterado esos errores. No hay build nueva, medición de FPS ni partida validada. Quedan pendientes lectura de enemigos y avisos en movimiento, giros rápidos, distintas resoluciones y coste GPU.

Los archivos Unity están también en Fuentes-Unity dentro de esta carpeta. El ZIP incluye galería y fuentes para conservar esta comparación.

Referencia técnica de Unity: https://docs.unity3d.com/6000.0/Documentation/Manual/urp/renderer-features/renderer-feature-full-screen-pass.html
'''
(out/'README.md').write_text(readme,encoding='utf-8')
(root/'Assets/Art/PrintStyles/README.txt').write_text(readme,encoding='utf-8')
# Include importable asset trees, preserving metadata.
for folder in ['Assets/Art/PrintStyles','Assets/Editor/PrintStyles']:
 dst=out/'Fuentes-Unity'/folder;dst.mkdir(parents=True,exist_ok=True)
 for f in (root/folder).iterdir():
  if f.is_file():shutil.copy2(f,dst/f.name)
print('Gallery inputs: 12 PNG passed; source GUIDs match; contact sheet ready.')

