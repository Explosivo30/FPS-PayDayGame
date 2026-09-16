using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using DungeonPainter.Data;
using DungeonPainter.Generation;
using DungeonPainter.Core;

namespace DungeonPainter.Editor
{
    public class DungeonPainterWindow : EditorWindow
    {
        #region Tool Modes
        private enum ToolMode
        {
            PlaceNode, ConnectNodes, PaintRoom,
            DefineRoomNumeric, EditConnection, SelectAndMove, DeleteElement
        }
        #endregion

        #region Fields
        private ToolMode currentMode = ToolMode.PaintRoom;
        private DungeonData dungeonData;
        private SerializedObject serializedData;

        // Visual settings
        private int currentHeightLevel = 0;
        private float currentCorridorWidth = 3f;
        private ConnectionType currentConnectionType = ConnectionType.Flat;

        // Grid rendering
        private Vector2 gridOffset = Vector2.zero;
        private float gridZoom = 1f;
        private const float minZoom = 0.3f;
        private const float maxZoom = 3f;

        // Selection
        private DungeonNode selectedNode;
        private DungeonConnection selectedConnection;
        private DungeonRoom selectedRoom;
        private DungeonNode connectionStartNode;

        // Room painting
        private List<Vector2Int> paintedCells = new List<Vector2Int>();
        private bool isPainting = false;
        private bool previewDirty = false;

        // Numeric room definition
        private Vector2Int roomSizeNumeric = new Vector2Int(5, 5);
        private Vector2Int roomPositionNumeric = Vector2Int.zero;

        // Copy/Paste
        private DungeonRoom copiedRoom;

        // Room Templates
        private List<RoomTemplate> roomTemplates;
        private bool showTemplatePanel = false;
        private Vector2 templateScrollPos;

        // Visual feedback
        private bool showGridCoordinates = true;
        private bool tempPanMode = false;

        // Colors for height levels
        private Dictionary<int, Color> heightColors = new Dictionary<int, Color>()
        {
            {-3, new Color(0.2f, 0.2f, 0.2f)},
            {-2, new Color(0.3f, 0.3f, 0.3f)},
            {-1, new Color(0.5f, 0.5f, 0.5f)},
            {0,  Color.white},
            {1,  new Color(0.7f, 0.9f, 1f)},
            {2,  new Color(0.5f, 0.8f, 1f)},
            {3,  new Color(0.3f, 0.7f, 1f)}
        };

        private GameObject generatedDungeon;
        private Vector2 propertiesScrollPos;

        private static readonly string[] LayerNames = { "B3","B2","B1","Planta Baja","Planta 1","Planta 2","Planta 3" };
        private static string GetLayerName(int level)
        {
            int idx = level + 3;
            return (idx >= 0 && idx < LayerNames.Length) ? LayerNames[idx] : $"Nivel {level}";
        }

        #endregion

        #region Window Setup
        [MenuItem("Window/Dungeon Painter")]
        public static void ShowWindow()
        {
            var w = GetWindow<DungeonPainterWindow>("Dungeon Painter");
            w.minSize = new Vector2(1000, 700);
        }

        private void OnEnable()
        {
            if (dungeonData != null)
            {
                serializedData = new SerializedObject(dungeonData);
                // Try to recover generated dungeon reference after Unity restart
                if (generatedDungeon == null)
                    generatedDungeon = GameObject.Find("Dungeon_" + dungeonData.name);
            }
            if (roomTemplates == null)
                roomTemplates = RoomTemplates.GetBuiltInTemplates();
        }
        #endregion

        #region Main GUI
        private void OnGUI()
        {
            // ── Keyboard shortcuts ──────────────────────────────────
            Event e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                var action = KeyboardShortcuts.ProcessInput(e);
                switch (action)
                {
                    case KeyboardShortcuts.ShortcutAction.PaintRoom:
                        currentMode = ToolMode.PaintRoom;      Repaint(); return;
                    case KeyboardShortcuts.ShortcutAction.PlaceNode:
                        currentMode = ToolMode.PlaceNode;      Repaint(); return;
                    case KeyboardShortcuts.ShortcutAction.ConnectNodes:
                        currentMode = ToolMode.ConnectNodes;   Repaint(); return;
                    case KeyboardShortcuts.ShortcutAction.SelectMove:
                        currentMode = ToolMode.SelectAndMove;  Repaint(); return;
                    case KeyboardShortcuts.ShortcutAction.Delete:
                        currentMode = ToolMode.DeleteElement;  Repaint(); return;
                    case KeyboardShortcuts.ShortcutAction.Copy:
                        CopySelectedRoom(); Repaint(); return;
                    case KeyboardShortcuts.ShortcutAction.Paste:
                        PasteRoom();        Repaint(); return;
                    case KeyboardShortcuts.ShortcutAction.CenterView:
                        CenterView();       Repaint(); return;
                }
            }

            // ── Layout ──────────────────────────────────────────────
            DrawMainToolbar();

            EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));

            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.7f));
            DrawGridCanvas();
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.3f));
            DrawPropertiesPanel();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            DrawActionButtons();
        }

        // ── Toolbar ────────────────────────────────────────────────
        private void DrawMainToolbar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.toolbar);
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField("Dungeon Data:", GUILayout.Width(100));
            DungeonData newData = (DungeonData)EditorGUILayout.ObjectField(dungeonData, typeof(DungeonData), false);
            if (newData != dungeonData)
            {
                dungeonData = newData;
                if (dungeonData != null)
                {
                    serializedData = new SerializedObject(dungeonData);
                    dungeonData.RebuildCache();
                }
            }

            if (GUILayout.Button("Create New", EditorStyles.toolbarButton, GUILayout.Width(100)))
                CreateNewDungeonData();

            if (dungeonData != null)
            {
                GUILayout.Space(20);
                // Layer pill in toolbar
                Color lc = heightColors.ContainsKey(currentHeightLevel)
                    ? heightColors[currentHeightLevel] : Color.white;
                GUIStyle layerStyle = new GUIStyle(EditorStyles.toolbarButton)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize  = 12
                };
                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = new Color(lc.r * 0.7f, lc.g * 0.7f, lc.b * 0.7f, 1f);
                GUILayout.Label(
                    $"  ▪  CAPA ACTIVA: {GetLayerName(currentHeightLevel)}  (nivel {currentHeightLevel})  ▪  ",
                    layerStyle, GUILayout.Height(20));
                GUI.backgroundColor = prev;

                GUILayout.Space(8);
                // Quick layer switcher ± buttons
                if (GUILayout.Button("▼", EditorStyles.toolbarButton, GUILayout.Width(22)))
                { currentHeightLevel--; Repaint(); }
                if (GUILayout.Button("▲", EditorStyles.toolbarButton, GUILayout.Width(22)))
                { currentHeightLevel++; Repaint(); }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        // ── Canvas ─────────────────────────────────────────────────
        private void DrawGridCanvas()
        {
            if (dungeonData == null)
            {
                EditorGUILayout.HelpBox("Create or select a Dungeon Data asset to begin.", MessageType.Info);
                if (GUILayout.Button("Create New Dungeon Data", GUILayout.Height(40)))
                    CreateNewDungeonData();
                return;
            }

            Rect canvasRect = GUILayoutUtility.GetRect(10, 10000, 10, 10000,
                GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));

            EditorGUI.DrawRect(canvasRect, new Color(0.15f, 0.15f, 0.15f));

            HandleCanvasControls(canvasRect);
            DrawGrid(canvasRect);
            DrawRooms(canvasRect);
            DrawConnections(canvasRect);
            DrawNodes(canvasRect);
            HandleMouseInput(canvasRect);
            DrawModeSpecificUI(canvasRect);
            DrawScaleRuler(canvasRect);
            DrawLayerLegend(canvasRect);
            DrawStats(canvasRect);
            DungeonMapPreview.Draw(dungeonData, canvasRect, currentHeightLevel, heightColors);
        }

        // ── Right panel ────────────────────────────────────────────
        private void DrawPropertiesPanel()
        {
            propertiesScrollPos = EditorGUILayout.BeginScrollView(propertiesScrollPos);

            DrawToolPalette();

            EditorGUILayout.Space(4);
            string inspectorTitle = "🔍  INSPECTOR";
            if      (selectedNode       != null) inspectorTitle = "🔍  INSPECTOR — Nodo";
            else if (selectedConnection != null) inspectorTitle = "🔍  INSPECTOR — Conexión";
            else if (selectedRoom       != null) inspectorTitle = "🔍  INSPECTOR — Habitación";
            DrawSectionHeader(inspectorTitle);

            if      (selectedNode       != null) DrawNodeProperties();
            else if (selectedConnection != null) DrawConnectionProperties();
            else if (selectedRoom       != null) DrawRoomProperties();
            else                                 DrawGeneralProperties();

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolPalette()
        {
            if (dungeonData == null) return;

            // ══ HERRAMIENTAS ══════════════════════════════════════
            DrawSectionHeader("🔧  HERRAMIENTAS");

            string[]   toolNames  = { "📍 Nodo","🔗 Conectar","🖌 Pintar Sala","📐 Sala Núm.","✏️ Editar Con.","↔ Seleccionar","🗑 Borrar" };
            ToolMode[] toolValues = { ToolMode.PlaceNode, ToolMode.ConnectNodes, ToolMode.PaintRoom,
                                      ToolMode.DefineRoomNumeric, ToolMode.EditConnection,
                                      ToolMode.SelectAndMove, ToolMode.DeleteElement };

            int selIdx = -1;
            for (int i = 0; i < toolValues.Length; i++)
                if (currentMode == toolValues[i]) selIdx = i;

            int newIdx = GUILayout.SelectionGrid(selIdx, toolNames, 2, GUILayout.Height(110));
            if (newIdx != selIdx && newIdx >= 0)
            {
                currentMode = toolValues[newIdx];
                if (currentMode != ToolMode.SelectAndMove)
                { selectedNode = null; selectedConnection = null; selectedRoom = null; }
            }

            // Tool hint
            string hint = currentMode switch
            {
                ToolMode.PlaceNode         => "Coloca nodos de conexión en el grid. Los pasillos los unen.",
                ToolMode.ConnectNodes      => "Click en nodo A, luego en nodo B para crear un pasillo.",
                ToolMode.PaintRoom         => "Click y arrastra para pintar celdas de habitación.",
                ToolMode.DefineRoomNumeric => "Define el tamaño exacto en celdas desde el Inspector.",
                ToolMode.EditConnection    => "Click en un pasillo para editar su tipo y anchura.",
                ToolMode.SelectAndMove     => "Click para seleccionar y editar propiedades.",
                ToolMode.DeleteElement     => "Click para borrar nodos, salas o conexiones.",
                _ => ""
            };
            if (!string.IsNullOrEmpty(hint))
                EditorGUILayout.HelpBox(hint, MessageType.None);

            // ══ CAPA ACTIVA ════════════════════════════════════════
            EditorGUILayout.Space(4);
            DrawSectionHeader("📐  CAPA ACTIVA");

            Color lc = heightColors.ContainsKey(currentHeightLevel) ? heightColors[currentHeightLevel] : Color.white;
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(lc.r * 0.5f + 0.1f, lc.g * 0.5f + 0.1f, lc.b * 0.5f + 0.1f, 1f);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUIStyle layerBig = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            layerBig.normal.textColor = lc == Color.white ? Color.black : Color.white;
            EditorGUILayout.LabelField(GetLayerName(currentHeightLevel).ToUpper(), layerBig, GUILayout.Height(28));

            float layerH = currentHeightLevel * (dungeonData?.heightPerLevel ?? 3f);
            GUIStyle layerSub = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField($"Nivel {currentHeightLevel}  ·  Altura {layerH:0.#}m sobre el suelo", layerSub);
            EditorGUILayout.EndVertical();
            GUI.backgroundColor = prevBg;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("▼ Bajar capa")) { currentHeightLevel--; Repaint(); }
            if (GUILayout.Button("▲ Subir capa")) { currentHeightLevel++; Repaint(); }
            EditorGUILayout.EndHorizontal();

            currentHeightLevel = EditorGUILayout.IntSlider("Nivel:", currentHeightLevel, -3, 3);

            // ══ PLANTILLAS ════════════════════════════════════════
            EditorGUILayout.Space(4);
            DrawSectionHeader("🏗  PLANTILLAS DE SALA");
            showTemplatePanel = EditorGUILayout.Foldout(showTemplatePanel, "Ver plantillas", true);
            if (showTemplatePanel && roomTemplates != null)
            {
                EditorGUILayout.HelpBox("Click para colocar en el origen. Usa Seleccionar para mover.", MessageType.None);
                templateScrollPos = EditorGUILayout.BeginScrollView(templateScrollPos, GUILayout.Height(120));
                foreach (var tmpl in roomTemplates)
                    if (GUILayout.Button($"{tmpl.name}  ({tmpl.size.x}×{tmpl.size.y} celdas = {tmpl.size.x * dungeonData.gridCellSize:0}×{tmpl.size.y * dungeonData.gridCellSize:0}m)"))
                        PlaceTemplate(tmpl);
                EditorGUILayout.EndScrollView();
            }

            // ══ COPIAR / PEGAR ════════════════════════════════════
            EditorGUILayout.Space(4);
            DrawSectionHeader("📋  COPIAR / PEGAR");
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledGroupScope(selectedRoom == null))
                if (GUILayout.Button("Copiar  (Ctrl+C)")) CopySelectedRoom();
            using (new EditorGUI.DisabledGroupScope(copiedRoom == null))
                if (GUILayout.Button("Pegar  (Ctrl+V)")) PasteRoom();
            EditorGUILayout.EndHorizontal();
            if (copiedRoom != null)
                EditorGUILayout.LabelField($"Portapapeles: {copiedRoom.roomName}", EditorStyles.miniLabel);

            // ══ AJUSTES DEL EDITOR ════════════════════════════════
            EditorGUILayout.Space(4);
            DrawSectionHeader("⚙️  AJUSTES");
            showGridCoordinates = EditorGUILayout.Toggle("Mostrar coordenadas", showGridCoordinates);

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Atajos de teclado..."))
                EditorUtility.DisplayDialog("Atajos", KeyboardShortcuts.GetShortcutHelpText(), "OK");
        }

        private void DrawSectionHeader(string title)
        {
            EditorGUILayout.Space(2);
            Rect r = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, new Color(0.22f, 0.22f, 0.26f));
            GUIStyle hs = new GUIStyle(EditorStyles.boldLabel) { fontSize = 10 };
            hs.normal.textColor = new Color(0.7f, 0.75f, 0.85f);
            GUI.Label(new Rect(r.x + 6, r.y + 3, r.width, r.height), title, hs);
            EditorGUILayout.Space(2);
        }

        // ── Bottom bar ─────────────────────────────────────────────
        private void DrawActionButtons()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            using (new EditorGUI.DisabledGroupScope(dungeonData == null))
            {
                if (GUILayout.Button("Generate Dungeon",  GUILayout.Height(28), GUILayout.Width(150))) GenerateDungeon();
                if (GUILayout.Button("Replace Meshes",    GUILayout.Height(28), GUILayout.Width(130))) ReplaceMeshes();
                if (GUILayout.Button("Restore Original",  GUILayout.Height(28), GUILayout.Width(130))) RestoreOriginal();

                GUILayout.Space(12);
                using (new EditorGUI.DisabledGroupScope(generatedDungeon == null))
                {
                    Color prevBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.3f, 0.6f, 1f);
                    if (GUILayout.Button("⬡  Convertir a ProBuilder", GUILayout.Height(28), GUILayout.Width(180)))
                        ProBuilderConverter.Convert(generatedDungeon);
                    GUI.backgroundColor = prevBg;
                }

                GUILayout.FlexibleSpace();

                if (dungeonData != null)
                    EditorGUILayout.LabelField(
                        $"Rooms: {dungeonData.rooms.Count}  Nodes: {dungeonData.nodes.Count}  Connections: {dungeonData.connections.Count}",
                        GUILayout.Width(280));

                if (GUILayout.Button("Center View (F)", GUILayout.Height(28), GUILayout.Width(110))) CenterView();

                if (GUILayout.Button("Clear All", GUILayout.Height(28), GUILayout.Width(80)))
                    if (EditorUtility.DisplayDialog("Clear All", "Delete everything?", "Yes", "Cancel"))
                    {
                        Undo.RegisterCompleteObjectUndo(dungeonData, "Clear All");
                        dungeonData.ClearAll(); ClearSelection(); Repaint();
                    }
            }

            EditorGUILayout.EndHorizontal();
        }
        #endregion

        #region Grid Drawing
        private void DrawGrid(Rect r)
        {
            float cs = dungeonData.gridCellSize * gridZoom;
            Handles.BeginGUI();
            Handles.color = new Color(0.3f, 0.3f, 0.3f);

            int x0 = Mathf.FloorToInt(-gridOffset.x / cs) - 1;
            int x1 = Mathf.CeilToInt((r.width  - gridOffset.x) / cs) + 1;
            int y0 = Mathf.FloorToInt(-gridOffset.y / cs) - 1;
            int y1 = Mathf.CeilToInt((r.height - gridOffset.y) / cs) + 1;

            for (int x = x0; x <= x1; x++)
            {
                float sx = r.x + x * cs + gridOffset.x;
                Handles.DrawLine(new Vector3(sx, r.y), new Vector3(sx, r.yMax));
            }
            for (int y = y0; y <= y1; y++)
            {
                float sy = r.y + y * cs + gridOffset.y;
                Handles.DrawLine(new Vector3(r.x, sy), new Vector3(r.xMax, sy));
            }

            Handles.color = new Color(0.55f, 0.55f, 0.55f);
            float ox = r.x + gridOffset.x, oy = r.y + gridOffset.y;
            Handles.DrawLine(new Vector3(ox, r.y), new Vector3(ox, r.yMax));
            Handles.DrawLine(new Vector3(r.x, oy), new Vector3(r.xMax, oy));
            Handles.EndGUI();
        }

        private void DrawNodes(Rect canvasRect)
        {
            if (dungeonData.nodes == null) return;
            Handles.BeginGUI();
            foreach (var node in dungeonData.nodes)
            {
                Vector2 sp = GridToScreen(node.gridPosition, canvasRect);
                Color c = heightColors.ContainsKey(node.heightLevel) ? heightColors[node.heightLevel] : Color.white;
                if      (node == selectedNode)                c = Color.yellow;
                else if (node.heightLevel != currentHeightLevel) c = Color.Lerp(c, Color.clear, 0.5f);
                Handles.color = c;
                Handles.DrawSolidDisc(sp, Vector3.forward, 8f);
                Handles.color = Color.black;
                Handles.DrawWireDisc(sp, Vector3.forward, 8f);
                if (node.heightLevel != 0)
                    GUI.Label(new Rect(sp.x - 15, sp.y - 20, 30, 20), node.heightLevel.ToString(), EditorStyles.miniLabel);
            }
            Handles.EndGUI();
        }

        private void DrawConnections(Rect canvasRect)
        {
            if (dungeonData.connections == null) return;
            Handles.BeginGUI();
            foreach (var conn in dungeonData.connections)
            {
                var nA = dungeonData.GetNode(conn.nodeAId);
                var nB = dungeonData.GetNode(conn.nodeBId);
                if (nA == null || nB == null) continue;

                Vector2 pA = GridToScreen(nA.gridPosition, canvasRect);
                Vector2 pB = GridToScreen(nB.gridPosition, canvasRect);
                Color lc = conn == selectedConnection ? Color.cyan : Color.gray;
                switch (conn.transitionType)
                {
                    case ConnectionType.Ramp:   lc = Color.green;   break;
                    case ConnectionType.Stairs: lc = Color.blue;    break;
                    case ConnectionType.Tunnel: lc = Color.magenta; break;
                }
                Handles.color = lc;
                Handles.DrawLine(pA, pB);
                Vector2 mid  = (pA + pB) * 0.5f;
                Vector2 perp = new Vector2(-(pB - pA).normalized.y, (pB - pA).normalized.x);
                Handles.DrawLine(mid, mid + perp * 5f);
            }

            if (currentMode == ToolMode.ConnectNodes && connectionStartNode != null)
            {
                Vector2 sp = GridToScreen(connectionStartNode.gridPosition, canvasRect);
                Handles.color = Color.yellow;
                Handles.DrawDottedLine(sp, Event.current.mousePosition, 3f);
            }
            Handles.EndGUI();
        }

        private void DrawRooms(Rect canvasRect)
        {
            if (dungeonData.rooms == null) return;
            foreach (var room in dungeonData.rooms)
            {
                Color rc = heightColors.ContainsKey(room.heightLevel) ? heightColors[room.heightLevel] : Color.white;
                rc.a = room == selectedRoom ? 0.5f : 0.3f;
                if (room.heightLevel != currentHeightLevel) rc.a *= 0.3f;

                foreach (var cell in room.gridCells)
                    EditorGUI.DrawRect(GetCellScreenRect(cell, canvasRect), rc);

                if (room.gridCells.Count > 0)
                {
                    Handles.BeginGUI();
                    Handles.color = room == selectedRoom ? Color.yellow : Color.white;
                    Vector2Int mn = room.gridCells[0], mx = room.gridCells[0];
                    foreach (var cell in room.gridCells)
                    {
                        mn.x = Mathf.Min(mn.x, cell.x); mn.y = Mathf.Min(mn.y, cell.y);
                        mx.x = Mathf.Max(mx.x, cell.x); mx.y = Mathf.Max(mx.y, cell.y);
                    }
                    float cs = dungeonData.gridCellSize * gridZoom;
                    Rect br = new Rect(
                        GetCellScreenRect(mn, canvasRect).position,
                        GetCellScreenRect(mx, canvasRect).max - GetCellScreenRect(mn, canvasRect).position + Vector2.one * cs);
                    Handles.DrawSolidRectangleWithOutline(br, Color.clear, Handles.color);
                    Handles.EndGUI();
                }
            }

            if (isPainting && paintedCells.Count > 0)
            {
                Color pc = heightColors.ContainsKey(currentHeightLevel) ? heightColors[currentHeightLevel] : Color.white;
                pc.a = 0.4f;
                foreach (var cell in paintedCells)
                    EditorGUI.DrawRect(GetCellScreenRect(cell, canvasRect), pc);
            }
        }

        private void DrawModeSpecificUI(Rect canvasRect)
        {
            GUIStyle bigStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, alignment = TextAnchor.UpperCenter };
            GUIStyle subStyle = new GUIStyle(EditorStyles.label)     { fontSize = 11, alignment = TextAnchor.UpperCenter };
            subStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            bigStyle.normal.textColor = Color.yellow;

            string modeText = "", info = "";
            switch (currentMode)
            {
                case ToolMode.PlaceNode:         modeText = "PLACE NODE";       info = "Click para colocar  [N]";           break;
                case ToolMode.PaintRoom:         modeText = "PAINT ROOM";       info = "Click+Arrastrar  [P]"; bigStyle.normal.textColor = Color.cyan;  break;
                case ToolMode.ConnectNodes:      modeText = "CONNECT NODES";    info = "Click nodo → nodo  [C]"; bigStyle.normal.textColor = new Color(0.4f,1f,0.4f); break;
                case ToolMode.SelectAndMove:     modeText = "SELECT / MOVE";    info = "Click para seleccionar  [S]";       break;
                case ToolMode.DeleteElement:     modeText = "DELETE";           info = "Click para borrar  [D]"; bigStyle.normal.textColor = Color.red;  break;
                case ToolMode.DefineRoomNumeric: modeText = "DEFINE ROOM";      info = "Configura el tamaño en el Inspector"; break;
                case ToolMode.EditConnection:    modeText = "EDIT CONNECTION";  info = "Click en una conexión";             break;
            }

            // Semi-transparent background for mode label
            Rect modeBg = new Rect(canvasRect.x, canvasRect.y, canvasRect.width, 52);
            EditorGUI.DrawRect(modeBg, new Color(0, 0, 0, 0.45f));
            GUI.Label(new Rect(canvasRect.x, canvasRect.y + 6,  canvasRect.width, 22), modeText, bigStyle);
            GUI.Label(new Rect(canvasRect.x, canvasRect.y + 28, canvasRect.width, 18), info,     subStyle);

            // Cursor tooltip with grid position + world meters
            Vector2 mp = Event.current.mousePosition;
            if (canvasRect.Contains(mp) && showGridCoordinates && dungeonData != null)
            {
                Vector2Int gp = ScreenToGrid(mp, canvasRect);
                float cellM   = dungeonData.gridCellSize;
                float worldX  = gp.x * cellM;
                float worldZ  = gp.y * cellM;
                float layerY  = currentHeightLevel * dungeonData.heightPerLevel;
                string coordText = $"Celda ({gp.x}, {gp.y})  →  {worldX:0}m, {worldZ:0}m  |  Altura: {layerY:0.#}m  [{GetLayerName(currentHeightLevel)}]";

                GUIStyle coordStyle = new GUIStyle(EditorStyles.label) { fontSize = 11 };
                coordStyle.normal.textColor = Color.yellow;
                float tw = coordStyle.CalcSize(new GUIContent(coordText)).x + 16;
                Rect cr = new Rect(canvasRect.x + 8, canvasRect.yMax - 30, tw, 20);
                EditorGUI.DrawRect(new Rect(cr.x - 4, cr.y - 3, cr.width + 8, cr.height + 6), new Color(0, 0, 0, 0.75f));
                GUI.Label(cr, coordText, coordStyle);
            }

            // Cursor cell highlight
            if (canvasRect.Contains(mp))
            {
                Vector2Int gp = ScreenToGrid(mp, canvasRect);
                Color cc;
                switch (currentMode)
                {
                    case ToolMode.PlaceNode:     cc = new Color(0,1,0,0.3f); break;
                    case ToolMode.PaintRoom:
                        cc = heightColors.ContainsKey(currentHeightLevel) ? heightColors[currentHeightLevel] : Color.white;
                        cc.a = 0.4f; break;
                    case ToolMode.DeleteElement: cc = new Color(1,0,0,0.3f); break;
                    default:                     cc = new Color(1,1,1,0.1f); break;
                }
                EditorGUI.DrawRect(GetCellScreenRect(gp, canvasRect), cc);
            }
        }
        #endregion

        #region Mouse Input
        private void HandleMouseInput(Rect canvasRect)
        {
            Event e = Event.current;
            bool allowOutside = currentMode == ToolMode.PaintRoom && isPainting;
            if (!canvasRect.Contains(e.mousePosition) && !allowOutside) return;

            Vector2Int gridPos = ScreenToGrid(e.mousePosition, canvasRect);
            switch (currentMode)
            {
                case ToolMode.PlaceNode:     HandlePlaceNodeInput(e, gridPos);             break;
                case ToolMode.ConnectNodes:  HandleConnectNodesInput(e, gridPos, canvasRect); break;
                case ToolMode.PaintRoom:     HandlePaintRoomInput(e, gridPos);             break;
                case ToolMode.SelectAndMove: HandleSelectAndMoveInput(e, gridPos);         break;
                case ToolMode.DeleteElement: HandleDeleteInput(e, gridPos);                break;
            }
            if (e.type == EventType.MouseMove) Repaint();
        }

        private void HandlePlaceNodeInput(Event e, Vector2Int gridPos)
        {
            if (e.type != EventType.MouseDown || e.button != 0) return;
            bool exists = dungeonData.nodes.Exists(n => n.gridPosition == gridPos && n.heightLevel == currentHeightLevel);
            if (!exists)
            {
                Undo.RegisterCompleteObjectUndo(dungeonData, "Place Node");
                dungeonData.nodes.Add(new DungeonNode(gridPos, currentHeightLevel, NodeType.Corridor));
                dungeonData.RebuildCache();
                EditorUtility.SetDirty(dungeonData);
                e.Use(); Repaint();
            }
        }

        private void HandleConnectNodesInput(Event e, Vector2Int gridPos, Rect canvasRect)
        {
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                DungeonNode clicked = GetNodeAtPosition(gridPos, currentHeightLevel);
                if (clicked != null)
                {
                    if (connectionStartNode == null) connectionStartNode = clicked;
                    else if (connectionStartNode != clicked) { CreateConnection(connectionStartNode, clicked); connectionStartNode = null; }
                    e.Use(); Repaint();
                }
            }
            else if (e.type == EventType.MouseMove) Repaint();
        }

        private void HandlePaintRoomInput(Event e, Vector2Int gridPos)
        {
            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
            {
                isPainting = true; paintedCells.Clear(); paintedCells.Add(gridPos);
                e.Use(); Repaint();
            }
            else if (e.type == EventType.MouseDrag && e.button == 0 && isPainting && !e.alt)
            {
                if (!paintedCells.Contains(gridPos)) paintedCells.Add(gridPos);
                e.Use(); Repaint();
            }
            else if (e.type == EventType.MouseUp && e.button == 0 && isPainting)
            {
                if (paintedCells.Count > 0 && dungeonData != null)
                {
                    Undo.RegisterCompleteObjectUndo(dungeonData, "Paint Room");
                    DungeonRoom newRoom = new DungeonRoom(currentHeightLevel);
                    newRoom.gridCells.AddRange(paintedCells);
                    newRoom.roomName = "Room_" + (dungeonData.rooms.Count + 1);
                    dungeonData.rooms.Add(newRoom);
                    EditorUtility.SetDirty(dungeonData);
                    Debug.Log($"Created '{newRoom.roomName}' – {newRoom.gridCells.Count} cells");
                }
                isPainting = false; paintedCells.Clear();
                previewDirty = true;
                e.Use(); Repaint();
            }
            // Safety catch
            if (e.type == EventType.MouseDrag && e.button == 0 && !isPainting && !e.alt)
            {
                isPainting = true; paintedCells.Clear(); paintedCells.Add(gridPos);
                e.Use(); Repaint();
            }
        }

        private void HandleSelectAndMoveInput(Event e, Vector2Int gridPos)
        {
            if (e.type != EventType.MouseDown || e.button != 0) return;
            DungeonNode n = GetNodeAtPosition(gridPos, currentHeightLevel);
            if (n != null) { selectedNode = n; selectedConnection = null; selectedRoom = null; e.Use(); Repaint(); return; }
            DungeonRoom r = GetRoomAtPosition(gridPos, currentHeightLevel);
            if (r != null) { selectedRoom = r; selectedNode = null; selectedConnection = null; e.Use(); Repaint(); return; }
            ClearSelection(); Repaint();
        }

        private void HandleDeleteInput(Event e, Vector2Int gridPos)
        {
            if (e.type != EventType.MouseDown || e.button != 0) return;
            DungeonNode n = GetNodeAtPosition(gridPos, currentHeightLevel);
            if (n != null) { DeleteNode(n); e.Use(); Repaint(); return; }
            DungeonRoom r = GetRoomAtPosition(gridPos, currentHeightLevel);
            if (r != null)
            {
                Undo.RegisterCompleteObjectUndo(dungeonData, "Delete Room");
                dungeonData.rooms.Remove(r);
                EditorUtility.SetDirty(dungeonData);
                e.Use(); Repaint();
            }
        }

        private void HandleCanvasControls(Rect canvasRect)
        {
            Event e = Event.current;
            if (currentMode == ToolMode.PaintRoom && e.button == 0 && !e.alt) return;

            if ((e.type == EventType.MouseDrag && e.button == 2) ||
                (e.type == EventType.MouseDrag && e.button == 0 && (e.alt || tempPanMode)))
            { gridOffset += e.delta; e.Use(); Repaint(); }

            if (e.type == EventType.ScrollWheel && canvasRect.Contains(e.mousePosition))
            {
                float nz  = Mathf.Clamp(gridZoom + -e.delta.y * 0.05f, minZoom, maxZoom);
                Vector2 l = e.mousePosition - canvasRect.position - gridOffset;
                gridOffset = gridOffset * (nz / gridZoom) + l * (1 - nz / gridZoom);
                gridZoom   = nz;
                e.Use(); Repaint();
            }

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Space) { tempPanMode = true;  e.Use(); }
            if (e.type == EventType.KeyUp   && e.keyCode == KeyCode.Space) { tempPanMode = false; e.Use(); }
        }
        #endregion

        #region Properties Panels
        private void DrawNodeProperties()
        {
            if (selectedNode == null || dungeonData == null) { ClearSelection(); return; }
            EditorGUILayout.LabelField("Node Properties", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("ID:", selectedNode.id);
            EditorGUILayout.LabelField("Position:", selectedNode.gridPosition.ToString());
            int newH = EditorGUILayout.IntField("Height Level:", selectedNode.heightLevel);
            if (newH != selectedNode.heightLevel) { selectedNode.heightLevel = newH; EditorUtility.SetDirty(dungeonData); }
            NodeType newT = (NodeType)EditorGUILayout.EnumPopup("Type:", selectedNode.type);
            if (newT != selectedNode.type) { selectedNode.type = newT; EditorUtility.SetDirty(dungeonData); }
            EditorGUILayout.Space();
            if (GUILayout.Button("Delete Node")) { DeleteNode(selectedNode); selectedNode = null; }
        }

        private void DrawConnectionProperties()
        {
            if (selectedConnection == null || dungeonData == null) { ClearSelection(); return; }
            EditorGUILayout.LabelField("Connection Properties", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("ID:", selectedConnection.id);

            ConnectionType newT = (ConnectionType)EditorGUILayout.EnumPopup("Type:", selectedConnection.transitionType);
            if (newT != selectedConnection.transitionType) { selectedConnection.transitionType = newT; EditorUtility.SetDirty(dungeonData); }

            EditorGUILayout.LabelField("Width Points:", EditorStyles.boldLabel);
            if (selectedConnection.widthPoints == null)
                selectedConnection.widthPoints = new List<WidthPoint>
                {
                    new WidthPoint { normalizedPosition = 0f, width = 3f },
                    new WidthPoint { normalizedPosition = 1f, width = 3f }
                };

            for (int i = 0; i < selectedConnection.widthPoints.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Point {i}:", GUILayout.Width(55));
                float np = EditorGUILayout.Slider(selectedConnection.widthPoints[i].normalizedPosition, 0f, 1f);
                float nw = EditorGUILayout.FloatField(selectedConnection.widthPoints[i].width, GUILayout.Width(45));
                if (np != selectedConnection.widthPoints[i].normalizedPosition || nw != selectedConnection.widthPoints[i].width)
                { selectedConnection.widthPoints[i].normalizedPosition = np; selectedConnection.widthPoints[i].width = nw; EditorUtility.SetDirty(dungeonData); }
                if (GUILayout.Button("X", GUILayout.Width(20)) && selectedConnection.widthPoints.Count > 2)
                { selectedConnection.widthPoints.RemoveAt(i); EditorUtility.SetDirty(dungeonData); }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Add Width Point"))
            { selectedConnection.widthPoints.Add(new WidthPoint { normalizedPosition = 0.5f, width = 3f }); EditorUtility.SetDirty(dungeonData); }

            if (selectedConnection.transitionType == ConnectionType.Ramp)
            {
                float ns = EditorGUILayout.FloatField("Custom Slope (°, -1=auto):", selectedConnection.customSlope);
                if (ns != selectedConnection.customSlope) { selectedConnection.customSlope = ns; EditorUtility.SetDirty(dungeonData); }
            }
            if (selectedConnection.transitionType == ConnectionType.Stairs)
            {
                int nsc = EditorGUILayout.IntField("Step Count:", selectedConnection.stepCount);
                if (nsc != selectedConnection.stepCount) { selectedConnection.stepCount = Mathf.Max(1, nsc); EditorUtility.SetDirty(dungeonData); }
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Delete Connection"))
            {
                Undo.RegisterCompleteObjectUndo(dungeonData, "Delete Connection");
                dungeonData.connections.Remove(selectedConnection);
                EditorUtility.SetDirty(dungeonData); selectedConnection = null;
            }
        }

        private void DrawRoomProperties()
        {
            if (selectedRoom == null || dungeonData == null) { ClearSelection(); return; }
            EditorGUILayout.LabelField("Room Properties", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("ID:", selectedRoom.id);
            string newName = EditorGUILayout.TextField("Name:", selectedRoom.roomName);
            if (newName != selectedRoom.roomName) { selectedRoom.roomName = newName; EditorUtility.SetDirty(dungeonData); }
            int newH = EditorGUILayout.IntField("Height Level:", selectedRoom.heightLevel);
            if (newH != selectedRoom.heightLevel) { selectedRoom.heightLevel = newH; EditorUtility.SetDirty(dungeonData); }
            int cellCount = selectedRoom.gridCells?.Count ?? 0;
            float roomArea = cellCount * dungeonData.gridCellSize * dungeonData.gridCellSize;
            EditorGUILayout.LabelField("Tamaño:", $"{cellCount} celdas  ·  {roomArea:0} m²");
            float layerHRoom = selectedRoom.heightLevel * dungeonData.heightPerLevel;
            EditorGUILayout.LabelField("Capa:", $"{GetLayerName(selectedRoom.heightLevel)}  (altura {layerHRoom:0.#}m)");
            EditorGUILayout.Space();
            if (GUILayout.Button("Copy Room")) CopySelectedRoom();
            if (GUILayout.Button("Delete Room"))
            {
                Undo.RegisterCompleteObjectUndo(dungeonData, "Delete Room");
                dungeonData.rooms.Remove(selectedRoom);
                EditorUtility.SetDirty(dungeonData); selectedRoom = null;
            }
        }

        private void DrawGeneralProperties()
        {
            if (dungeonData == null) return;

            // ── Escala del mundo ───────────────────────────────────
            DrawSectionHeader("📏  Escala del mundo");
            EditorGUILayout.HelpBox(
                $"Cada celda = {dungeonData.gridCellSize:0.#}m x {dungeonData.gridCellSize:0.#}m  |  Altura por planta = {dungeonData.heightPerLevel:0.#}m",
                MessageType.Info);

            float newCS = EditorGUILayout.Slider($"Celda = {dungeonData.gridCellSize:0.#}m", dungeonData.gridCellSize, 1f, 20f);
            if (newCS != dungeonData.gridCellSize) { dungeonData.gridCellSize = newCS; EditorUtility.SetDirty(dungeonData); }

            float newHL = EditorGUILayout.FloatField("Altura por planta (m):", dungeonData.heightPerLevel);
            if (newHL != dungeonData.heightPerLevel) { dungeonData.heightPerLevel = newHL; EditorUtility.SetDirty(dungeonData); }

            // ── Pasillos ───────────────────────────────────────────
            EditorGUILayout.Space(4);
            DrawSectionHeader("🚪  Pasillos y túneles");
            int newTS = EditorGUILayout.IntSlider("Segmentos túnel:", dungeonData.tunnelSegments, 4, 16);
            if (newTS != dungeonData.tunnelSegments) { dungeonData.tunnelSegments = newTS; EditorUtility.SetDirty(dungeonData); }
            float newTH = EditorGUILayout.FloatField("Altura pasillo (m):", dungeonData.defaultTunnelHeight);
            if (newTH != dungeonData.defaultTunnelHeight) { dungeonData.defaultTunnelHeight = newTH; EditorUtility.SetDirty(dungeonData); }

            // ── Mesh replacement ───────────────────────────────────
            EditorGUILayout.Space(4);
            DrawSectionHeader("🧱  Meshes personalizados (opcional)");
            EditorGUILayout.HelpBox("Sin Mesh Set se usan primitivas de Unity. Compatible URP/HDRP/Built-in.", MessageType.Info);
            MeshReplacementSet newMS = (MeshReplacementSet)EditorGUILayout.ObjectField("Mesh Set:", dungeonData.meshSet, typeof(MeshReplacementSet), false);
            if (newMS != dungeonData.meshSet) { dungeonData.meshSet = newMS; EditorUtility.SetDirty(dungeonData); }

            // ── Estadísticas ───────────────────────────────────────
            EditorGUILayout.Space(4);
            DrawSectionHeader("📊  Estadísticas");
            int totalCells = 0;
            foreach (var room in dungeonData.rooms) totalCells += room.gridCells.Count;
            float areaSqM = totalCells * dungeonData.gridCellSize * dungeonData.gridCellSize;

            EditorGUILayout.LabelField("Habitaciones:", (dungeonData.rooms?.Count ?? 0).ToString());
            EditorGUILayout.LabelField("Nodos:",        (dungeonData.nodes?.Count ?? 0).ToString());
            EditorGUILayout.LabelField("Conexiones:",   (dungeonData.connections?.Count ?? 0).ToString());
            EditorGUILayout.LabelField("Área total:",   $"{areaSqM:0} m²  ({totalCells} celdas)");

            // ── Sala numérica ──────────────────────────────────────
            if (currentMode == ToolMode.DefineRoomNumeric)
            {
                EditorGUILayout.Space(4);
                DrawSectionHeader("📐  Sala numérica");
                roomSizeNumeric     = EditorGUILayout.Vector2IntField("Tamaño (celdas):", roomSizeNumeric);
                roomPositionNumeric = EditorGUILayout.Vector2IntField("Posición:",        roomPositionNumeric);
                float wM = roomSizeNumeric.x * dungeonData.gridCellSize;
                float hM = roomSizeNumeric.y * dungeonData.gridCellSize;
                EditorGUILayout.LabelField("Dimensiones:", $"{wM:0}m × {hM:0}m  ({roomSizeNumeric.x * roomSizeNumeric.y} celdas)", EditorStyles.miniLabel);
                if (GUILayout.Button("Crear habitación")) CreateNumericRoom();
            }
        }
        #endregion

        #region Canvas Overlays

        /// <summary>
        /// Regla de escala en la esquina inferior derecha del canvas.
        /// Muestra cuántos metros representa un segmento visible del grid.
        /// </summary>
        private void DrawScaleRuler(Rect canvasRect)
        {
            if (dungeonData == null) return;

            float cs      = dungeonData.gridCellSize * gridZoom; // pixels per cell
            float cellM   = dungeonData.gridCellSize;             // meters per cell

            // Choose a round number of cells that gives a ruler between 60-160px
            int   rulerCells = 1;
            float rulerPx    = cs;
            int[] candidates = { 1, 2, 5, 10, 20, 50 };
            foreach (int c in candidates)
            {
                float px = c * cs;
                if (px >= 60f) { rulerCells = c; rulerPx = px; break; }
            }

            float rulerM  = rulerCells * cellM;
            string label  = rulerM >= 1000 ? $"{rulerM/1000:0.#}km" : $"{rulerM:0}m";

            float margin  = 12f;
            float rulerY  = canvasRect.yMax - 48f;
            float rulerX  = canvasRect.xMax - margin - rulerPx;

            // Background
            EditorGUI.DrawRect(new Rect(rulerX - 6, rulerY - 4, rulerPx + 12, 26), new Color(0, 0, 0, 0.65f));

            // Draw ruler line + ticks
            Handles.BeginGUI();
            Handles.color = new Color(1f, 0.92f, 0.3f);
            float lineY = rulerY + 14f;
            Handles.DrawLine(new Vector3(rulerX, lineY), new Vector3(rulerX + rulerPx, lineY));
            Handles.DrawLine(new Vector3(rulerX,          lineY - 5), new Vector3(rulerX,          lineY + 5));
            Handles.DrawLine(new Vector3(rulerX + rulerPx, lineY - 5), new Vector3(rulerX + rulerPx, lineY + 5));
            Handles.EndGUI();

            GUIStyle rs = new GUIStyle(EditorStyles.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter };
            rs.normal.textColor = new Color(1f, 0.92f, 0.3f);
            GUI.Label(new Rect(rulerX, rulerY, rulerPx, 14), label, rs);
        }

        /// <summary>
        /// Leyenda de capas activas en la esquina superior derecha del canvas.
        /// Solo muestra las capas que tienen contenido.
        /// </summary>
        private void DrawLayerLegend(Rect canvasRect)
        {
            if (dungeonData == null) return;

            // Collect which levels actually have content
            var levelsWithContent = new System.Collections.Generic.HashSet<int>();
            foreach (var room in dungeonData.rooms)    levelsWithContent.Add(room.heightLevel);
            foreach (var node in dungeonData.nodes)    levelsWithContent.Add(node.heightLevel);
            if (levelsWithContent.Count == 0) return;

            var levels = new System.Collections.Generic.List<int>(levelsWithContent);
            levels.Sort();

            float itemH   = 20f;
            float itemW   = 150f;
            float padding = 6f;
            float totalH  = levels.Count * itemH + padding * 2;
            float startX  = canvasRect.xMax - itemW - 10f;
            float startY  = canvasRect.y + 62f; // below mode label

            EditorGUI.DrawRect(new Rect(startX - 4, startY, itemW + 8, totalH), new Color(0, 0, 0, 0.65f));

            GUIStyle ls = new GUIStyle(EditorStyles.label) { fontSize = 11 };
            GUIStyle bs = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };

            for (int i = 0; i < levels.Count; i++)
            {
                int lv    = levels[i];
                bool active = lv == currentHeightLevel;
                Color lc  = heightColors.ContainsKey(lv) ? heightColors[lv] : Color.white;
                float rowY = startY + padding + i * itemH;

                // Color swatch
                EditorGUI.DrawRect(new Rect(startX, rowY + 3f, 12f, 12f), lc);

                // Level name
                GUIStyle style = active ? bs : ls;
                style.normal.textColor = active ? Color.white : new Color(0.7f, 0.7f, 0.7f);
                string prefix = active ? "▶ " : "   ";
                GUI.Label(new Rect(startX + 18f, rowY, itemW - 18f, itemH),
                    $"{prefix}{GetLayerName(lv)}  ({lv})", style);
            }
        }

        /// <summary>
        /// Stats del dungeon en la parte superior del canvas (área total, habitaciones, pasillos).
        /// </summary>
        private void DrawStats(Rect canvasRect)
        {
            if (dungeonData == null) return;

            int totalCells = 0;
            foreach (var room in dungeonData.rooms)
                totalCells += room.gridCells.Count;

            float cellM = dungeonData.gridCellSize;
            float areaSqM = totalCells * cellM * cellM;
            string areaStr = areaSqM >= 10000 ? $"{areaSqM/10000:0.##}ha" : $"{areaSqM:0}m²";

            string statsText = $"Área: {areaStr}   Habitaciones: {dungeonData.rooms.Count}   Nodos: {dungeonData.nodes.Count}   Conexiones: {dungeonData.connections.Count}";

            GUIStyle st = new GUIStyle(EditorStyles.label) { fontSize = 10, alignment = TextAnchor.UpperLeft };
            st.normal.textColor = new Color(0.75f, 0.75f, 0.75f);
            float tw = st.CalcSize(new GUIContent(statsText)).x + 16;
            Rect bg = new Rect(canvasRect.x + 4, canvasRect.y + 54, tw, 18);
            EditorGUI.DrawRect(bg, new Color(0, 0, 0, 0.5f));
            GUI.Label(new Rect(bg.x + 6, bg.y + 2, tw, 16), statsText, st);
        }

        #endregion

        #region Helpers
        private Vector2 GridToScreen(Vector2Int gp, Rect r)
        {
            float cs = dungeonData.gridCellSize * gridZoom;
            return new Vector2(r.x + gp.x * cs + gridOffset.x, r.y + gp.y * cs + gridOffset.y);
        }
        private Vector2Int ScreenToGrid(Vector2 sp, Rect r)
        {
            float cs = dungeonData.gridCellSize * gridZoom;
            return new Vector2Int(
                Mathf.RoundToInt((sp.x - r.x - gridOffset.x) / cs),
                Mathf.RoundToInt((sp.y - r.y - gridOffset.y) / cs));
        }
        private Rect GetCellScreenRect(Vector2Int gp, Rect r)
        {
            float cs = dungeonData.gridCellSize * gridZoom;
            Vector2 sp = GridToScreen(gp, r);
            return new Rect(sp.x, sp.y, cs, cs);
        }
        private DungeonNode GetNodeAtPosition(Vector2Int gp, int h) =>
            dungeonData.nodes.Find(n => n.gridPosition == gp && n.heightLevel == h);
        private DungeonRoom GetRoomAtPosition(Vector2Int gp, int h) =>
            dungeonData.rooms.Find(room => room.gridCells.Contains(gp) && room.heightLevel == h);

        private void CreateConnection(DungeonNode nA, DungeonNode nB)
        {
            bool exists = dungeonData.connections.Exists(c =>
                (c.nodeAId == nA.id && c.nodeBId == nB.id) || (c.nodeAId == nB.id && c.nodeBId == nA.id));
            if (!exists)
            {
                Undo.RegisterCompleteObjectUndo(dungeonData, "Create Connection");
                var conn = new DungeonConnection(nA.id, nB.id, currentCorridorWidth);
                conn.transitionType = currentConnectionType;
                if (nA.heightLevel != nB.heightLevel && conn.transitionType == ConnectionType.Flat)
                    conn.transitionType = ConnectionType.Ramp;
                dungeonData.connections.Add(conn);
                EditorUtility.SetDirty(dungeonData);
            }
        }

        private void DeleteNode(DungeonNode node)
        {
            Undo.RegisterCompleteObjectUndo(dungeonData, "Delete Node");
            dungeonData.connections.RemoveAll(c => c.nodeAId == node.id || c.nodeBId == node.id);
            dungeonData.nodes.Remove(node);
            dungeonData.RebuildCache();
            EditorUtility.SetDirty(dungeonData);
        }

        private void CreateNumericRoom()
        {
            if (dungeonData == null) return;
            Undo.RegisterCompleteObjectUndo(dungeonData, "Create Numeric Room");
            DungeonRoom nr = new DungeonRoom(currentHeightLevel);
            nr.roomName  = "Room_" + (dungeonData.rooms.Count + 1);
            nr.manualSize = roomSizeNumeric;
            nr.shape     = RoomShape.Rectangular;
            for (int x = 0; x < roomSizeNumeric.x; x++)
                for (int y = 0; y < roomSizeNumeric.y; y++)
                    nr.gridCells.Add(roomPositionNumeric + new Vector2Int(x, y));
            dungeonData.rooms.Add(nr);
            EditorUtility.SetDirty(dungeonData);
            Repaint();
        }

        private void ClearSelection()
        {
            selectedNode = null; selectedConnection = null;
            selectedRoom = null; connectionStartNode = null;
        }
        #endregion

        #region v1.0.8 Features
        private void CopySelectedRoom()
        {
            if (selectedRoom == null) { Debug.Log("Select a room first."); return; }
            copiedRoom = new DungeonRoom(selectedRoom.heightLevel);
            copiedRoom.gridCells = new List<Vector2Int>(selectedRoom.gridCells);
            copiedRoom.roomName  = selectedRoom.roomName + "_Copy";
            Debug.Log($"Copied: {selectedRoom.roomName}");
        }

        private void PasteRoom()
        {
            if (copiedRoom == null || dungeonData == null) return;
            Undo.RegisterCompleteObjectUndo(dungeonData, "Paste Room");
            DungeonRoom nr = new DungeonRoom(copiedRoom.heightLevel);
            nr.roomName = copiedRoom.roomName;
            foreach (var cell in copiedRoom.gridCells)
                nr.gridCells.Add(cell + new Vector2Int(3, 3));
            dungeonData.rooms.Add(nr);
            EditorUtility.SetDirty(dungeonData);
            Debug.Log("Pasted room (+3,+3)");
            Repaint();
        }

        private void CenterView()
        {
            if (dungeonData == null || dungeonData.rooms.Count == 0) return;
            Vector2Int mn = new Vector2Int(int.MaxValue, int.MaxValue);
            Vector2Int mx = new Vector2Int(int.MinValue, int.MinValue);
            foreach (var room in dungeonData.rooms)
                foreach (var cell in room.gridCells)
                {
                    mn.x = Mathf.Min(mn.x, cell.x); mn.y = Mathf.Min(mn.y, cell.y);
                    mx.x = Mathf.Max(mx.x, cell.x); mx.y = Mathf.Max(mx.y, cell.y);
                }
            Vector2 center = (Vector2)(mn + mx) * 0.5f * (dungeonData.gridCellSize * gridZoom);
            gridOffset = position.size * 0.35f - center;
            Repaint();
        }

        private void PlaceTemplate(RoomTemplate template)
        {
            if (dungeonData == null) return;
            Undo.RegisterCompleteObjectUndo(dungeonData, "Place Template");
            DungeonRoom nr = new DungeonRoom(currentHeightLevel);
            nr.roomName = template.name + "_" + (dungeonData.rooms.Count + 1);
            foreach (var cell in template.GetNormalizedCells())
                nr.gridCells.Add(cell);
            dungeonData.rooms.Add(nr);
            EditorUtility.SetDirty(dungeonData);
            Debug.Log($"Placed template: {template.name}");
            Repaint();
        }
        #endregion

        #region Dungeon Actions
        private void CreateNewDungeonData()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create New Dungeon Data", "NewDungeon", "asset", "Save location");
            if (!string.IsNullOrEmpty(path))
            {
                DungeonData nd = ScriptableObject.CreateInstance<DungeonData>();
                AssetDatabase.CreateAsset(nd, path);
                AssetDatabase.SaveAssets();
                dungeonData    = nd;
                serializedData = new SerializedObject(nd);
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = nd;
            }
        }

        private void GenerateDungeon()
        {
            if (dungeonData == null) { EditorUtility.DisplayDialog("Error", "No dungeon data!", "OK"); return; }

            // Recover lost reference (e.g. after Unity restart)
            if (generatedDungeon == null)
                generatedDungeon = GameObject.Find("Dungeon_" + dungeonData.name);

            if (generatedDungeon != null)
            {
                if (!EditorUtility.DisplayDialog("Replace?", "Delete existing and regenerate?", "Yes", "Cancel")) return;
                Undo.DestroyObjectImmediate(generatedDungeon);
                generatedDungeon = null;
            }

            generatedDungeon = DungeonGenerator.Generate(dungeonData);

            if (generatedDungeon != null)
            {
                // CRITICAL: register with Undo system so Unity includes it in the Scene
                Undo.RegisterCreatedObjectUndo(generatedDungeon, "Generate Dungeon");

                // Mark scene dirty so Unity prompts to save
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene());

                Selection.activeGameObject = generatedDungeon;
                EditorGUIUtility.PingObject(generatedDungeon);
                Debug.Log("Dungeon generated. Save the Scene (Ctrl+S) to persist it!");
            }
        }

        private void ReplaceMeshes()
        {
            if (generatedDungeon == null) { EditorUtility.DisplayDialog("Error", "Generate first.", "OK"); return; }
            if (dungeonData?.meshSet == null) { EditorUtility.DisplayDialog("Error", "Assign a Mesh Replacement Set.", "OK"); return; }
            DungeonMeshReplacer.ReplaceMeshes(dungeonData, generatedDungeon);
        }

        private void RestoreOriginal()
        {
            if (generatedDungeon == null) { EditorUtility.DisplayDialog("Error", "No generated dungeon.", "OK"); return; }
            DungeonMeshReplacer.RestoreOriginalMeshes(generatedDungeon);
        }
        #endregion
    }
}
