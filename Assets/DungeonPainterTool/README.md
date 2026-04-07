# Dungeon Painter Tool for Unity

Una herramienta de editor para Unity que te permite diseñar mazmorras orgánicas tipo Danmachi dibujando en una cuadrícula, con soporte para múltiples niveles de altura, pasillos con ancho variable, rampas, escaleras y túneles.

## Características

- ✅ **Sistema de Cuadrícula Visual**: Dibuja tu mazmorra en una interfaz intuitiva de cuadrícula
- ✅ **Múltiples Niveles de Altura**: Crea mazmorras con diferentes niveles usando un sistema de colores
- ✅ **Conexiones Inteligentes**: Rampas, escaleras y túneles que se adaptan automáticamente a diferencias de altura
- ✅ **Ancho Variable**: Los pasillos pueden cambiar de ancho a lo largo de su recorrido
- ✅ **Paredes Curvadas**: Túneles con forma ovalada estilo mazmorras naturales
- ✅ **Reemplazo de Meshes**: Intercambia cubos básicos por tus propios modelos 3D sin regenerar el nivel
- ✅ **No Runtime**: Todo se genera en el editor, perfecto para handcrafting

## Instalación

1. **Importar a Unity**:
   - Descarga el proyecto
   - Copia la carpeta `DungeonPainterTool` a tu proyecto de Unity en `Assets/`
   
2. **Estructura del Proyecto**:
   ```
   Assets/DungeonPainterTool/
   ├── Scripts/
   │   ├── Core/           (Futuros scripts de runtime)
   │   ├── Data/           (DungeonData, MeshReplacementSet)
   │   ├── Editor/         (DungeonPainterWindow)
   │   └── Generation/     (DungeonGenerator, MeshReplacer)
   ├── Prefabs/            (Tus prefabs personalizados)
   └── Examples/           (Ejemplos de mazmorras)
   ```

3. **Verificar Instalación**:
   - En Unity, ve a `Window → Dungeon Painter`
   - Deberías ver la ventana del editor

## Guía de Uso Rápido

### 1. Crear un Dungeon Data

1. Abre la ventana: `Window → Dungeon Painter`
2. Click en **"Create New"**
3. Guarda el asset en tu proyecto (ejemplo: `Assets/Dungeons/MiMazmorra.asset`)

### 2. Configurar la Cuadrícula

- **Cell Size**: Tamaño de cada celda en metros (mínimo 1x1)
- **Height Level**: Nivel de altura actual (0 = base, -1 = abajo, 1 = arriba)
- Los colores indican la altura:
  - Gris oscuro = niveles bajos (-3, -2, -1)
  - Blanco = nivel base (0)
  - Azul claro = niveles altos (1, 2, 3)

### 3. Modos de Herramienta

#### **Place Node** (Colocar Nodos)
- Click en la cuadrícula para colocar nodos
- Los nodos son puntos de conexión

#### **Connect Nodes** (Conectar Nodos)
- Click en un nodo para empezar
- Click en otro nodo para crear una conexión
- La tool detecta automáticamente si necesita rampa/escaleras

#### **Paint Room** (Pintar Sala)
- Click y arrastra para pintar celdas
- Suelta para crear la sala

#### **Define Room** (Definir Sala Numérica)
- En el panel derecho, define tamaño y posición
- Click "Create Room"

#### **Edit Connection** (Editar Conexión)
- Selecciona una conexión
- Ajusta ancho, tipo de transición, pendiente

#### **Select/Move** (Seleccionar)
- Click en elementos para seleccionarlos
- Ver/editar propiedades en el panel derecho

#### **Delete** (Borrar)
- Click en elementos para eliminarlos

### 4. Trabajar con Alturas

Para crear niveles a diferentes alturas:

1. Cambia el **Height Level** (ej: -1 para un nivel más bajo)
2. Coloca nodos o pinta salas en ese nivel
3. Conecta nodos de diferentes alturas
4. La tool creará automáticamente rampas/escaleras

**Ejemplo**: Sala en nivel 0 conectada a sala en nivel -2
```
Nivel 0: [Sala A] → Rampa descendente → Nivel -2: [Sala B]
```

