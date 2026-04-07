# Guía: Generación de Meshes y Reemplazo

## ¿Por Qué Veo Errores en la Consola?

Los errores de `NullReferenceException` que ves son normales si **no has asignado un MeshReplacementSet**. ¡Esto es completamente opcional!

## Cómo Funciona el Sistema

### 1. Generación Básica (Sin MeshReplacementSet)

Cuando das click en **"Generate Dungeon"** sin tener un MeshReplacementSet asignado:

✅ **Esto ES Normal:**
- El generador crea geometría usando **primitivas de Unity** (cubos, planos)
- Los suelos son **quads simples**
- Las paredes son **meshes procedurales** con forma de túnel
- Todo funciona perfectamente

❌ **NO Necesitas:**
- MeshReplacementSet para que funcione
- Prefabs personalizados
- Modelos 3D especiales

### 2. Reemplazo de Meshes (Opcional)

El botón **"Replace Meshes"** es para **después** de generar, cuando quieras cambiar los cubos básicos por tus propios modelos 3D.

## Pasos Correctos

### Para Tu Primera Mazmorra:

```
1. Crea DungeonData (Create New)
2. Diseña tu layout (Paint Room, Connect Nodes, etc.)
3. Click "Generate Dungeon"
   → Aparece geometría básica en la escena ✅
4. ¡Listo! Ya tienes tu mazmorra
```

### Si Quieres Usar Modelos Custom (Opcional):

```
5. Crea tus prefabs de suelo, paredes, etc.
6. Create → Dungeon Painter → Mesh Replacement Set
7. Asigna tus prefabs al MeshReplacementSet
8. Asigna el MeshReplacementSet al DungeonData
9. Click "Replace Meshes"
   → Los cubos se reemplazan por tus modelos ✅
```

## Errores Comunes y Soluciones

### Error: "NullReferenceException: Object reference not set to an instance of an object"

**Causa**: El código intenta acceder a algo que no existe (usualmente el MeshReplacementSet).

**Solución**: 
- ✅ Ignora este error si solo quieres usar primitivas básicas
- ✅ O asigna un MeshReplacementSet si quieres usar modelos custom

### Error: "Dungeon generated with 0 rooms and 0 connections"

**Causa**: No has diseñado nada en el editor antes de generar.

**Solución**:
1. Mode: Paint Room → Dibuja una sala
2. Luego genera

### El botón "Replace Meshes" no hace nada

**Causa**: No hay un MeshReplacementSet asignado.

**Solución**:
- Esto es normal si no tienes modelos custom
- Las primitivas de Unity ya están en la escena y funcionan

## ¿Qué es un MeshReplacementSet?

Es un **asset opcional** que le dice a la tool qué prefabs usar para reemplazar la geometría básica.

### Campos del MeshReplacementSet:

- **Floor Prefab**: Reemplaza los suelos planos
- **Wall Prefab**: Reemplaza las paredes de túnel
- **Ramp Prefab**: Reemplaza las rampas
- **Stairs Prefab**: Reemplaza las escaleras
- **Corridor Floor Prefab**: Reemplaza suelos de pasillos
- Etc.

Todos son **opcionales**. Si no asignas nada, se usan las primitivas.

## Ejemplo Completo Sin Modelos Custom

```csharp
// Workflow mínimo:

1. Window → Dungeon Painter

2. "Create New" → Guarda como MiMazmorra.asset

3. Mode: Paint Room
   - Arrastra para pintar sala de 5x5

4. Mode: Paint Room
   - Height Level: -1
   - Arrastra otra sala

5. Mode: Place Node
   - Click en centro de primera sala
   - Click en centro de segunda sala

6. Mode: Connect Nodes
   - Click en primer nodo
   - Click en segundo nodo
   - Se crea rampa automáticamente

7. "Generate Dungeon"
   → Aparece en Scene con cubos y meshes básicos ✅

8. Añade enemigos, luces, decoración manualmente

¡TERMINADO! No necesitas Replace Meshes.
```

## Ejemplo Con Modelos Custom

```csharp
// Si quieres tus propios modelos:

1-7. (Igual que arriba)

8. Crea prefabs:
   - Assets/Prefabs/FloorTile.prefab
   - Assets/Prefabs/WallSection.prefab

9. Create → Dungeon Painter → Mesh Replacement Set
   - Asigna FloorTile a "Floor Prefab"
   - Asigna WallSection a "Wall Prefab"

10. En DungeonData properties:
    - Asigna tu MeshReplacementSet

11. "Replace Meshes"
    → Los cubos se convierten en tus modelos ✅

12. Si no te gusta:
    "Restore Original" → Vuelve a cubos
```

## Resumen Rápido

| Pregunta | Respuesta |
|----------|-----------|
| ¿Necesito MeshReplacementSet? | No, es opcional |
| ¿Puedo usar solo primitivas? | Sí, totalmente |
| ¿Cuándo uso Replace Meshes? | Solo si tienes modelos custom |
| ¿Los errores son graves? | No si solo usas primitivas |
| ¿Funciona sin prefabs? | Sí, perfectamente |

## Notas Importantes

✅ **La tool está diseñada para funcionar con o sin modelos custom**
✅ **Las primitivas de Unity son suficientes para testear y prototipar**
✅ **Puedes añadir modelos custom más tarde sin regenerar**
✅ **El botón "Restore Original" vuelve a las primitivas si algo sale mal**

## Arreglando Errores de Consola

Los errores NullReference que ves actualmente son porque el código intenta verificar si hay un MeshReplacementSet asignado. He corregido estos errores en la última versión del archivo.

### Para Limpiar la Consola:

1. Click derecho en la consola → Clear
2. Regenera tu dungeon
3. Los errores deberían desaparecer

Si sigues viendo errores:
- Asegúrate de tener un DungeonData creado y seleccionado
- Verifica que hayas pintado al menos una sala antes de generar

---

**¿Más Dudas?**

- Lee el README.md para documentación completa
- Los errores actuales están corregidos en el archivo actualizado
- MeshReplacementSet es 100% opcional

**¡Disfruta creando mazmorras con primitivas! 🏰**
