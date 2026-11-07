WOODCUT RELIEF — shader nuevo

Abrir index.html: imagenes incrustadas, anterior/nuevo, detalle, luces y giro. La referencia del usuario se incluye en reference.png.

Assets/Rendering/WoodcutRelief/WoodcutRelief.shader es un material nuevo. No modifica el shader anterior. El preset usa tinta azul oscura, papel crema y rojo teja. PaletteStrength=0 conserva los colores base. InkStrength controla tinta; Carving abre surcos claros en las zonas oscuras. Gouge Width, Broken Ink y Print Wear controlan marcas e imperfecciones. Density se expresa en unidades locales de la malla: los materiales generados compensan la escala de importacion del FBX.

ArtistUV permite orientar las lineas con las islas UV. CurvedObject usa coordenadas cilindricas locales por pieza; PlanarObject se usa en placas. No existe extraccion automatica de curvatura anatomica. Las marcas tienen longitud y ancho variables; aparecen por niveles de luz y por pigmento oscuro. En mallas deformables debe usarse ArtistUV o coordenadas de reposo horneadas. Esta prueba valida mallas rigidas; no piel, pelo, tela ni animacion esqueletica.

Escena: Assets/Rendering/WoodcutRelief/WoodcutReliefComparison.unity. En Play, 1 original / 2 nuevo. Ratón para orbitar; rueda para zoom; Espacio para giro; L para mover la luz. El visor comparte el controlador y contorno del estudio EngravingCharacter. Usa temporalmente el pipeline de prueba y lo restaura al desactivarse. No es una nueva escena de gameplay.

Generar de nuevo: Tools > Rendering > Engraving > Generate woodcut relief comparison. Guarda antes la escena abierta. Solo escribe esta carpeta de prueba y sus renders.

Validacion: shader compilado sin errores y renders de 24 posiciones de camara y 4 direcciones de luz, Unity 6000.0.58f2 / URP 17.0.4 / RTX 3070 en el laboratorio aislado. No hay pruebas de FPS, partida ni deformacion esqueletica. La semejanza con la referencia es una decision artistica pendiente de revision; el robot no sustituye al personaje organico de la imagen.
