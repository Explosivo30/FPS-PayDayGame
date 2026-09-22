# Robot Tower — pisos jugables

Unity 6000.0.58f2 / URP. Primera versión jugable de los pisos del GDD, con recursos existentes y geometría original. El acabado es una primera pasada de arte estilizado.

## Jugar

- Abre `Assets/Scenes/RoboticForest.unity` y pulsa Play para empezar directamente en el bosque.
- Abre `Assets/Scenes/LaboratoryFloor.unity` para empezar desde el laboratorio.
- La versión de Windows está en `Builds/Tower/RobotTower.exe` y empieza en el laboratorio.
- Movimiento WASD, ratón para mirar, clic izquierdo para atacar, clic derecho para apuntar, R para recargar, Q para cambiar de arma, E para interactuar. Espacio para saltar. E también cierra la tienda.
- En el ascensor, gira hacia el panel interior y pulsa E apuntándolo. Debes estar dentro de la cabina y haber superado tres oleadas.

## Escenas

| Escena | Contenido |
| --- | --- |
| LaboratoryFloor | Adaptación del laboratorio existente, terminales y ascensor. |
| RoboticForest | Arena de 56 × 50 m, árboles de cobre y copas metálicas, circuitos conectados, coberturas, dos rampas y pasarela a 3 m. Dos raíces anuncian pulsos con un perímetro ámbar. |
| CrystalGarden | Arena de 50 × 46 m, islas de cultivo hexagonales, plantas luminiscentes, agujas de cristal, arcos y rutas que rodean las coberturas. |
| TowerCore | Jugador, inventario, tienda, mejoras, HUD y estado de partida compartidos. Se carga automáticamente. |

Secuencia de esta demo: **laboratorio → bosque robótico → jardín de cristal → laboratorio**. El último enlace permite volver a probar los pisos; todavía no representa un final de campaña.

Cada piso dispone de cuatro entradas enemigas ocultas y navegación propia. Las escenas originales SampleScene y Level1 se conservan, también en la lista de compilación.

## Partida y recursos

- Tres oleadas obligatorias (5, 7 y 9 enemigos) desbloquean el ascensor.
- Tras las tres oleadas, F permite iniciar voluntariamente una oleada extra con mayor vida, daño, precisión y recompensa. El ascensor sigue disponible para retirarse. Daño y precisión tienen límites; el número simultáneo de enemigos permanece limitado a 12.
- La cabina abre y cierra sus puertas. El cambio requiere confirmación en el panel, carga la siguiente escena y descarga la anterior.
- Se conservan las mismas instancias de jugador y armas: salud, escudo, chips, mejoras, arma seleccionada, cargadores y reservas.
- El cambio cancela recargas y apuntado. Las acciones se bloquean durante el viaje.
- Morir muestra el resumen y permite volver a intentar desde el laboratorio con 100 chips, equipo inicial y mejoras a nivel cero. Los núcleos guardados al completar objetivos, los descubrimientos y las investigaciones sobreviven a la muerte. Consulta Docs/Player.md para las reglas actuales.
- Reserva inicial/máxima: pistola 72, carabina 180, escopeta 36. Los cargadores siguen siendo 12, 30 y 6. El cuchillo no consume munición.
- Recargar transfiere únicamente las balas disponibles. Cancelar conserva cargador y reserva; la escopeta conserva las inserciones terminadas.
- Terminal de munición: 40 chips por dos cargadores de reserva para cada arma, respetando sus límites.
- Terminal médico: 60 chips por hasta 40 de salud. No cobra si la salud ya está completa. Curación y munición también se ofrecen en la tienda. Cada tercera baja deja una pequeña recogida de munición.
- Estos precios y multiplicadores son valores iniciales para probar, no un balance definitivo.

## Editar y extender

El componente `TowerFloor` de cada escena configura nombre, número, destino, oleadas necesarias, introducción y estadísticas iniciales de enemigos. Referencia la llegada, el ascensor y las entradas de su propio piso.

`TowerCore` se mantiene cargada de forma aditiva durante la partida. Las escenas de piso no incluyen copias del jugador ni de los gestores. Los enemigos y la navegación pertenecen al piso activo.

El menú **Tools → Tower → Build playable floors** reconstruye las cuatro escenas generadas y su navegación. Regenerar reemplaza las ediciones manuales de esas escenas; para cambios de diseño repetibles, modifica primero los constructores de `Assets/Editor/Tower`.

Materiales y mallas originales: `Assets/Art/Tower`. El shader Cel Metal usa iluminación por bandas. Los materiales antiguos de las armas que no funcionan con URP se convierten a copias locales para TowerCore.

## Validación reproducible

`TowerValidation.Play` con `-tower-qa` ejecuta comprobaciones dentro de Unity. `-tower-lab` comienza la prueba en el laboratorio; por defecto comienza en el bosque. La compilación de desarrollo también admite `-tower-qa`.

Se comprueba carga directa, número de jugadores, navegación de las cuatro entradas, subida física por ambas rampas del bosque, reservas y cancelación de recarga, compra y persistencia de mejoras, recuento de oleadas, bloqueo del ascensor, cambios consecutivos, descarga del piso anterior y reinicio por muerte.

Las pruebas de oleadas aceleran el tiempo y eliminan enemigos mediante su interfaz de daño, con llamadas duplicadas para comprobar que una muerte se contabiliza una sola vez. No equivalen a una partida humana ni a una valoración del equilibrio.

Resultados y capturas: `Artifacts/Tower`. El balance, la legibilidad de señales durante combate y el acabado artístico deben seguir afinándose con partidas reales.
