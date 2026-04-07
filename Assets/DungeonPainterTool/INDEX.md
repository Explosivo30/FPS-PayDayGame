# Dungeon Painter Tool - Índice de Documentación

## 📚 Documentación Incluida

### 🚀 Para Empezar
- **[INSTALLATION.md](INSTALLATION.md)** - Instrucciones de instalación paso a paso
- **[QUICKSTART.md](QUICKSTART.md)** - Guía de 5 minutos para tu primera mazmorra
- **[README.md](README.md)** - Documentación completa y referencia

### 🔧 Para Desarrolladores
- **[IMPLEMENTATION_NOTES.md](IMPLEMENTATION_NOTES.md)** - Notas técnicas, features, roadmap
- **[Examples/DungeonExample.cs](Examples/DungeonExample.cs)** - Scripts de ejemplo

---

## 📂 Estructura del Proyecto

```
DungeonPainterTool/
│
├── 📄 Documentación
│   ├── README.md                    # Documentación principal
│   ├── QUICKSTART.md               # Guía rápida
│   ├── INSTALLATION.md             # Instalación
│   ├── IMPLEMENTATION_NOTES.md     # Notas técnicas
│   ├── INDEX.md                    # Este archivo
│   └── package.json                # Package manifest
│
├── 📜 Scripts/
│   │
│   ├── Core/                       # Utilidades y sistemas base
│   │   ├── DungeonUtilities.cs        - Validación y estadísticas
│   │   └── DungeonImportExport.cs     - Import/Export JSON
│   │
│   ├── Data/                       # Estructuras de datos
│   │   ├── DungeonData.cs             - Datos principales
│   │   └── MeshReplacementSet.cs      - Prefabs de reemplazo
│   │
│   ├── Editor/                     # Herramientas de editor
│   │   └── DungeonPainterWindow.cs    - Ventana principal
│   │
│   └── Generation/                 # Sistema de generación
│       ├── DungeonGenerator.cs        - Generador de geometría
│       └── DungeonMeshReplacer.cs     - Reemplazo de meshes
│
├── 📦 Prefabs/                     # Tus prefabs custom (vacío)
│
└── 📋 Examples/                    # Ejemplos de uso
    └── DungeonExample.cs              - Script de ejemplo
```

---

## 🎯 Flujo de Aprendizaje Recomendado

### Nivel 1: Usuario Básico (15 minutos)
1. Lee [INSTALLATION.md](INSTALLATION.md) → Instala la tool
2. Lee [QUICKSTART.md](QUICKSTART.md) → Crea tu primera mazmorra
3. Experimenta con los modos de herramienta

### Nivel 2: Usuario Intermedio (1 hora)
1. Lee [README.md](README.md) secciones:
   - Características
   - Guía de Uso Rápido
   - Ejemplos
2. Experimenta con múltiples niveles de altura
3. Prueba ancho variable en pasillos
4. Genera y reemplaza meshes

### Nivel 3: Usuario Avanzado (2-3 horas)
1. Lee [README.md](README.md) completo
2. Estudia [Examples/DungeonExample.cs](Examples/DungeonExample.cs)
3. Crea tus propios prefabs
4. Experimenta con JSON export/import
5. Crea templates reutilizables

### Nivel 4: Desarrollador/Extensor (Según necesidad)
1. Lee [IMPLEMENTATION_NOTES.md](IMPLEMENTATION_NOTES.md)
2. Estudia el código fuente
3. Añade features personalizadas
4. Contribuye mejoras

---

## 🔍 Encontrar Información Rápidamente

### "¿Cómo hago...?"

| Pregunta | Documento | Sección |
|----------|-----------|---------|
| Instalar la tool | INSTALLATION.md | Todo |
| Crear mi primera mazmorra | QUICKSTART.md | Paso 1-4 |
| Cambiar altura de salas | README.md | "Trabajar con Alturas" |
| Crear rampas/escaleras | README.md | "Tipos de Conexión" |
| Variar ancho de pasillos | README.md | "Ancho Variable de Pasillos" |
| Reemplazar meshes | README.md | "Reemplazar Meshes" |
| Exportar a JSON | README.md | FAQ / IMPLEMENTATION_NOTES.md |
| Añadir nuevas features | IMPLEMENTATION_NOTES.md | "Extension Points" |
| Ver roadmap | IMPLEMENTATION_NOTES.md | "Planned Features" |

### "¿Qué significa...?"

| Término | Definición |
|---------|------------|
| **DungeonData** | ScriptableObject que guarda toda la información del layout |
| **Node** | Punto de conexión en la cuadrícula |
| **Connection** | Pasillo/rampa/escalera entre dos nodos |
| **Room** | Área pintada o definida en la cuadrícula |
| **Height Level** | Nivel vertical (0=base, -1=abajo, 1=arriba) |
| **Cell Size** | Tamaño en metros de cada celda de la cuadrícula |
| **Width Point** | Punto que define ancho en una posición del pasillo |
| **MeshReplacementSet** | Asset con prefabs para reemplazar geometría básica |

