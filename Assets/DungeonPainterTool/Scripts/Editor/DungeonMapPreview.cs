using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using DungeonPainter.Data;

namespace DungeonPainter.Editor
{
    /// <summary>
    /// Dibuja un preview top-down estilo mazmorra de papel en la esquina inferior izquierda
    /// del canvas del editor. Se actualiza en tiempo real mientras se pinta.
    /// </summary>
    public static class DungeonMapPreview
    {
        // ── Configuración del panel ────────────────────────────────
        private const float PANEL_W      = 220f;
        private const float PANEL_H      = 220f;
        private const float MARGIN       = 12f;
        private const float HEADER_H     = 18f;
        private const float PADDING      = 8f;
        private const float WALL_T       = 2.5f;   // grosor de paredes en px
        private const float SHADOW_OFF   = 3f;     // offset de sombra interior

        // Colores estilo D&D / papel de cuadrícula
        private static readonly Color COL_BG          = new Color(0.85f, 0.78f, 0.62f);  // papel envejecido
        private static readonly Color COL_GRID        = new Color(0.55f, 0.70f, 0.85f, 0.35f); // líneas azul claro
        private static readonly Color COL_FLOOR       = new Color(0.93f, 0.88f, 0.72f);  // suelo habitación
        private static readonly Color COL_FLOOR_DARK  = new Color(0.80f, 0.74f, 0.58f);  // sombra interior
        private static readonly Color COL_WALL        = new Color(0.28f, 0.22f, 0.18f);  // paredes oscuras
        private static readonly Color COL_CORRIDOR    = new Color(0.88f, 0.83f, 0.67f);  // pasillo
        private static readonly Color COL_NODE        = new Color(0.4f,  0.3f,  0.2f);   // nodo
        private static readonly Color COL_CONN        = new Color(0.35f, 0.28f, 0.20f);  // línea conexión
        private static readonly Color COL_HEADER_BG   = new Color(0.20f, 0.15f, 0.10f, 0.85f);
        private static readonly Color COL_PANEL_BORDER = new Color(0.25f, 0.18f, 0.12f);

        // ── Estado interno ─────────────────────────────────────────
        private static bool   _collapsed = false;
        private static bool   _visible   = true;

        // ── Entry point ────────────────────────────────────────────
        public static void Draw(DungeonData data, Rect canvasRect, int currentLevel,
                                Dictionary<int, Color> heightColors)
        {
            if (data == null) return;

            // Panel rect — esquina inferior izquierda, encima del tooltip
            float panelH = _collapsed ? HEADER_H + 4 : PANEL_H;
            Rect panelRect = new Rect(
                canvasRect.x + MARGIN,
                canvasRect.yMax - panelH - 38f,   // 38 = espacio del tooltip
                PANEL_W,
                panelH);

            // Sombra del panel
            EditorGUI.DrawRect(new Rect(panelRect.x + 3, panelRect.y + 3, panelRect.width, panelRect.height),
                new Color(0, 0, 0, 0.4f));

            // Fondo papel
            if (!_collapsed)
                EditorGUI.DrawRect(panelRect, COL_BG);

            // Header
            Rect headerRect = new Rect(panelRect.x, panelRect.y, panelRect.width, HEADER_H + 4);
            EditorGUI.DrawRect(headerRect, COL_HEADER_BG);

            // Borde del panel
            DrawBorder(panelRect, COL_PANEL_BORDER);

            // Header labels + toggle
            GUIStyle headerStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            headerStyle.normal.textColor = new Color(0.85f, 0.78f, 0.62f);

            GUI.Label(new Rect(panelRect.x + 6, panelRect.y + 2, panelRect.width - 30, HEADER_H),
                "🗺  PREVIEW MAZMORRA", headerStyle);

            // Botón colapsar
            GUIStyle btnStyle = new GUIStyle(EditorStyles.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter };
            btnStyle.normal.textColor = new Color(0.85f, 0.78f, 0.62f);
            if (GUI.Button(new Rect(panelRect.xMax - 22, panelRect.y + 2, 18, HEADER_H), _collapsed ? "▲" : "▼", btnStyle))
                _collapsed = !_collapsed;

            if (_collapsed) return;

            // Área de dibujo del mapa
            Rect drawRect = new Rect(
                panelRect.x + PADDING,
                panelRect.y + HEADER_H + PADDING + 4,
                panelRect.width  - PADDING * 2,
                panelRect.height - HEADER_H - PADDING * 2 - 4);

            DrawMapContent(data, drawRect, currentLevel, heightColors);
        }

