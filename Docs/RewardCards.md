# Cartas de recompensa y mejoras

## Flujo

Cada oleada completada en la torre concede una oferta gratuita. Tras terminar el fotograma del último impacto, el juego toma una pausa propia y presenta una celebración de 0,6 s. Se muestran hasta tres cartas elegibles. Ratón o 1/2/3 seleccionan; Elegir/Enter confirma. La confirmación dura 0,45 s y después comienza el descanso normal.

La oferta permanece fija. No se puede renovarla, comprar mientras está pendiente ni salir por el ascensor. Al completar las oleadas obligatorias se mantiene la opción voluntaria de continuar. Una muerte durante el cierre de oleada cancela la recompensa y conserva la pausa de la pantalla de muerte.

Los rangos y efectos duran todo el intento, entre escenas, y desaparecen al reiniciar. No cambian las investigaciones permanentes. El botón **Ver mis cartas** en la tienda muestra las cartas elegidas. El HUD de combate se oculta mientras se muestran estos menús; los especiales usan icono propio, marco doble y rótulo para distinguirse también sin depender del color.

## Configuración

`Assets/Art/Rewards` contiene 21 definiciones y una recompensa de respaldo:
- Daño por cada una de las cuatro armas: +20% por rango, máximo 3.
- Cadencia (+15%), capacidad (+25%) y recarga (−12%) por cada arma de fuego, máximo 3.
- Suministro por cada arma de fuego: dos cargadores a la reserva, sin superar su capacidad.
- Blindaje: +20 salud máxima y recupera 20; máximo 3.
- Condensador: +15 escudo máximo y recupera 15; máximo 3.
- Reparación: hasta 40 salud y 25 escudo.
- Recuperador: 5 escudo por baja propia; un rango.
- Autocargador: transfiere desde la reserva un 10% del cargador del arma que causó la baja, redondeado hacia arriba; un rango.
- Fondos de apoyo: 40 chips cuando no hay suficientes alternativas útiles.

Las ofertas contienen ofensiva, manejo/suministros y supervivencia. La ofensiva prioriza el arma equipada. Se excluyen duplicados, máximos y suministros inútiles. Las primeras dos oleadas son básicas; completar un piso habilita los especiales y garantiza uno disponible en esa oferta. Si faltan grupos, se completan con otras cartas elegibles. Si únicamente quedan chips, se conceden directamente.

El menú **Tools / HELIX / Configure reward cards** regenera la configuración inicial, iconos y sonidos y conecta los componentes a TowerCore. Solo debe usarse para regenerar los valores iniciales. **RewardBuild.Build** compila sin regenerar ni sobrescribir el balance del Inspector.

## Arquitectura

- `RewardCard`: configuración inmutable durante el juego.
- `RunRewards`: estado por intento, oferta fija, rangos y confirmación. Tokens por partida, visita y oleada impiden recompensas repetidas.
- `IRewardEffect`: elegibilidad, previsualización y aplicación. Implementaciones separadas para estadísticas, jugador, suministros, especiales y chips.
- `RewardCardView`: presentación e interacción; no aplica estadísticas ni controla oleadas.
- `WeaponStatLedger`: libro de modificadores por instancia. Recalcula desde su base; tienda y cartas tienen fuentes independientes. Armas guardadas también se actualizan.
- `RunPause`: pausas con propietario, compartidas por recompensas, tienda, ascensor y muerte.
- `PlayerKill`: baja atribuida a un arma del jugador y deduplicada por enemigo. Los efectos especiales no escuchan la muerte genérica.
- `KillShieldReward` y `AutoLoaderReward`: suscripciones independientes que se retiran al destruir la partida.

Daño, cadencia y capacidad: primero se suman los incrementos planos de tienda; después se multiplica por 1 más el porcentaje acumulado de cartas. La capacidad se redondea hacia arriba sin rellenar munición. Recarga: base × descuentos de tienda × (1 − suma de cartas). Mínimos: 0,6 s/cargador, 0,2 s/cartucho y 0,05 s para inicio/cierre. Las cartas afectan las tres fases de la escopeta; se conserva el comportamiento previo de la tienda, que reduce la inserción.

La restauración parcial de escudo no reinicia la regeneración ni supera el máximo. El autocargador conserva las acciones en curso y consume reserva real; no cambia de arma ni dispara.

## Compatibilidad y pruebas

Las escenas anteriores sin RunRewards conservan su funcionamiento. Se mantienen precios, recompensas de chips y perfil permanente. Los assets compartidos no guardan niveles ni estadísticas modificadas durante la partida.

Prueba optativa: `RewardBuild.Play -reward-qa -player-profile <perfil temporal>`. Grabación: `-reward-record`. `-reward-output` permite separar resultados de Editor y Windows. Nunca usar el perfil personal para QA.

Las pruebas cubren orden de mejoras, previsualización, munición, elegibilidad, pausa, confirmación doble, especiales, tres oleadas y una extra, transición y reinicio. Los resultados y capturas se guardan en `Artifacts/Rewards`. La secuencia automática de grabación reduce temporalmente una oleada a un enemigo; conserva el circuito real de baja, puntuación, final de oleada y recompensa. No cambia la configuración normal de las escenas.

## Entrega

- Windows: `Builds/Tower/RobotTower.exe` (distribuir la carpeta Tower completa, con sus datos).
- Vídeo a 1080p con sonido del juego: `Artifacts/Rewards/Reward-cards.mp4`.
- Capturas finales: `cards-in-game.png`, `shop-in-game.png`, `cards-special.png` y `card-history.png` dentro de `Artifacts/Rewards`.
- Informe de comprobaciones: `Artifacts/Rewards/Validation.md`.

Los tiempos de presentación están en `RunRewards.Present` (0,6 s) y `FinishChoice` (0,45 s); el catálogo y los valores de cada carta se ajustan desde sus assets en el Inspector. El aspecto se configura en `RewardCardView`; el audio está asignado en TowerCore. Los perfiles usados para validar son independientes del progreso del jugador.
