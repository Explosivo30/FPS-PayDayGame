# Jugador y ciclo de partida

Unity 6000.0.58f2 / URP. El jugador usa las cuatro armas y los pisos existentes.

## Movimiento y daño

- WASD para moverse, Espacio para saltar, C o Ctrl para agacharse. Agacharse con suficiente velocidad inicia un deslizamiento corto.
- Aceleración y frenado separados, velocidad diagonal limitada, dirección ajustable en el aire y deslizamiento con impulso y tiempo de reutilización limitados.
- El salto admite 0,10 s de margen al abandonar un borde y 0,12 s de anticipación antes de aterrizar. No permite dobles saltos.
- Agacharse conserva la posición de los pies. Un techo bajo impide ponerse de pie.
- 100 de salud y 50 de escudo iniciales. El escudo comienza a recuperar 12 puntos/s tras 3 segundos sin recibir daño. La salud necesita curación.
- El exceso de daño sobre el escudo pasa a salud una sola vez. Cualquier nuevo golpe reinicia el tiempo de recuperación, incluso con el escudo agotado.
- Sonidos distintos para absorción, rotura, salud, recuperación, pasos y aterrizaje. Barras de estado e indicador de origen del golpe. La reacción de cámara es breve y configurable mediante PlayerFeedback.

## Recursos y mejoras

Las reservas de munición siguen siendo limitadas. Cada tercera baja de una oleada deja una célula recogible al acercarse, con 3 disparos de pistola, 8 de carabina y 1 cartucho, respetando los máximos. Dura 45 segundos y no se recoge a través de paredes.

La tienda pausa el combate y reúne las mejoras, la curación y la reposición. Curar hasta 40 de salud cuesta 60 chips; añadir dos cargadores a la reserva de cada arma cuesta 40. No se cobra cuando el recurso está completo.

Hay seis mejoras iniciales de partida: salud máxima, escudo, movilidad, daño de plasma, cargador de carabina y daño de escopeta. Los precios crecen por nivel. Aumentar el cargador no regala munición. Las mejoras afectan también a las armas guardadas.

## Elegir el riesgo

Tras las tres oleadas obligatorias se abre el ascensor. Las oleadas extra requieren pulsar F y muestran la recompensa prevista. El ascensor sigue disponible durante ellas. Se mantienen los límites de daño, precisión y 12 enemigos simultáneos.

Completar el objetivo obligatorio de un piso guarda un núcleo automáticamente. Las oleadas extra no otorgan núcleos adicionales. Cada visita al piso solo entrega su núcleo una vez.

## Muerte y progreso

La pantalla de muerte muestra piso, bajas, oleadas, causa y núcleos obtenidos. Volver a intentar comienza en el laboratorio con 100 chips, salud y escudo iniciales, munición inicial y mejoras de partida a cero.

Se conservan núcleos, pisos descubiertos e investigaciones. Los núcleos desbloquean tres opciones de tienda:

| Investigación | Núcleos | Efecto de la opción durante una partida |
| --- | ---: | --- |
| Estabilizador de carabina | 2 | Menor retroceso vertical |
| Choke de escopeta | 2 | Menor dispersión |
| Recarga optimizada | 3 | Recargas más rápidas |

Investigar habilita la opción: su mejora se compra después con chips y se pierde al morir. No añade daño o vida permanentes.

El archivo player-progress-v1.json se guarda en Application.persistentDataPath, mediante sustitución atómica. Un perfil ilegible no se sobrescribe. Las pruebas usan perfiles separados dentro de Artifacts/Player.

## Editar

PlayerStateMachine y PlayerLocomotion controlan el movimiento y el daño. PlayerFeedback configura sonido y movimiento visual de cámara; Shield configura recuperación. Los valores y precios del catálogo están en Assets/Art/Player.

Tools > HELIX > Update player and run configura el prefab compartido y el catálogo de TowerCore. No reconstruye la geometría de los pisos. PlayerBuild.Build compila los valores actuales sin volver a generar el catálogo.

## Pruebas

PlayerPlaytest con -player-qa supera 44 casos en Editor y en la compilación de Windows, incluyendo salto a 30, 60 y 144 FPS, techo bajo, pausa, exceso de daño, recuperación, compras con armas guardadas, recogidas, guardado, muerte y reinicio completo. El argumento -player-profile permite seleccionar un perfil aislado para la prueba.

El recorrido de TowerPlaytest supera 57 comprobaciones: cambios de piso, permanencia de la interfaz, guardado del núcleo al completar oleadas reales, inicio voluntario de oleadas extra y retirada con el ascensor disponible. Las 22 comprobaciones previas de combate pasan con el nuevo jugador. Level1 también arranca sin errores.

Resultados: Artifacts/Player/player-results.txt, Artifacts/Player/Windows/player-results.txt y Artifacts/Tower/tower-results.txt. Capturas: shield-hit.png, shield-break.png, shop.png y death-summary.png en la misma carpeta.

La compilación actualizada está en Builds/Tower/RobotTower.exe. Las capturas mostradas se revisaron en Editor; la prueba de Windows con ventana oculta verifica el comportamiento, no el aspecto visual.

Las pruebas son automatizadas; no sustituyen una valoración humana del equilibrio de una partida completa.
