# Installation Instructions

## Requisitos

- **Unity Version**: 2020.3 o superior
- **Platforms**: Windows, Mac, Linux (Editor only)
- **Dependencies**: Ninguna (100% Unity nativo)

---

## Método 1: Instalación Manual (Recomendado)

### Paso 1: Descargar
1. Descarga la carpeta completa `DungeonPainterTool`
2. Extrae si es necesario

### Paso 2: Importar a Unity
1. Abre tu proyecto de Unity
2. Navega a `Assets/` en el Project window
3. Arrastra la carpeta `DungeonPainterTool` a `Assets/`
4. Unity importará automáticamente todos los scripts

### Paso 3: Verificar
1. Ve a `Window` en la barra de menú superior
2. Deberías ver `Dungeon Painter` en la lista
3. Click en `Dungeon Painter` para abrir la ventana

### Paso 4: (Opcional) Reorganizar
Puedes mover la carpeta a cualquier lugar dentro de `Assets/`:
```
Assets/
├── DungeonPainterTool/     ← Aquí está por defecto
├── Tools/
│   └── DungeonPainterTool/ ← O aquí
└── ThirdParty/
    └── DungeonPainterTool/ ← O aquí
```

---

## Método 2: Unity Package Manager (Avanzado)

### Desde Git URL
1. Abre Unity Package Manager (`Window → Package Manager`)
2. Click `+` → `Add package from git URL`
3. Pega: `https://github.com/tuusuario/dungeon-painter-tool.git`
4. Click `Add`

### Desde Disco Local
1. Copia `DungeonPainterTool` a una ubicación fuera del proyecto
2. Abre Unity Package Manager
3. Click `+` → `Add package from disk`
4. Navega a `DungeonPainterTool/package.json`
5. Click `Open`

---

## Estructura de Archivos

Después de importar, deberías tener:

```
Assets/DungeonPainterTool/
│
├── Scripts/
│   ├── Core/
│   │   ├── DungeonUtilities.cs          # Validación y estadísticas
│   │   └── DungeonImportExport.cs       # JSON import/export
│   │
│   ├── Data/
│   │   ├── DungeonData.cs               # Estructura de datos principal
│   │   └── MeshReplacementSet.cs        # Prefabs para reemplazo
│   │
│   ├── Editor/
│   │   └── DungeonPainterWindow.cs      # Ventana del editor
│   │
│   └── Generation/
│       ├── DungeonGenerator.cs          # Generador de geometría
│       └── DungeonMeshReplacer.cs       # Sistema de reemplazo
│
├── Examples/
│   └── DungeonExample.cs                # Ejemplos de uso
│
├── Prefabs/                              # Tus prefabs van aquí
├── README.md                             # Documentación completa
├── QUICKSTART.md                         # Guía rápida
├── IMPLEMENTATION_NOTES.md               # Notas técnicas
└── package.json                          # Manifest del paquete
```

---

## Verificación de Instalación

### Test Rápido

1. **Abrir la Ventana**
   - `Window → Dungeon Painter`
   - ✅ Si se abre, la instalación fue exitosa

2. **Crear Data**
   - En la ventana, click `Create New`
   - Guarda como `TestDungeon.asset`
   - ✅ Si se crea, el sistema funciona

3. **Generar Prueba**
   - En la ventana, mode: `Paint Room`
   - Arrastra para pintar una sala
   - Click `Generate Dungeon`
   - ✅ Si aparece geometría en Scene, todo funciona

### Solución de Problemas de Instalación

❌ **"DungeonPainterWindow not found"**
- Asegúrate que el archivo está en `Assets/.../Scripts/Editor/`
- Verifica que no haya errores de compilación en Console
- Reinicia Unity

❌ **"Missing namespace DungeonPainter.Data"**
- Reimporta todos los scripts
- `Assets → Reimport All`

❌ **"Cannot create ScriptableObject"**
- Verifica que `DungeonData.cs` tiene el atributo `[CreateAssetMenu]`
- Cierra y reabre Unity

---

## Actualización de Versión

### Desde versión anterior:

1. **Backup** de tus DungeonData assets existentes
2. **Borra** la carpeta `DungeonPainterTool` antigua
3. **Importa** la nueva versión
4. **Verifica** que tus assets siguen funcionando

---

## Desinstalación

1. **Backup** de tus dungeons (opcional):
   - Exporta a JSON: `Assets → Dungeon Painter → Export to JSON`
   
2. **Eliminar** la carpeta:
   - Click derecho en `DungeonPainterTool` → `Delete`
   
3. **Limpiar** meta files:
   - Unity limpiará automáticamente

---

## Configuración Inicial Recomendada

### 1. Crear Carpeta de Dungeons
```
Assets/
└── Dungeons/
    ├── Data/        ← DungeonData assets
    ├── Prefabs/     ← Mesh replacements
    └── Generated/   ← GameObjects generados
```

### 2. Configurar Settings (Opcional)

Crea tu primer MeshReplacementSet:
1. `Assets → Create → Dungeon Painter → Mesh Replacement Set`
2. Guarda en `Assets/Dungeons/Prefabs/DefaultSet.asset`
3. Asigna tus prefabs de suelo, paredes, etc.

### 3. Template Inicial

Usa el script de ejemplo:
1. Crea Empty GameObject en Scene
2. Add Component → `DungeonExample`
3. Arrastra un DungeonData al campo
4. Inspector → Context Menu → `Create Example Dungeon`
5. Abre Dungeon Painter para ver el resultado

---

## Integración con Proyectos Existentes

### Con ProBuilder
✅ Compatible - Puedes usar ProBuilder para editar meshes generadas

### Con Gaia/Terrain Tools
✅ Compatible - Los dungeons son GameObjects normales

### Con Navigation/NavMesh
✅ Compatible - Marca suelos como `Walkable` y bake

### Con Procedural Toolkit
✅ Compatible - Combínalos para más variedad

---

## Rendimiento y Optimización

### Para Proyectos Pequeños
- Default settings funcionan bien
- Cell Size: 3-5m
- Tunnel Segments: 8

### Para Proyectos Grandes (100+ habitaciones)
- Cell Size: 10m+
- Tunnel Segments: 4-6
- Considera mesh combining después de generar

### Para Builds
- Marca GameObjects generados como `Static`
- Usa Occlusion Culling
- Considera baking lights

---

## Siguiente Paso

Lee el [QUICKSTART.md](QUICKSTART.md) para crear tu primera mazmorra en 5 minutos!

O lee el [README.md](README.md) para documentación completa.

---

## Soporte

- **Issues**: Reporta bugs o problemas
- **Documentación**: Lee los archivos .md incluidos
- **Ejemplos**: Revisa `Examples/DungeonExample.cs`

---

**¡Instalación Completa! 🎉**

Ahora ve a `Window → Dungeon Painter` y empieza a crear.
