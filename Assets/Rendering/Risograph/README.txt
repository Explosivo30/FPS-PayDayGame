RISOGRAPH — Unity 6 / URP 17 (Render Graph)

El efecto está instalado en PC_Renderer, LaboratoryRenderer y Mobile_Renderer.
Se aplica una vez al final de la pila de cámaras, después del postprocesado.

AJUSTAR
Tools > Rendering > Risograph > Edit global settings.
En el perfil DefaultVolumeProfile, abrir Torbellino / Risograph.
Intensity = 0 desactiva; 1 produce la impresión completa.
Palette Size = Three: tinta de sombra + acento + papel (3 colores).
Palette Size = Four: añade la tinta principal (4 colores, como la referencia).
Los cuatro colores son editables. El papel cuenta dentro de la paleta.

Dot Size controla el tamaño de la retícula; Screen Angle su inclinación.
Halftone = 0 deja colores planos; 1 distribuye puntos entre dos tintas.
Paper Grain añade fibras; Ink Wear deja pequeñas motas de papel sin tinta.
Misregistration simula un desplazamiento del muestreo en zonas cálidas, sin placas independientes.
Exposure, Contrast y Saturation adaptan el efecto a la iluminación de la escena.
Midtone Lift ilumina los tonos medios; Paper Highlights abre zonas de papel crema.
Apply vivid reference style restaura el preset luminoso, con menos desgaste y rosa vivo.
Apply fantasy style aplica violeta profundo, lavanda, rosa luminoso y papel melocotón.
RisographFantasy.asset guarda ese preset; RisographReference.asset conserva el estilo anterior.
El ruido es estático y el tamaño de impresión se referencia a 1080p.
La retícula usa un mínimo de 3 píxeles por celda para evitar moiré a baja resolución.

PARA UNA ESCENA O ZONA
En su Volume, Add Override > Torbellino > Risograph y activar los parámetros
que quieras modificar. RisographReference.asset incluye el preset de referencia.
Los Volume locales pueden modificar o desactivar el estilo global con Intensity.
Screen Space Overlay UI se dibuja después del efecto y conserva su legibilidad.

INTEGRACIÓN
El shader usa Blit.hlsl y una pasada Render Graph; no necesita texturas externas.
Cada Renderer Feature referencia el shader para que se incluya en las builds.
Para reinstalar en los renderers del proyecto:
Tools > Rendering > Risograph > Install reference style.
En otro renderer: Add Renderer Feature > Risograph Renderer Feature y asignar
Risograph.shader en Shader. Requiere Render Graph activado (configuración actual).

Con Paper Grain = 0, Ink Wear = 0 e Intensity = 1 se conserva la paleta limitada,
salvo los píxeles de suavizado en los bordes de los puntos y el AA final de URP.
El grano introduce pequeñas variaciones de esas tintas, como en papel impreso.