### 5. Tipos de Conexión

- **Flat**: Pasillo plano (mismo nivel)
- **Ramp**: Rampa suave (para diferencias de altura)
- **Stairs**: Escaleras (control preciso de escalones)
- **Tunnel**: Túnel encerrado (con paredes y techo)

### 6. Ancho Variable de Pasillos

En las propiedades de una conexión seleccionada:

- **Width Points**: Lista de puntos de ancho
  - `Position` (0.0 - 1.0): Posición a lo largo del pasillo
  - `Width`: Ancho en ese punto
  
**Ejemplo**: Pasillo que se ensancha
```
Point 0: Position=0.0, Width=3m  (inicio estrecho)
Point 1: Position=0.5, Width=8m  (medio ancho)
Point 2: Position=1.0, Width=3m  (final estrecho)
```

### 7. Generar la Mazmorra

1. Click en **"Generate Dungeon"**
2. Se crea un GameObject en la escena con geometría básica (cubos)
3. Ahora puedes:
   - Inspeccionar el resultado
   - Añadir enemigos, trampas, decoración manualmente

### 8. Reemplazar Meshes

#### A. Crear Mesh Replacement Set

1. En el Project: `Create → Dungeon Painter → Mesh Replacement Set`
2. Asigna tus prefabs personalizados:
   - `Floor Prefab`: Suelo de salas
   - `Wall Prefab`: Paredes de túnel
   - `Ramp Prefab`: Rampas personalizadas
   - `Stairs Prefab`: Escaleras personalizadas

#### B. Asignar al Dungeon Data

1. En las propiedades generales, asigna tu `Mesh Set`

#### C. Reemplazar

1. Click en **"Replace Meshes"**
2. Los cubos básicos se reemplazan por tus modelos
3. Los originales se desactivan (no se borran)

#### D. Restaurar Originales

- Click en **"Restore Original"** si quieres volver a los cubos básicos

## Controles del Editor

### Navegación
- **Middle Mouse Drag**: Pan (mover vista)
- **Alt + Left Mouse Drag**: Pan alternativo
- **Scroll Wheel**: Zoom in/out
- **Right Click**: (Reservado para menú contextual)

### Atajos de Teclado
- `Space`: Centrar vista (futuro)
- `Delete`: Borrar elemento seleccionado (futuro)

## Flujo de Trabajo Recomendado

### Para una Mazmorra Nueva

```
1. Create New Dungeon Data
2. Configurar Cell Size (ej: 5m)
3. Diseñar Layout:
   a. Paint Room para salas grandes
   b. Place Node para puntos de conexión
   c. Connect Nodes para crear pasillos
4. Añadir Variedad:
   a. Cambiar Height Level y crear niveles bajos/altos
   b. Conectar niveles diferentes (rampas/escaleras)
   c. Ajustar anchos de pasillos
5. Generate Dungeon
6. Inspeccionar y ajustar layout si es necesario
7. Replace Meshes con tus modelos
8. Handcraft: Añadir enemigos, loot, decoración
```

### Para Iterar en un Diseño

```
1. Hacer cambios en el editor (añadir salas, mover nodos, etc.)
2. Delete el GameObject generado anterior
3. Generate Dungeon de nuevo
4. Replace Meshes (si ya tienes tus modelos)
5. Repetir hasta estar satisfecho
```

## Ejemplos

### Ejemplo 1: Sala Simple con Pasillo

```
1. Mode: Paint Room
2. Dibujar sala rectangular (10x10 celdas) en nivel 0
3. Mode: Place Node
4. Colocar nodo en el centro de la sala
5. Colocar nodo fuera de la sala
6. Mode: Connect Nodes
7. Conectar ambos nodos
8. Generate Dungeon
```

### Ejemplo 2: Dos Niveles Conectados

```
1. Mode: Paint Room, Height Level: 0
2. Dibujar Sala A (nivel base)
3. Height Level: -2
4. Dibujar Sala B (dos niveles abajo)
5. Mode: Place Node
6. Nodo en Sala A (nivel 0)
7. Nodo en Sala B (nivel -2)
8. Mode: Connect Nodes
9. Conectar nodos → Se crea rampa automáticamente
10. Seleccionar conexión, cambiar a "Stairs" si prefieres escaleras
11. Generate Dungeon
```