        // ── Contenido del mapa ─────────────────────────────────────
        private static void DrawMapContent(DungeonData data, Rect drawRect,
                                           int currentLevel, Dictionary<int, Color> heightColors)
        {
            if (data.rooms.Count == 0 && data.nodes.Count == 0)
            {
                GUIStyle emptyStyle = new GUIStyle(EditorStyles.label)
                    { fontSize = 9, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                emptyStyle.normal.textColor = new Color(0.4f, 0.3f, 0.2f);
                GUI.Label(drawRect, "Pinta habitaciones\npara ver el mapa", emptyStyle);
                return;
            }

            // ── Calcular bounding box de todo el contenido ─────────
            Vector2Int mn = new Vector2Int(int.MaxValue, int.MaxValue);
            Vector2Int mx = new Vector2Int(int.MinValue, int.MinValue);

            foreach (var room in data.rooms)
                foreach (var cell in room.gridCells)
                {
                    mn.x = Mathf.Min(mn.x, cell.x); mn.y = Mathf.Min(mn.y, cell.y);
                    mx.x = Mathf.Max(mx.x, cell.x); mx.y = Mathf.Max(mx.y, cell.y);
                }
            foreach (var node in data.nodes)
            {
                mn.x = Mathf.Min(mn.x, node.gridPosition.x); mn.y = Mathf.Min(mn.y, node.gridPosition.y);
                mx.x = Mathf.Max(mx.x, node.gridPosition.x); mx.y = Mathf.Max(mx.y, node.gridPosition.y);
            }

            if (mn.x == int.MaxValue) return;

            // Añadir margen de 1 celda
            mn -= Vector2Int.one;
            mx += Vector2Int.one;

            int spanX = Mathf.Max(1, mx.x - mn.x + 1);
            int spanY = Mathf.Max(1, mx.y - mn.y + 1);

            float cellPx = Mathf.Min(drawRect.width / spanX, drawRect.height / spanY);
            cellPx = Mathf.Clamp(cellPx, 2f, 18f);

            // Centrar en el área de dibujo
            float totalW = spanX * cellPx;
            float totalH = spanY * cellPx;
            float offX = drawRect.x + (drawRect.width  - totalW) * 0.5f;
            float offY = drawRect.y + (drawRect.height - totalH) * 0.5f;

            // ── Grid de fondo (líneas azul pálido) ─────────────────
            if (cellPx >= 5f)
            {
                Handles.BeginGUI();
                Handles.color = COL_GRID;
                for (int gx = 0; gx <= spanX; gx++)
                {
                    float px = offX + gx * cellPx;
                    Handles.DrawLine(new Vector3(px, offY), new Vector3(px, offY + totalH));
                }
                for (int gy = 0; gy <= spanY; gy++)
                {
                    float py = offY + gy * cellPx;
                    Handles.DrawLine(new Vector3(offX, py), new Vector3(offX + totalW, py));
                }
                Handles.EndGUI();
            }

            // Helper: grid pos → screen rect en el preview
            System.Func<Vector2Int, Rect> cellRect = (Vector2Int gp) =>
            {
                float px = offX + (gp.x - mn.x) * cellPx;
                float py = offY + (gp.y - mn.y) * cellPx;
                return new Rect(px, py, cellPx, cellPx);
            };

            // ── Dibujar habitaciones ────────────────────────────────
            // Paso 1: suelo base
            var allCells = new HashSet<Vector2Int>();
            foreach (var room in data.rooms)
                foreach (var cell in room.gridCells)
                    allCells.Add(cell);

            foreach (var room in data.rooms)
            {
                bool isCurrentLayer = room.heightLevel == currentLevel;
                foreach (var cell in room.gridCells)
                {
                    Rect cr = cellRect(cell);
                    Color floorCol = isCurrentLayer ? COL_FLOOR : Color.Lerp(COL_FLOOR, COL_BG, 0.55f);
                    EditorGUI.DrawRect(cr, floorCol);

                    // Sombra interior (borde izquierdo y superior más oscuro)
                    if (cellPx >= 5f)
                    {
                        Color shadowCol = isCurrentLayer ? COL_FLOOR_DARK : Color.Lerp(COL_FLOOR_DARK, COL_BG, 0.55f);
                        EditorGUI.DrawRect(new Rect(cr.x, cr.y, SHADOW_OFF, cr.height), shadowCol);
                        EditorGUI.DrawRect(new Rect(cr.x, cr.y, cr.width, SHADOW_OFF),  shadowCol);
                    }
                }
            }

            // Paso 2: paredes (bordes entre celda de sala y vacío)
            if (cellPx >= 4f)
            {
                Handles.BeginGUI();
                Handles.color = COL_WALL;

                Vector2Int[] dirs = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
                // dir → qué borde de la celda dibujar (start offset, end offset respecto a cellRect)
                // right  → borde derecho
                // up     → borde superior  (Y invertida en screen)
                // left   → borde izquierdo
                // down   → borde inferior

                foreach (var room in data.rooms)
                {
                    foreach (var cell in room.gridCells)
                    {
                        Rect cr = cellRect(cell);
                        float t = Mathf.Max(1f, WALL_T * (cellPx / 10f));

                        // Borde derecho
                        if (!allCells.Contains(cell + Vector2Int.right))
                            DrawThickLine(new Vector2(cr.xMax, cr.y), new Vector2(cr.xMax, cr.yMax), t);
                        // Borde inferior (abajo en screen = +Y en grid)
                        if (!allCells.Contains(cell + Vector2Int.up))
                            DrawThickLine(new Vector2(cr.x, cr.yMax), new Vector2(cr.xMax, cr.yMax), t);
                        // Borde izquierdo
                        if (!allCells.Contains(cell + Vector2Int.left))
                            DrawThickLine(new Vector2(cr.x, cr.y), new Vector2(cr.x, cr.yMax), t);
                        // Borde superior (arriba en screen = -Y en grid)
                        if (!allCells.Contains(cell + Vector2Int.down))
                            DrawThickLine(new Vector2(cr.x, cr.y), new Vector2(cr.xMax, cr.y), t);
                    }
                }
                Handles.EndGUI();
            }

            // ── Dibujar conexiones (pasillos) ───────────────────────
            if (data.connections != null && cellPx >= 3f)
            {
                Handles.BeginGUI();
                Handles.color = COL_CONN;
                foreach (var conn in data.connections)
                {
                    var nA = data.GetNode(conn.nodeAId);
                    var nB = data.GetNode(conn.nodeBId);
                    if (nA == null || nB == null) continue;

                    Rect rA = cellRect(nA.gridPosition);
                    Rect rB = cellRect(nB.gridPosition);
                    Vector2 pA = rA.center;
                    Vector2 pB = rB.center;

                    // Pasillo como línea gruesa
                    float corridorW = Mathf.Max(2f, cellPx * 0.4f);
                    DrawThickLine(pA, pB, corridorW);

                    // Suelo del pasillo encima (color más claro)
                    Handles.color = COL_CORRIDOR;
                    float innerW = Mathf.Max(1f, corridorW - 1.5f);
                    DrawThickLine(pA, pB, innerW);
                    Handles.color = COL_CONN;
                }
                Handles.EndGUI();
            }

            // ── Dibujar nodos ───────────────────────────────────────
            if (data.nodes != null && cellPx >= 4f)
            {
                Handles.BeginGUI();
                foreach (var node in data.nodes)
                {
                    Rect nr = cellRect(node.gridPosition);
                    float r = Mathf.Max(2f, cellPx * 0.18f);
                    Handles.color = COL_NODE;
                    Handles.DrawSolidDisc(nr.center, Vector3.forward, r);
                }
                Handles.EndGUI();
            }

            // ── Label de escala ─────────────────────────────────────
            if (data.gridCellSize > 0 && cellPx >= 6f)
            {
                GUIStyle scaleStyle = new GUIStyle(EditorStyles.label) { fontSize = 8 };
                scaleStyle.normal.textColor = new Color(0.3f, 0.2f, 0.1f, 0.7f);
                GUI.Label(new Rect(drawRect.x, drawRect.yMax - 12, drawRect.width, 12),
                    $"1 celda = {data.gridCellSize:0}m", scaleStyle);
            }
        }

        // ── Helpers ────────────────────────────────────────────────
        private static void DrawBorder(Rect r, Color col)
        {
            Handles.BeginGUI();
            Handles.color = col;
            Handles.DrawLine(new Vector3(r.xMin, r.yMin), new Vector3(r.xMax, r.yMin));
            Handles.DrawLine(new Vector3(r.xMax, r.yMin), new Vector3(r.xMax, r.yMax));
            Handles.DrawLine(new Vector3(r.xMax, r.yMax), new Vector3(r.xMin, r.yMax));
            Handles.DrawLine(new Vector3(r.xMin, r.yMax), new Vector3(r.xMin, r.yMin));
            Handles.EndGUI();
        }

        private static void DrawThickLine(Vector2 a, Vector2 b, float thickness)
        {
            // IMGUI no tiene líneas con grosor, lo simulamos con un quad rotado
            Vector2 dir  = (b - a).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x) * thickness * 0.5f;
            Vector3[] verts = {
                a + perp, b + perp, b - perp, a - perp
            };
            Handles.DrawSolidRectangleWithOutline(verts, Handles.color, Color.clear);
        }
    }
}
