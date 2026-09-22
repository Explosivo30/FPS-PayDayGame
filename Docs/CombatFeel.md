# Gamefeel de combate

Unity 6000.0.58f2 / URP. Mejora aplicada a la presentación de las cuatro armas existentes y a la respuesta de los robots.

## Cambios

- Recuperación visual más rápida de pistola y carabina; empuje más pesado de escopeta. Menos sacudida de cámara al apuntar.
- Fogonazos con color y tamaño por arma y una iluminación breve sobre el arma. Sonido de movimiento del cuchillo separado del contacto.
- Los impactos mueven e inclinan la parte visual del robot en la dirección del disparo. La reacción está limitada y vuelve suavemente a su posición; no desplaza la navegación ni las colisiones.
- Destello breve del cuerpo, chispas en el contacto y pequeñas chispas de avería cuando queda poca vida. Los materiales compartidos no se modifican durante la partida.
- Caída orientada por el golpe mortal y chispas al aterrizar. La baja continúa emitiéndose una sola vez.
- Sonidos originales de metal, corte y eliminación, con una confirmación breve. Los perdigones contra un mismo robot se agrupan para producir un golpe claro; no se apilan ocho sonidos.
- Indicadores de cuatro trazos finos: blanco para impacto y ámbar para eliminación, con el centro despejado.

## Ajustes en el Inspector

En el componente RobotPresentation del prefab LaboratorySentinel: Reaction Strength controla la intensidad, Flash Duration la duración del destello, Spring Frequency la recuperación y Maximum Impact Sparks el límite de chispas.

En ImpactFeedbackPlayer del objeto GunController del jugador compartido: Confirmation Volume controla la confirmación y Metal Volume el sonido en el punto de contacto. WeaponPresentation conserva Kick Scale y Movement Scale por arma.

## Verificación

- Compilación de Windows completada: Builds/Tower/RobotTower.exe.
- 22 comprobaciones automatizadas superadas en Editor y Windows, sin errores registrados. Resultados en Artifacts/CombatFeel/feel-results.txt y Artifacts/CombatFeel/Windows/feel-results.txt.
- Se verificaron daño, consumo de cartucho, agrupación de ocho perdigones, límite y recuperación de la reacción, pausa, contacto diferido del cuchillo, dirección de caída, colisiones y una única baja.
- La integración matemática de la recuperación visual coincide a 30, 60 y 144 FPS. Esto no es una medición del rendimiento de una partida completa.
- Level1 arranca con el jugador compartido, sin errores en la prueba de arranque.
- Revisión visual en Editor: impactos, exposición del arma, chispas, caída e indicador de eliminación. Las capturas automáticas de Windows oculto no renderizaron imagen y no se usan como evidencia visual.

## Demostración

Artifacts/CombatFeel/Combat-feel.mp4 contiene unos 15 segundos a 1080p con las cuatro armas y el audio real mezclado por Unity. Se grabó en Editor mediante una secuencia automatizada con enemigos inmóviles, usando el disparo, daño y munición reales. Se fijaron el apuntado y la dispersión para comparar los impactos. No representa una partida manual ni una prueba de dificultad.

Pico de audio de la captura: 0,606, sin saturación. El vídeo se puede regenerar con CombatFeelBuild.Play y el argumento -feel-record; Tools/encode_combat_video.py combina los fotogramas y el audio mediante Blender.
