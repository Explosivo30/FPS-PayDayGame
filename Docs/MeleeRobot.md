# Robot de seguridad cuerpo a cuerpo

Implementa el **Enemigo Normal** de la página 22 de `Shooter Robots.pdf`: bípedo humanoide, referencia de estilo Hi-Fi Rush, persigue desde su aparición y golpea con un puñetazo al acercarse. La sección anterior de especiales no se usa para convertirlo en tanque o corredor. Las variantes, piezas destructibles y habilidades de las páginas 22–24 quedan fuera de este único enemigo.

## Juego

- Se mezcla con el centinela a distancia en TowerCore (todos los pisos actuales) y SampleScene.
- El 40% de cada oleada son robots cuerpo a cuerpo: 2 de 5, 3 de 7, 4 de 9. Se conserva el total, la separación entre apariciones, el límite simultáneo, las recompensas y las condiciones del ascensor.
- Persigue directamente por NavMesh; los centinelas conservan sus formaciones. No desplaza al robot durante la animación del golpe.
- Empieza a preparar el ataque a 1,65 m. La dirección queda fijada al comenzar: brazo levantado, iluminación ámbar, triángulo y aviso de servo.
- Anticipación de 0,62 s; golpe de 0,16 s, con un único contacto al 45% de esa fase; recuperación de 0,85 s. Esquivar de lado o alejarse permite contraatacar.
- Contacto máximo a 1,85 m, arco frontal de ±42°. Comprueba altura y visibilidad hasta la cápsula del jugador. Las paredes bloquean el daño.
- La tienda congela movimiento, temporizadores, pose y audio. Matarlo cancela el golpe pendiente, desactiva navegación y colisiones, cuenta una baja y retira el cuerpo a los 3 s.

## Balance inicial (propuesta ajustable, no cifras del GDD)

Vida = 85% de la vida del centinela en ese piso/oleada. Daño = 150% del daño del centinela. Velocidad = 112%, limitada a 4,6 m/s. En SampleScene: 85 de vida y 18 de daño. Los multiplicadores mantienen la progresión existente de rondas y farmeo.

`MeleeRobotCombat` expone distancias, arco, tiempos, daño y multiplicadores en el Inspector. `GameManager.meleeShare` controla la composición.

## Arte y arquitectura

- Modelo original: `ArtSource/MeleeRobot.blend`.
- Importable: `Assets/Art/MeleeRobot/MeleeRobot.fbx`.
- Prefab jugable: `Assets/Art/MeleeRobot/SecurityBrawler.prefab`.
- 1.376 vértices de geometría rígida con pivotes para torso, cabeza, brazos, codos, caderas y rodillas. Cerámica clara, guantes grandes y hombros naranjas distinguen la silueta.
- Animación procedural de marcha, anticipación, puñetazo y recuperación en las articulaciones. La reacción direccional a disparos, las chispas y la caída reutilizan el sistema de impactos del juego, en otra raíz visual para evitar sobrescrituras.
- Cuatro sonidos originales: aviso, movimiento del puño, contacto y pisada.
- El núcleo existente conserva IDamageable, IHitReactable, muerte, munición/recompensas y contadores. El componente opcional MeleeRobotCombat selecciona el comportamiento cercano; no introduce otra suscripción de muerte.

## Reproducción y validación

El menú **Tools / HELIX / Create melee security robot** reconstruye únicamente el prefab, sus materiales y las referencias de aparición. No reconstruye los niveles.

`-melee-qa` activa pruebas optativas en Editor o Development Build. `-melee-record` graba una demostración automatizada dentro del juego. Usar `-player-profile` con una ruta temporal para no tocar la progresión del jugador.

Resultados y capturas en `Artifacts/MeleeRobot`. Pasaron 53 comprobaciones tanto en Editor como en Windows, más 22 de regresión del combate previo. El informe completo está en `Artifacts/MeleeRobot/Validation.md`. La grabación es una secuencia automatizada de 6,44 segundos, no una sesión de prueba manual.
