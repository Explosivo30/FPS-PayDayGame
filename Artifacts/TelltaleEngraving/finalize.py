from pathlib import Path
import shutil,re,json
root=Path.cwd();art=root/'Assets/Rendering/TelltaleEngraving';out=root/'Artifacts/TelltaleEngraving'
lab=Path(r'C:\Users\Edgar.DESKTOP-BOONDKL\.codex\visualizations\2026\10\29\01a189e4-ccd6-73d2-a757-94fc2d2924b2\PrintStyleLab')
for f in (lab/'Assets/Rendering/TelltaleEngraving').rglob('*'):
 if not f.is_file():continue
 rel=f.relative_to(lab/'Assets/Rendering/TelltaleEngraving')
 m=re.match(r'Materials[\\/]Part-(\d+)\.mat(?:\.meta)?$',str(rel))
 if m and int(m.group(1))>=6:continue
 dest=art/rel;dest.parent.mkdir(parents=True,exist_ok=True)
 if f.suffix=='.cs' and dest.exists():continue
 shutil.copy2(f,dest)
readme='''# Telltale Engraving — shader de personaje separado

El material combina iluminación de novela gráfica con grabado discreto en sombras. Conserva los colores y texturas base. No usa renderer features, blits, filtros de cámara ni postprocesado. Woodcut Relief, Risograph y los materiales originales permanecen separados.

## Ver y probar

- Comparación visual: Artifacts/TelltaleEngraving/index.html. Las imágenes van incrustadas.
- Escena principal: Assets/Rendering/TelltaleEngraving/TelltaleComparison.unity.
- Pruebas de presets, huesos y blendshape: ValidationFixtures.unity, en esta misma carpeta.
- En la escena principal, entrar en Play: 1 original, 2 Telltale; arrastrar para orbitar, rueda para zoom, Espacio para giro y L para luz.
- Las escenas de prueba seleccionan temporalmente su pipeline limpio mientras están en Play y restauran el anterior. Así no reciben el risograph global del proyecto. El shader de personaje no depende de ese pipeline de prueba.

## Aplicar a otro personaje

Selecciona UN GameObject de personaje y usa Tools > Rendering > Telltale Engraving > Duplicate selected character with shader. Se crea una copia a su lado, con materiales nuevos. El original no se modifica. La copia conserva meshes, jerarquía, componentes de animación y referencias de huesos que Unity duplica. Revisa sus componentes de gameplay antes de usarla en una escena jugable.

El conversor inicial usa Armor; cambia sus parámetros o toma los valores del preset adecuado para cada material. Materials/Presets contiene Skin, Hair, Cloth, Armor, Accessory y Low. Los presets base son blancos para no imponer una paleta. Sample-*.mat son colores sintéticos de calibración, no tonos obligatorios.

- Skin: transición suave, intensidad de trama baja y menor contorno.
- Hair: una dirección UV y sin trama cruzada por defecto. Hay que orientar las UV por mechones.
- Cloth: más trama y cruce en sombras profundas. Orientar UV según tejido y pliegues.
- Armor: sombras más nítidas, trama limitada y brillo pintado.
- Accessory: trama y contorno desactivados inicialmente.
- Low: UV, sin cruce, grano, mapa de normales ni oclusión. Desactivar también el compañero de contorno para reducir geometría.

## Ajustes principales

Base Material conserva albedo, multiplicador de color, normal, oclusión y emisión. Saturation=1, Contrast=1 y Quantization=0 conservan la base cromática. No hay remapeado obligatorio a crema y rojo.

Graphic Novel Lighting permite ajustar umbrales de sombra y luz, anchura de transición, tintes y respuesta a luz/ambiente. Continuous Form conserva una pequeña variación de volumen dentro de las bandas.

Engraving Texture controla textura, intensidad, escala, grosor, direcciones, cruce y umbral. Cambia Line Coverage para variar el grosor y Texture Tiles para variar densidad. La textura suministrada tiene ocho surcos por repetición. La proyección UV sirve para mallas animadas; ObjectTriplanar es para piezas rígidas. Las UV y la orientación artística siguen siendo necesarias para cara, pelo y ropa. El shader no calcula anatomía ni curvatura principal.

Ink Imperfections controla grano superficial, escala del grano y rotura de líneas. Es ruido estable de textura; no depende de Time. El patrón se desvanece al ser demasiado pequeño para evitar moiré.

Authored Internal Lines recibe una máscara (negro dibuja tinta) para detalles elegidos por el artista. Está desactivada por defecto; no se dibujan todos los bordes internos automáticamente.

## Contorno

TelltaleOutline.shader y TelltaleOutlineRig implementan un casco invertido. Color, ancho a 1080p, corrección de distancia, sensibilidad de normal y pequeña variación de borde están en el material de contorno. Outline Weight en cada material del personaje escala el contorno cuando se reconstruye el compañero.

El compañero admite MeshRenderer y SkinnedMeshRenderer. Comparte malla y huesos; sincroniza pesos de blendshapes en LateUpdate únicamente si existen. Rebuild geometry outlines reconstruye tras cambios de malla/material. Desactivar el componente elimina los objetos que creó.

No se han inventado controles de sensibilidad a profundidad: este método no lee profundidad de pantalla. Los bordes internos se controlan por máscara artística y por geometría. Los LODGroups requieren integrar los renderers de contorno en sus listas de LOD; no se hace automáticamente.

## Límites de compatibilidad

Probado en Unity 6000.0.58f2 / URP 17.0.4, Forward y Forward+. Soporta materiales opacos y cutout; el conversor rechaza alpha blending transparente. No es un reemplazo PBR completo: usa metálico/suavidad escalares y brillo estilizado, no los mapas MetallicGloss ni Clear Coat. No soporta teselación ni desplazamiento. Normal y oclusión usan la UV base como URP Lit.

Los presets de piel y pelo están implementados como configuraciones, no validados sobre un rostro o cabello humano. La prueba de huesos y blendshape es una malla sintética. No demuestra actuación facial, belleza, ausencia de parpadeo en cualquier animación, legibilidad en combate ni calidad comercial final.

## Generación

Tools > Rendering > Telltale Engraving > Generate final test scene genera solo los assets y renders de este sistema. Requiere Ariete.fbx y sus materiales existentes. Guarda antes la escena abierta. La textura HatchAtlas.png se entrega ya generada; el script Python opcional reproduce el atlas y no es necesario para usar el shader.

Consultar TECHNICAL.md, PERFORMANCE.md y VALIDATION.md para decisiones y evidencias.
'''
tech='''# Decisiones técnicas

## Inspección y plan realizado

Unity: ProjectSettings/ProjectVersion.txt = 6000.0.58f2. Packages/manifest.json y cache local = URP 17.0.4 / Shader Graph 17.0.4. QualitySettings activa PC_RPAsset; PC_Renderer y LaboratoryRenderer son Forward (m_RenderingMode=0), Mobile_Renderer es Forward+ (2). Los renderers existentes contienen Risograph y, en PC, outline/tonemapper/SSAO. No se han reescrito ni sustituido.

Se validaron por etapas: baseline del modelo con URP Lit; iluminación sin trama (02); trama simple (03); cruce (04); contorno (05); imperfecciones (06); presets y combinación final (08). Cada etapa generó imágenes y comprobación de shader. El combinado final se volvió a renderizar tras ajustes de filtrado y soporte de animación. La revisión visual es técnica, no aprobación artística del usuario.

## Elección de HLSL

Shader Graph está instalado y puede usar Custom Function. No se afirma que sea incapaz. Se ha elegido un shader HLSL modular para controlar las bandas a partir de luces reales Forward/Forward+, compartir el mismo buffer de material entre color/profundidad/sombras y mantener el contorno geométrico separado. Un Graph Unlit con estos mismos bucles necesitaría un módulo HLSL de iluminación y una segunda implementación del contorno. No se entrega un grafo vacío ni una envoltura sin validar. Los materiales siguen siendo editables desde el Inspector.

## Módulos

TEInput.hlsl: datos y texturas de material, vertex transform, UV y cutout común. El shader tiene pasadas UniversalForwardOnly, ShadowCaster, DepthOnly y DepthNormalsOnly. No usa UsePass de URP Lit con un buffer incompatible.

TELighting.hlsl: main light, sombras, luces adicionales, entorno SH, tres regiones y variación continua moderada. El bucle usa las macros oficiales de URP 17 para Forward+. La radiancia real decide las regiones, incluso al variar intensidad. La multiplicación por tintes no reemplaza el color base.

TEHatching.hlsl: una textura RGB. R es distancia de surco, G rotura y B grano. Una dirección en medios oscuros; segunda en sombras profundas. Se atenúa al quedar por debajo del tamaño resoluble. El muestreo de distancia usa LOD 0 con AA por derivadas y desvanecimiento, evitando que el promedio de mips borre trazos finos antes de aplicar el umbral. Hay UV y triplanar local. Triplanar sobre coordenadas de posición deformada puede nadar en una malla animada: usar UV en ese caso.

TelltaleOutline.shader: casco invertido de geometría. No utiliza textura de profundidad de cámara. TelltaleOutlineRig crea compañeros por renderer; comparte referencias de huesos y comprueba cambios de pesos de blendshapes. Este seguimiento evita que el casco se quede en la expresión de reposo.

Las imperfecciones salen de textura fija; no se animan. Las líneas internas son máscaras elegidas por el artista, no un detector de todos los detalles del modelo. No se aplica un filtro al mundo ni a la UI.

## Correspondencia con el encargo

Implementado: albedo/color original, normal/AO/emisión, bandas configurables, textura de hatching, segundo ángulo, filtrado, UV/triplanar, casco geométrico, grano sutil, presets y configuración reducida, escenas y herramientas independientes.

Alternativas explícitas: detalle interno por máscara en lugar de contorno por profundidad; HLSL modular en lugar de Graph; sin paleta obligatoria ni postproceso. No hay detección automática de pliegues, pelo o curvatura anatómica.

Pendiente de validación de producción: rostro/cabello humano, varios personajes reales animados, LODGroups, luces/mixed lighting del nivel final, lectura y parpadeo durante una partida, GPU de gama baja y perfiles de tiempo GPU. La prueba no establece que el resultado sea comercialmente terminado.

## Fuentes primarias consultadas

Unity 6, bucle Forward y Forward+: https://docs.unity3d.com/6000.0/Documentation/Manual/urp/use-built-in-shader-methods-additional-lights-fplus.html
Shader Graph 17, Custom Function: https://docs.unity3d.com/Packages/com.unity.shadergraph@17.0/manual/Custom-Function-Node.html
SkinnedMeshRenderer, matrices por render: https://docs.unity3d.com/ja/6000.0/ScriptReference/SkinnedMeshRenderer-forceMatrixRecalculationPerRender.html
Las firmas se contrastaron con el paquete local y con compilación/render en Unity 6000.0.58f2.
'''
perf='''# Coste y configuración reducida

No se han medido FPS ni milisegundos GPU. No se afirma una mejora de rendimiento. Los datos siguientes son recuentos de estructura y lecturas del código, no un benchmark.

La escena de Ariete tiene 130 renderers originales y 260 con los cascos geométricos. Los materiales de superficie se comparten: 6 únicos. El coste del contorno depende de submeshes, triángulos y cobertura, y añade geometría/pasadas de dibujo. Estos recuentos NO son una medición de draw calls del frame: luces, sombras, depth y batching alteran ese número.

En la pasada de color: albedo = 1 lectura; normales, oclusión y emisión = 1 lectura opcional cada una; máscara interna = 1 opcional. El patrón principal es 1 lectura en UV o 3 en triplanar. El segundo ángulo añade 1/3 solo cuando hay cruce y sombra profunda. El grano independiente añade 1/3 si su intensidad es positiva. El atlas comparte una sola textura; el uso de lecturas no equivale a coste temporal y la ramificación varía por GPU.

Sin normales/AO/emisión/detalle, el peor caso de armadura triplanar con cruce y grano es 10 lecturas del código (albedo + 3 + 3 + 3). Low con UV, un ángulo y sin grano usa 2 (albedo + patrón). Hatching desactivado y grano cero evita la función de patrones. Desactivar TelltaleOutlineRig elimina sus objetos y trabajo de geometría; poner solo su ancho a cero no elimina la presentación del draw.

No se crean variantes por cada control artístico; son parámetros de material. Las variantes existentes corresponden a luces, sombras, Forward+, fog, instancing y normals de URP. Los buffers de material son compartidos entre las pasadas propias.

El sombreado y el patrón no necesitan cálculos CPU por frame. El visor mueve la cámara y la malla de prueba anima un hueso. El compañero de contorno solo hace seguimiento por frame cuando hay blendshapes: lee sus pesos y actualiza el casco si cambian. Esto debe perfilarse en caras con muchos controles.

La prueba de huesos activa forceMatrixRecalculationPerRender para obtener poses distintas en solicitudes de render consecutivas dentro del mismo frame de Editor. Es una propiedad del fixture y se copia al casco; no se fuerza globalmente en los personajes del proyecto. Los fixtures desactivan temporalmente SRP batching para comparaciones de materiales en el mismo frame y lo restauran en finally. No se deben interpretar sus tiempos como rendimiento de juego.

Para hardware modesto: usar Low; UV en lugar de triplanar; apagar grano/cruce; mantener pocas luces adicionales; desactivar contorno o utilizar un único casco de personaje con LOD adecuado. Validar tiempos GPU y lectura en el hardware objetivo antes de decidir el presupuesto.
'''
for name,text in [('README.md',readme),('TECHNICAL.md',tech),('PERFORMANCE.md',perf)]:
 (art/name).write_text(text,encoding='utf-8');(out/name).write_text(text,encoding='utf-8')
print('Sources, generated scenes, texture, six presets and documentation copied to dedicated project folder.')