---

## 📝 Casos de Uso Comunes

### Diseñador de Niveles
**Quiero**: Diseñar layouts de mazmorras visualmente
**Lee**: QUICKSTART.md → README.md (hasta "Generar la Mazmorra")

### Artista 3D
**Quiero**: Reemplazar geometría básica con mis modelos
**Lee**: README.md sección "Reemplazar Meshes"

### Programador Gameplay
**Quiero**: Integrar con sistema de spawns/gameplay
**Lee**: IMPLEMENTATION_NOTES.md → "Gameplay Integration"

### Tech Artist
**Quiero**: Crear variantes procedurales
**Lee**: Examples/DungeonExample.cs → IMPLEMENTATION_NOTES.md

### Project Manager
**Quiero**: Entender capacidades y limitaciones
**Lee**: README.md "Características" y "Limitaciones Conocidas"

---

## 🆘 Troubleshooting Rápido

| Problema | Solución | Documento |
|----------|----------|-----------|
| No aparece en menú Window | Revisar instalación | INSTALLATION.md |
| Errores de compilación | Verificar estructura de carpetas | INSTALLATION.md |
| No genera geometría | Revisar configuración | README.md "Troubleshooting" |
| Meshes mal escalados | Verificar pivot de prefabs | README.md "Troubleshooting" |
| Layout se ve mal | Validar con utilities | IMPLEMENTATION_NOTES.md |

---

## 🎓 Tutoriales por Objetivo

### Tutorial 1: Sala Simple
**Tiempo**: 2 minutos
**Archivo**: QUICKSTART.md
**Aprenderás**: Pintar sala, generar

### Tutorial 2: Multi-Nivel
**Tiempo**: 5 minutos
**Archivo**: QUICKSTART.md → Ejemplo 2
**Aprenderás**: Alturas, rampas

### Tutorial 3: Pasillo Variable
**Tiempo**: 5 minutos
**Archivo**: QUICKSTART.md → Ejemplo 3
**Aprenderás**: Width points, edición

### Tutorial 4: Reemplazo de Meshes
**Tiempo**: 10 minutos
**Archivo**: README.md → "Reemplazar Meshes"
**Aprenderás**: MeshReplacementSet, prefabs

### Tutorial 5: Template System
**Tiempo**: 10 minutos
**Archivo**: README.md → FAQ + IMPLEMENTATION_NOTES.md
**Aprenderás**: JSON export, templates

---

## 📖 Glosario de Archivos

### Archivos de Documentación

- **INDEX.md** (este archivo)
  - Índice general y guía de navegación
  
- **INSTALLATION.md**
  - Cómo instalar en Unity
  - Solución de problemas de instalación
  
- **QUICKSTART.md**
  - Tutorial de 5 minutos
  - Controles básicos
  - Ejemplos mínimos
  
- **README.md**
  - Documentación completa
  - Todas las características
  - Guía de uso detallada
  - FAQ
  
- **IMPLEMENTATION_NOTES.md**
  - Features implementadas
  - Roadmap
  - Deuda técnica
  - Extension points

### Archivos de Código

Todos documentados inline con XML comments. Abre cualquier archivo .cs para ver documentación detallada de cada clase/método.

---

## 🔗 Enlaces Rápidos

### Empezar Ahora
1. [INSTALLATION.md](INSTALLATION.md) - Instalar
2. [QUICKSTART.md](QUICKSTART.md) - Primera mazmorra

### Referencia Completa
- [README.md](README.md) - Todo lo que necesitas saber

### Avanzado
- [IMPLEMENTATION_NOTES.md](IMPLEMENTATION_NOTES.md) - Desarrolladores

---

## 📊 Estadísticas del Proyecto

- **Archivos de Código**: 8 scripts C#
- **Líneas de Código**: ~3000+
- **Archivos de Documentación**: 5 archivos .md
- **Features Core**: 20+
- **Ejemplos Incluidos**: 2

---

## 🎯 Checklist de Primeros Pasos

- [ ] Leer INSTALLATION.md
- [ ] Instalar en Unity
- [ ] Abrir Window → Dungeon Painter
- [ ] Leer QUICKSTART.md
- [ ] Crear primera mazmorra
- [ ] Experimentar con alturas
- [ ] Generar geometría
- [ ] (Opcional) Crear prefabs custom
- [ ] (Opcional) Reemplazar meshes
- [ ] (Opcional) Leer README.md completo

---

**¿Por dónde empiezo?**

→ [INSTALLATION.md](INSTALLATION.md) si aún no has instalado

→ [QUICKSTART.md](QUICKSTART.md) si ya instalaste y quieres empezar YA

→ [README.md](README.md) si quieres entender todo en detalle

---

¡Feliz diseño de mazmorras! 🏰⚔️
