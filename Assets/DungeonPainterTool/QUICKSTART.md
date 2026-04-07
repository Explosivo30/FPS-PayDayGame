# Quick Start Guide - Dungeon Painter

## 5 Minutos para Tu Primera Mazmorra

### Paso 1: Instalación (30 segundos)
1. Copia la carpeta `DungeonPainterTool` a `Assets/`
2. Unity detectará automáticamente los scripts
3. Ve a `Window → Dungeon Painter`

### Paso 2: Crear Data (30 segundos)
1. En la ventana Dungeon Painter, click **"Create New"**
2. Guarda como `Assets/MyFirstDungeon.asset`
3. Configura `Cell Size: 5`

### Paso 3: Diseñar Layout (2 minutos)

#### Crear Primera Sala
1. Mode: **Paint Room**
2. `Height Level: 0`
3. Click y arrastra un cuadrado de ~5x5 celdas
4. Suelta el mouse → Se crea la sala

#### Crear Segunda Sala (Más Abajo)
1. Cambia `Height Level: -1` (color gris)
2. Arrastra otra sala 5x5 unos espacios a la derecha
3. Suelta → Segunda sala creada

#### Conectar las Salas
1. Mode: **Place Node**
2. Click en el centro de la primera sala → Aparece nodo blanco
3. Click en el centro de la segunda sala → Aparece nodo gris
4. Mode: **Connect Nodes**
5. Click en nodo blanco → Click en nodo gris
6. ¡Conexión creada! (Automáticamente será una rampa porque tienen diferentes alturas)

### Paso 4: Generar (30 segundos)
1. Click **"Generate Dungeon"**
2. Observa en la Hierarchy: `Dungeon_[fecha]`
3. Click en Scene view → ¡Ves tu mazmorra en 3D!

### Paso 5: Customizar (1 minuto)

#### Cambiar Tipo de Conexión
1. Mode: **Select/Move**
2. Click en la línea que conecta los nodos
3. En el panel derecho, cambia `Type: Stairs`
4. Delete el GameObject generado
5. Click **"Generate Dungeon"** de nuevo
6. ¡Ahora hay escaleras!

#### Ajustar Ancho del Pasillo
1. Con la conexión seleccionada
2. En `Width Points`:
   - Point 0: Width = 2
   - Point 1: Width = 6
3. Regenera → Pasillo ensanchado

---

## Controles Esenciales

| Acción | Control |
|--------|---------|
| Pan (mover vista) | Middle Mouse Drag |
| Zoom | Scroll Wheel |
| Colocar nodo | Click (en modo Place Node) |
| Pintar sala | Click + Drag (en modo Paint Room) |
| Seleccionar | Click (en modo Select) |
| Borrar | Click (en modo Delete) |

---

## Modos de Herramienta

1. **Place Node** → Colocar puntos de conexión
2. **Connect Nodes** → Crear pasillos/rampas/escaleras
3. **Paint Room** → Dibujar salas arrastrando
4. **Select/Move** → Editar propiedades
5. **Delete** → Eliminar elementos

---

## Niveles de Altura

- **Gris Oscuro** = Niveles profundos (-3, -2, -1)
- **Blanco** = Nivel base (0)
- **Azul** = Niveles altos (1, 2, 3)

**Tip**: Cambia `Height Level` antes de crear salas/nodos

---

## Tipos de Conexión

- **Flat** → Pasillo plano (mismo nivel)
- **Ramp** → Rampa suave (diferentes alturas)
- **Stairs** → Escaleras con escalones
- **Tunnel** → Túnel cerrado

**Auto**: La tool elige Ramp automáticamente si hay diferencia de altura

---

## Workflow Típico

```
Create New Data
    ↓
Paint Room (sala grande)
    ↓
Change Height Level
    ↓
Paint Room (sala en otro nivel)
    ↓
Place Nodes (en centros de salas)
    ↓
Connect Nodes
    ↓
Generate Dungeon
    ↓
Inspect & Tweak
    ↓
Replace Meshes (cuando tengas modelos)
    ↓
Handcraft (añadir enemigos, loot, etc.)
```

---

## Ejemplos Rápidos

### Sala Simple con Corredor
```
1. Paint Room → Sala 5x5
2. Place Node → Centro sala
3. Place Node → Fuera de la sala
4. Connect Nodes → Une ambos
5. Generate
```

### Mazmorra Multinivel
```
1. Paint Room (Height: 0) → Sala A
2. Paint Room (Height: -2) → Sala B más abajo
3. Place Nodes en ambas
4. Connect Nodes → Rampa automática
5. Generate
```

### Pasillo Ancho Variable
```
1. Crear 2 nodos y conectarlos
2. Select la conexión
3. Editar Width Points:
   - 0.0 → 2m (inicio estrecho)
   - 0.5 → 8m (medio ancho)
   - 1.0 → 2m (final estrecho)
4. Generate
```

---

## Tips Pro

💡 **Organización**: Nombra tus salas en Properties para encontrarlas fácil

💡 **Performance**: Cell Size grande (10m+) para mazmorras enormes

💡 **Estilo Danmachi**: Usa rampas, múltiples alturas, y evita ángulos rectos

💡 **Testing**: Usa el script `DungeonExample.cs` para crear layouts programáticamente

💡 **Backup**: DungeonData es un ScriptableObject → Duplica para versiones

---

## Solución de Problemas Comunes

❌ **"No dungeon data selected"**
→ Click "Create New" o arrastra un DungeonData existente

❌ **"No generated dungeon found"**
→ Genera primero con "Generate Dungeon"

❌ **Las paredes se ven raras**
→ Ajusta `Tunnel Segments` (4-12) en Properties

❌ **Los prefabs están mal escalados**
→ Asegúrate que el pivot esté correcto en tus modelos

---

## Próximos Pasos

1. **Lee el README.md completo** para features avanzadas
2. **Experimenta** con diferentes layouts
3. **Crea tus prefabs** y usa Replace Meshes
4. **Comparte** tus creaciones!

---

**¿Dudas?** Lee `README.md` para documentación completa

**¡A crear mazmorras épicas! 🏰**
