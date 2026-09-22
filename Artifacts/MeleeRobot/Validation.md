# Validación — Robot de seguridad cuerpo a cuerpo

Fecha: 20 de septiembre de 2026. Unity 6000.0.58f2, URP, Windows.

## Resultado

- **53/53 comprobaciones en Editor**, en el Bosque robótico.
- **53/53 comprobaciones en el ejecutable Windows**, cargando el Bosque robótico desde el arranque del laboratorio.
- **22/22 comprobaciones de regresión del combate existente** en Windows: pistola, carabina, escopeta, cuchillo, impactos, muerte y recuperación de la reacción visual.
- Compilación Windows completada correctamente: `Builds/Tower/RobotTower.exe`.
- Sin errores o excepciones durante las pruebas de juego.

## Qué se comprobó

Persecución desde la aparición; anticipación antes del daño; pausa de tienda; contacto único; dirección fijada al preparar el golpe; recuperación sin repetir daño; esquiva lateral, hacia atrás y por detrás; paredes y diferencias de altura; cancelación al morir; una sola baja; desactivación de colisiones y navegación; retirada del cuerpo a los 3 s.

El robot recorrió ambos accesos elevados del Bosque robótico. Se resolvieron automáticamente tres oleadas mixtas (5, 7 y 9 enemigos; 2, 3 y 4 de cuerpo a cuerpo), con recuentos y recompensas únicos y desbloqueo de las oleadas voluntarias.

Con límites solicitados de 30, 60 y 144 FPS, el contacto en Windows ocurrió a 0,701 / 0,702 / 0,697 segundos desde el inicio de la anticipación, con un único daño. Esta prueba comprueba la temporización; no es un benchmark de rendimiento ni garantiza 144 FPS sostenidos.

## Evidencias

- `results.txt`: Editor.
- `Windows/results.txt`: ejecutable.
- `RangedRegression/feel-results.txt`: combate anterior.
- `robot.png`, `anticipation.png`, `punch.png`, `collapse.png`: capturas reales del Editor revisadas visualmente.
- `Melee-robot.mp4`: demostración automatizada de 6,44 s, 1920 × 1080, con audio real del juego. Muestra aproximación, anticipación, esquiva lateral, contraataque y caída.
- `recording.json`: 174 fotogramas y señal de audio comprobada.

Las capturas de los procesos Windows en modo batch resultaron negras y no se usan como evidencia visual. La revisión visual corresponde al Editor. No se presenta esta secuencia automatizada como una sesión manual de juego.

El balance (85% de vida, 150% de daño y 112% de velocidad del centinela, con límite de 4,6 m/s) es inicial y ajustable en el Inspector.
