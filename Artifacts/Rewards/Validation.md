# Validación de cartas y mejoras

Fecha: 22 de septiembre de 2026. Unity 6000.0.58f2, URP, Windows x64.

## Resultado

Build de desarrollo generada correctamente en `Builds/Tower/RobotTower.exe`. Conservar la carpeta Tower completa para ejecutarla.

| Recorrido automático | Entorno | Comprobaciones aprobadas | Resultado |
|---|---|---:|---|
| Recompensas y recorrido integrado | Editor | 78 | Sin errores |
| Recompensas y recorrido integrado | Build Windows | 78 | Sin errores |
| Impactos y combate | Build Windows | 22 | Sin errores |
| Jugador, compras, muerte y reinicio | Build Windows | 44 | Sin errores |
| Robot cuerpo a cuerpo y oleadas mixtas | Build Windows | 53 | Sin errores |
| Laboratorio, armas, retroceso y cinco oleadas | Editor / SampleScene | 24 | Sin errores |
| Compatibilidad con Level1 | Editor | 1 | Sin errores |

Las 78 comprobaciones de recompensas se repiten en ambos entornos; no son 156 casos distintos. En conjunto son 222 comprobaciones distintas.

## Qué se ha comprobado

- 21 cartas configuradas con iconos válidos, oferta estable y tres opciones diferentes cuando existen alternativas elegibles.
- Prioridad ofensiva del arma equipada, dos primeras oleadas básicas, especial garantizada al completar el piso y chips automáticos al agotarse el catálogo.
- Rangos separados de la tienda, +60% de daño a nivel 3, orden independiente entre compras y cartas y mínimos seguros de recarga.
- Capacidad sin munición gratuita, suministro limitado por reserva, armas guardadas y ausencia de modificaciones en el arma original.
- Cancelación de recargas y de ambas fases del cambio de arma; conservación de cartuchos ya insertados y obligación de soltar el disparo.
- Una activación especial por baja real, incluso con ocho perdigones; ninguna por daño ambiental, duplicados o reserva vacía. El cuchillo recupera escudo sin cargar otras armas.
- Selección sin confirmación, confirmación doble, bloqueos de tienda/suministros/ascensor/oleada extra y pausas con varios propietarios.
- Tres oleadas obligatorias y una extra reales, una carta por oleada, cambio de piso con cartas conservadas, muerte durante una recompensa pendiente y reinicio limpio. Núcleos permanentes conservados.
- Regresión de cadencia, perdigones, cuchillo, recargas y retroceso a 30/60/144 FPS en la prueba existente.
- Cinco oleadas de SampleScene mediante armas reales, máximo de 12 enemigos simultáneos y recorrido de las dos rampas.

## Presentación y grabación

Revisión visual de capturas a 1920 × 1080:
- `cards-in-game.png`: elección después de completar una oleada.
- `shop-in-game.png`: compra con previsualización y confirmación.
- `cards-special.png`: especial con icono propio, marco doble y resultado concreto.
- `card-history.png`: consulta de cartas adquiridas.

`Reward-cards.mp4`: 11 segundos a 1080p, con audio capturado del juego. Muestra última baja, final real de oleada, elección, mejora aplicada, compra y regreso al descanso entre oleadas. La grabación automatizada reduce temporalmente esa oleada a un enemigo; el balance normal no cambia. Pico de audio: 0,602, sin saturación.

Las imágenes de pruebas aisladas pueden mostrar «oleada 00», porque esas ofertas se generan fuera del contador del combate. La captura y grabación del flujo integrado muestran «oleada 01».

## Alcance de las pruebas

Los recorridos son automáticos. La selección de interfaz se ejercita mediante sus métodos/eventos; no se presenta como una sesión manual de teclado y ratón. El laboratorio usa velocidad de simulación ×2 y repone salud al bot para repetir cinco oleadas. Los perfiles de QA están separados del progreso personal.

La revisión visual procede del Editor; Windows se ha comprobado en modo de prueba oculto. La grabación no es una medición del rendimiento del juego.

Resultados completos en `results.txt`, `Windows/results.txt`, `CombatRegression/feel-results.txt`, `PlayerRegression/player-results.txt`, `MeleeRegression/results.txt`, `LaboratoryRegression/playtest-results.txt` y `Legacy/level1-results.txt`.

Valores, arquitectura y puntos de ajuste: `Docs/RewardCards.md`.