### Ejemplo 3: Pasillo con Ancho Variable

```
1. Crear dos nodos y conectarlos
2. Mode: Select/Move
3. Click en la conexión
4. En Properties:
   - Añadir Width Point: Position=0.0, Width=2m
   - Añadir Width Point: Position=0.3, Width=6m
   - Añadir Width Point: Position=0.7, Width=6m
   - Añadir Width Point: Position=1.0, Width=2m
5. Generate Dungeon → Pasillo ensanchado en el medio
```

## Consejos y Trucos

### Diseño de Layout

- **Empieza grande**: Es más fácil reducir que expandir
- **Usa niveles de altura** para añadir profundidad visual
- **Varía los anchos** de pasillos para romper la monotonía
- **Salas grandes primero**, luego conectores y pasillos

### Optimización

- **Cell Size apropiado**: 
  - 3-5m para mazmorras detalladas
  - 10m+ para mapas grandes
- **Menos segmentos de túnel** (4-8) para performance
- **Combina salas** donde sea posible en vez de muchas pequeñas

### Estilo Danmachi

Para mazmorras orgánicas tipo Danmachi:

1. **Evita ángulos rectos perfectos**: Usa pasillos diagonales
2. **Múltiples alturas**: Crea sensación de profundidad
3. **Anchos variados**: Túneles que se ensanchan y estrechan
4. **Rampas sobre escaleras**: Más naturales
5. **No uses cuadrícula perfecta**: Desplaza elementos ligeramente

## Preguntas Frecuentes

### ¿Puedo usar diagonales?

Sí, al conectar nodos puedes conectar cualquier posición, creando pasillos diagonales.

### ¿Cómo hago salas circulares?

Actualmente solo rectángulos. Para formas complejas:
1. Paint Room con forma aproximada
2. O usa múltiples salas pequeñas
3. Futura versión: custom brushes

### ¿Los meshes reemplazados se ajustan automáticamente?

Sí, la tool intenta escalar tus prefabs para que coincidan con las dimensiones originales. Si necesitas ajuste manual, edita los prefabs.

### ¿Puedo exportar/importar layouts?

El DungeonData es un ScriptableObject, así que puedes:
- Duplicarlo
- Compartirlo entre proyectos
- Versionarlo con Git

### ¿Funciona en runtime?

No, esta tool está diseñada para **editor-time** generation. Los niveles se generan una vez y permanecen en la escena.

## Limitaciones Conocidas

- No hay Undo/Redo (usa Ctrl+Z en el inspector si editas propiedades)
- Salas solo rectangulares/custom pintadas (no círculos/polígonos)
- Paredes de túnel simples (8 segmentos fijos por sección)
- No hay sistema de snapping automático entre salas

## Roadmap / Mejoras Futuras

- [ ] Sistema de Undo/Redo nativo
- [ ] Templates de salas predefinidas
- [ ] Brush system para formas complejas
- [ ] Auto-conexión de salas cercanas
- [ ] Exportar a JSON/XML
- [ ] Prefab variants para variedad
- [ ] Iluminación procedural básica
- [ ] Navmesh generation integrado

## Troubleshooting

### "Dungeon Data is null"
- Asegúrate de crear o seleccionar un DungeonData asset

### "No generated dungeon found"
- Genera primero con "Generate Dungeon" antes de Replace Meshes

### Las paredes se ven raras
- Ajusta `Tunnel Segments` en propiedades generales
- Verifica que tus prefabs tengan las normales correctas

### Los prefabs están en el tamaño incorrecto
- La tool intenta escalar automáticamente
- Verifica que tus prefabs tengan el pivot en el lugar correcto

## Licencia

Este proyecto es de código abierto. Puedes usarlo, modificarlo y distribuirlo libremente.

## Créditos

Inspirado por las mazmorras orgánicas de Danmachi y herramientas como ProBuilder.

---

**¡Feliz Dungeon Crafting! 🏰⚔️**
