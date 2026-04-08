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
            DefineRoomNumeric, EditConnection, SelectAndMove, DeleteElement,
            PlaceObject, PlaceWall
        }

        private enum PaintBrush
        {
            Freeform, Rectangle, Circle, Line, Polygon, Eraser
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
        private DungeonWall selectedWall;

        // Room painting
        private List<Vector2Int> paintedCells = new List<Vector2Int>();
        private bool isPainting = false;
        private PaintBrush currentBrush = PaintBrush.Freeform;
        private int brushSize = 1;
        private Vector2Int brushDragStart;
        private List<Vector2Int> polygonVertices = new List<Vector2Int>();
        private float currentRoomHeight = 3f;   // default height for newly painted rooms
        private bool currentRoomClosed = false;  // default closed state for newly painted rooms

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

        // Object placement
        private ObjectPrimitiveShape currentObjectShape = ObjectPrimitiveShape.Cube;
        private float currentObjectRotation = 0f;
        private DungeonObject selectedObject;
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
                serializedData = new SerializedObject(dungeonData);
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
            DrawWalls(canvasRect);
            DrawNodes(canvasRect);
            DrawObjects(canvasRect);
            HandleMouseInput(canvasRect);
            DrawModeSpecificUI(canvasRect);
        }

        // ── Right panel ────────────────────────────────────────────
        private void DrawPropertiesPanel()
        {
            propertiesScrollPos = EditorGUILayout.BeginScrollView(propertiesScrollPos);

            DrawToolPalette();

            EditorGUILayout.Space(8);
            GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1));
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Inspector", EditorStyles.boldLabel);

            if      (selectedNode       != null) DrawNodeProperties();
            else if (selectedConnection != null) DrawConnectionProperties();
            else if (selectedRoom       != null) DrawRoomProperties();
            else if (selectedObject     != null) DrawObjectProperties();
            else if (selectedWall       != null) DrawWallProperties();
            else                                 DrawGeneralProperties();

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolPalette()
        {
            if (dungeonData == null) return;

            EditorGUILayout.LabelField("Tool Mode", EditorStyles.boldLabel);

            string[]   toolNames  = { "Place Node","Connect Nodes","Paint Room","Define Room","Edit Conn.","Select/Move","Delete","Place Object", "Place Wall" };
            ToolMode[] toolValues = { ToolMode.PlaceNode, ToolMode.ConnectNodes, ToolMode.PaintRoom,
                                      ToolMode.DefineRoomNumeric, ToolMode.EditConnection,
                                      ToolMode.SelectAndMove, ToolMode.DeleteElement, ToolMode.PlaceObject, ToolMode.PlaceWall };

            int selIdx = -1;
            for (int i = 0; i < toolValues.Length; i++)
                if (currentMode == toolValues[i]) selIdx = i;

            int newIdx = GUILayout.SelectionGrid(selIdx, toolNames, 2, GUILayout.Height(130));
            if (newIdx != selIdx && newIdx >= 0)
            {
                currentMode = toolValues[newIdx];
                if (currentMode != ToolMode.SelectAndMove)
                { selectedNode = null; selectedConnection = null; selectedRoom = null; selectedObject = null; selectedWall = null; }
                if (currentMode != ToolMode.PaintRoom && currentMode != ToolMode.PlaceWall)
                    polygonVertices.Clear();
            }

            // ── Brush selector (Paint Room & Place Wall) ──────────
            if (currentMode == ToolMode.PaintRoom || currentMode == ToolMode.PlaceWall)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Brush Shape", EditorStyles.boldLabel);
                string[] brushNames = { "Freeform", "Rectangle", "Circle", "Line", "Polygon", "Eraser" };
                PaintBrush[] brushVals = { PaintBrush.Freeform, PaintBrush.Rectangle, PaintBrush.Circle, PaintBrush.Line, PaintBrush.Polygon, PaintBrush.Eraser };
                int bIdx = -1;
                for (int i = 0; i < brushVals.Length; i++)
                    if (currentBrush == brushVals[i]) bIdx = i;
                int newB = GUILayout.SelectionGrid(bIdx, brushNames, 3, GUILayout.Height(40));
                if (newB != bIdx && newB >= 0)
                {
                    currentBrush = brushVals[newB];
                    polygonVertices.Clear();
                }
                if (currentBrush == PaintBrush.Circle || currentBrush == PaintBrush.Line)
                    brushSize = EditorGUILayout.IntSlider("Brush Size:", brushSize, 1, 10);

                if (currentBrush == PaintBrush.Polygon && polygonVertices.Count > 0)
                {
                    EditorGUILayout.HelpBox($"Polygon: {polygonVertices.Count} vertices. Double-click or press Enter to finish.", MessageType.Info);
                    if (GUILayout.Button("Finish Polygon")) FinishPolygon();
                    if (GUILayout.Button("Cancel Polygon")) polygonVertices.Clear();
                }

                if (currentMode == ToolMode.PaintRoom)
                {
                    // ── Default room properties for new rooms ───────────
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("New Room Defaults", EditorStyles.boldLabel);
                    currentRoomHeight = EditorGUILayout.FloatField("Room Height (m):", Mathf.Max(0.5f, currentRoomHeight));
                    currentRoomClosed = EditorGUILayout.Toggle("Closed (with ceiling):", currentRoomClosed);
                }
            }

            // ── Object shape selector (only in Place Object mode) ──
            if (currentMode == ToolMode.PlaceObject)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Object Shape", EditorStyles.boldLabel);
                string[] shapeNames = { "Cube", "Sphere", "Cylinder", "Capsule" };
                ObjectPrimitiveShape[] shapeVals = { ObjectPrimitiveShape.Cube, ObjectPrimitiveShape.Sphere, ObjectPrimitiveShape.Cylinder, ObjectPrimitiveShape.Capsule };
                int sIdx = -1;
                for (int i = 0; i < shapeVals.Length; i++)
                    if (currentObjectShape == shapeVals[i]) sIdx = i;
                int newS = GUILayout.SelectionGrid(sIdx, shapeNames, 2, GUILayout.Height(40));
                if (newS != sIdx && newS >= 0)
                    currentObjectShape = shapeVals[newS];
                currentObjectRotation = EditorGUILayout.Slider("Rotation Y°:", currentObjectRotation, 0f, 360f);
                EditorGUILayout.HelpBox("Click to place. R to rotate 90°. Right-click to delete.", MessageType.None);
            }

            // ── Editor settings ────────────────────────────────────
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Editor Settings", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Current Level:", GUILayout.Width(95));
            currentHeightLevel = EditorGUILayout.IntField(currentHeightLevel);
            Color lc = heightColors.ContainsKey(currentHeightLevel) ? heightColors[currentHeightLevel] : Color.white;
            EditorGUILayout.ColorField(GUIContent.none, lc, false, false, false, GUILayout.Width(25));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Grid Size:", GUILayout.Width(95));
            dungeonData.gridCellSize = EditorGUILayout.Slider(dungeonData.gridCellSize, 1f, 20f);
            EditorGUILayout.EndHorizontal();

            showGridCoordinates = EditorGUILayout.Toggle("Show Coordinates", showGridCoordinates);

            // ── Room templates ─────────────────────────────────────
            EditorGUILayout.Space(8);
            showTemplatePanel = EditorGUILayout.Foldout(showTemplatePanel, "Room Templates", true);
            if (showTemplatePanel && roomTemplates != null)
            {
                EditorGUILayout.HelpBox("Click to place at origin, then Select/Move to reposition.", MessageType.None);
                templateScrollPos = EditorGUILayout.BeginScrollView(templateScrollPos, GUILayout.Height(130));
                foreach (var tmpl in roomTemplates)
                    if (GUILayout.Button($"{tmpl.name}  ({tmpl.size.x}x{tmpl.size.y})"))
                        PlaceTemplate(tmpl);
                EditorGUILayout.EndScrollView();
            }

            // ── Copy / Paste ───────────────────────────────────────
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Copy / Paste", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledGroupScope(selectedRoom == null))
                if (GUILayout.Button("Copy  (Ctrl+C)")) CopySelectedRoom();
            using (new EditorGUI.DisabledGroupScope(copiedRoom == null))
                if (GUILayout.Button("Paste  (Ctrl+V)")) PasteRoom();
            EditorGUILayout.EndHorizontal();
            if (copiedRoom != null)
                EditorGUILayout.LabelField($"Clipboard: {copiedRoom.roomName}", EditorStyles.miniLabel);

            // ── Shortcuts help ─────────────────────────────────────
            EditorGUILayout.Space(8);
            if (GUILayout.Button("Keyboard Shortcuts..."))
                EditorUtility.DisplayDialog("Shortcuts", KeyboardShortcuts.GetShortcutHelpText(), "OK");
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

                GUILayout.FlexibleSpace();

                if (dungeonData != null)
                    EditorGUILayout.LabelField(
                        $"Rooms: {dungeonData.rooms.Count}  Nodes: {dungeonData.nodes.Count}  Conn: {dungeonData.connections.Count}  Obj: {dungeonData.objects.Count}",
                        GUILayout.Width(360));

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
                Vector2 sp = GridToScreenCenter(node.gridPosition, canvasRect);
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

                Vector2 pA = GridToScreenCenter(nA.gridPosition, canvasRect);
                Vector2 pB = GridToScreenCenter(nB.gridPosition, canvasRect);
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
                Vector2 sp = GridToScreenCenter(connectionStartNode.gridPosition, canvasRect);
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

                    // ── Closed-room badge ──────────────────────────
                    if (room.isClosed)
                    {
                        float alpha = room.heightLevel != currentHeightLevel ? 0.4f : 1f;
                        GUIStyle badgeStyle = new GUIStyle(EditorStyles.boldLabel);
                        badgeStyle.normal.textColor = new Color(0.2f, 0.9f, 1f, alpha);
                        badgeStyle.fontSize = Mathf.Max(9, Mathf.RoundToInt(cs * 0.28f));
                        Vector2 badgePos = GridToScreenCenter(mn, canvasRect);
                        GUI.Label(new Rect(badgePos.x + 2, badgePos.y - cs * 0.5f, 60, 20),
                            $"[C] {room.roomHeight:0.#}m", badgeStyle);
                    }

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

            // Shape brush preview while dragging
            if (isPainting && (currentBrush == PaintBrush.Rectangle || currentBrush == PaintBrush.Circle || currentBrush == PaintBrush.Line))
            {
                Vector2Int curGrid = ScreenToGrid(Event.current.mousePosition, canvasRect);
                var previewCells = GetBrushShapeCells(brushDragStart, curGrid, currentBrush);
                Color previewCol = heightColors.ContainsKey(currentHeightLevel) ? heightColors[currentHeightLevel] : Color.white;
                previewCol.a = 0.35f;
                foreach (var cell in previewCells)
                    EditorGUI.DrawRect(GetCellScreenRect(cell, canvasRect), previewCol);
            }

            // Polygon vertices preview
            if (currentBrush == PaintBrush.Polygon && polygonVertices.Count > 0)
            {
                Color polyCol = new Color(0f, 1f, 0.5f, 0.5f);
                Handles.BeginGUI();
                Handles.color = polyCol;
                for (int i = 0; i < polygonVertices.Count; i++)
                {
                    Vector2 sp = GridToScreenCenter(polygonVertices[i], canvasRect);
                    Handles.DrawSolidDisc(sp, Vector3.forward, 5f);
                    if (i > 0)
                    {
                        Vector2 prev = GridToScreenCenter(polygonVertices[i - 1], canvasRect);
                        Handles.DrawLine(prev, sp);
                    }
                }
                // Closing line preview to mouse
                if (canvasRect.Contains(Event.current.mousePosition))
                {
                    Vector2 last = GridToScreenCenter(polygonVertices[polygonVertices.Count - 1], canvasRect);
                    Handles.color = new Color(0f, 1f, 0.5f, 0.3f);
                    Handles.DrawDottedLine(last, Event.current.mousePosition, 3f);
                }
                Handles.EndGUI();
            }
        }

        private void DrawModeSpecificUI(Rect canvasRect)
        {
            GUIStyle bigStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 22, alignment = TextAnchor.UpperCenter };
            GUIStyle subStyle = new GUIStyle(EditorStyles.label)     { fontSize = 13, alignment = TextAnchor.UpperCenter };
            subStyle.normal.textColor = Color.white;
            bigStyle.normal.textColor = Color.yellow;

            string modeText = "", info = "";
            switch (currentMode)
            {
                case ToolMode.PlaceNode:         modeText = "PLACE NODE";       info = "Click to place  [N]";           break;
                case ToolMode.PaintRoom:
                    modeText = "PAINT ROOM";
                    switch (currentBrush)
                    {
                        case PaintBrush.Freeform:  info = "Freeform: Click+Drag  [P]"; break;
                        case PaintBrush.Rectangle: info = "Rectangle: Click+Drag corners"; break;
                        case PaintBrush.Circle:    info = "Circle: Click center, drag radius"; break;
                        case PaintBrush.Line:      info = "Line: Click+Drag start→end"; break;
                        case PaintBrush.Polygon:   info = "Polygon: Click vertices, Enter to fill"; break;
                        case PaintBrush.Eraser:    info = "Eraser: Click on cells to remove"; break;
                        default: info = "Click+Drag  [P]"; break;
                    }
                    bigStyle.normal.textColor = Color.cyan;
                    break;
                case ToolMode.ConnectNodes:      modeText = "CONNECT NODES";    info = "Click node → node  [C]"; bigStyle.normal.textColor = new Color(0.4f,1f,0.4f); break;
                case ToolMode.SelectAndMove:     modeText = "SELECT / MOVE";    info = "Click to select  [S]";          break;
                case ToolMode.DeleteElement:     modeText = "DELETE";           info = "Click to delete  [D]"; bigStyle.normal.textColor = Color.red;  break;
                case ToolMode.DefineRoomNumeric: modeText = "DEFINE ROOM";      info = "Set size in Inspector";         break;
                case ToolMode.EditConnection:    modeText = "EDIT CONNECTION";  info = "Click a connection line";       break;
                case ToolMode.PlaceObject:
                    modeText = "PLACE OBJECT";
                    info = $"Click to place {currentObjectShape}  [R to rotate]";
                    bigStyle.normal.textColor = new Color(1f, 0.8f, 0.2f);
                    break;
                case ToolMode.PlaceWall:
                    modeText = "PLACE WALL";
                    info = "Drag to paint walls, Right-Click drag to erase";
                    bigStyle.normal.textColor = new Color(0.8f, 0.4f, 0.2f);
                    break;
            }

            GUI.Label(new Rect(canvasRect.x, canvasRect.y + 8,  canvasRect.width, 28), modeText, bigStyle);
            GUI.Label(new Rect(canvasRect.x, canvasRect.y + 34, canvasRect.width, 20), info,     subStyle);

            // Grid coordinates
            Vector2 mp = Event.current.mousePosition;
            if (canvasRect.Contains(mp) && showGridCoordinates)
            {
                Vector2Int gp = ScreenToGrid(mp, canvasRect);
                Rect cr = new Rect(canvasRect.x + 8, canvasRect.yMax - 26, 210, 20);
                EditorGUI.DrawRect(new Rect(cr.x - 4, cr.y - 2, cr.width + 8, cr.height + 4), new Color(0,0,0,0.7f));
                GUIStyle cs2 = new GUIStyle(EditorStyles.label) { fontSize = 11 };
                cs2.normal.textColor = Color.yellow;
                GUI.Label(cr, $"({gp.x}, {gp.y})  Level {currentHeightLevel}", cs2);
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
                    case ToolMode.PlaceObject:    cc = new Color(1f,0.8f,0f,0.35f); break;
                    case ToolMode.PlaceWall:      cc = new Color(0.8f,0.4f,0.2f,0.4f); break;
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
                case ToolMode.PlaceNode:         HandlePlaceNodeInput(e, gridPos);             break;
                case ToolMode.ConnectNodes:      HandleConnectNodesInput(e, gridPos, canvasRect); break;
                case ToolMode.PaintRoom:         HandlePaintRoomInput(e, gridPos);             break;
                case ToolMode.SelectAndMove:     HandleSelectAndMoveInput(e, gridPos, canvasRect); break;
                case ToolMode.EditConnection:    HandleEditConnectionInput(e, canvasRect);     break;
                case ToolMode.DeleteElement:     HandleDeleteInput(e, gridPos);                break;
                case ToolMode.PlaceObject:       HandlePlaceObjectInput(e, gridPos);           break;
                case ToolMode.PlaceWall:         HandlePlaceWallInput(e, gridPos);             break;
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
            // ── Polygon brush: click to add vertices, double-click/Enter to finish ──
            if (currentBrush == PaintBrush.Polygon)
            {
                if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
                {
                    // Double-click finishes polygon
                    if (e.clickCount >= 2 && polygonVertices.Count >= 3)
                    {
                        FinishPolygon();
                    }
                    else
                    {
                        polygonVertices.Add(gridPos);
                    }
                    e.Use(); Repaint();
                }
                if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Return && polygonVertices.Count >= 3)
                {
                    FinishPolygon();
                    e.Use(); Repaint();
                }
                if (e.type == EventType.MouseMove) Repaint();
                return;
            }

            // ── Eraser brush: remove cells from existing rooms ──
            if (currentBrush == PaintBrush.Eraser)
            {
                if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && !e.alt)
                {
                    DungeonRoom room = GetRoomAtPosition(gridPos, currentHeightLevel);
                    if (room != null)
                    {
                        Undo.RegisterCompleteObjectUndo(dungeonData, "Erase Cell");
                        room.gridCells.Remove(gridPos);
                        if (room.gridCells.Count == 0)
                            dungeonData.rooms.Remove(room);
                        EditorUtility.SetDirty(dungeonData);
                    }
                    e.Use(); Repaint();
                }
                return;
            }

            // ── Shape brushes: Rectangle / Circle / Line ──
            if (currentBrush == PaintBrush.Rectangle || currentBrush == PaintBrush.Circle || currentBrush == PaintBrush.Line)
            {
                if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
                {
                    isPainting = true;
                    brushDragStart = gridPos;
                    paintedCells.Clear();
                    e.Use(); Repaint();
                }
                else if (e.type == EventType.MouseDrag && e.button == 0 && isPainting && !e.alt)
                {
                    // Preview is drawn via DrawRooms; we just need repaint
                    e.Use(); Repaint();
                }
                else if (e.type == EventType.MouseUp && e.button == 0 && isPainting)
                {
                    var shapeCells = GetBrushShapeCells(brushDragStart, gridPos, currentBrush);
                    if (shapeCells.Count > 0 && dungeonData != null)
                    {
                        Undo.RegisterCompleteObjectUndo(dungeonData, "Paint Room (Shape)");
                        DungeonRoom newRoom = new DungeonRoom(currentHeightLevel, currentRoomHeight);
                        newRoom.isClosed     = currentRoomClosed;
                        newRoom.gridCells.AddRange(shapeCells);
                        newRoom.roomName = "Room_" + (dungeonData.rooms.Count + 1);
                        dungeonData.rooms.Add(newRoom);
                        EditorUtility.SetDirty(dungeonData);
                    }
                    isPainting = false; paintedCells.Clear();
                    e.Use(); Repaint();
                }
                return;
            }

            // ── Freeform fallback (original behaviour) ──
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
                    DungeonRoom newRoom = new DungeonRoom(currentHeightLevel, currentRoomHeight);
                    newRoom.isClosed     = currentRoomClosed;
                    newRoom.gridCells.AddRange(paintedCells);
                    newRoom.roomName = "Room_" + (dungeonData.rooms.Count + 1);
                    dungeonData.rooms.Add(newRoom);
                    EditorUtility.SetDirty(dungeonData);
                    Debug.Log($"Created '{newRoom.roomName}' – {newRoom.gridCells.Count} cells, height={newRoom.roomHeight}m, closed={newRoom.isClosed}");
                }
                isPainting = false; paintedCells.Clear();
                e.Use(); Repaint();
            }
            // Safety catch
            if (e.type == EventType.MouseDrag && e.button == 0 && !isPainting && !e.alt)
            {
                isPainting = true; paintedCells.Clear(); paintedCells.Add(gridPos);
                e.Use(); Repaint();
            }
        }

        private void DrawWalls(Rect canvasRect)
        {
            if (dungeonData.walls == null) return;
            foreach (var wall in dungeonData.walls)
            {
                if (wall.heightLevel != currentHeightLevel) continue;
                Color wc = wall.color;
                wc.a = wall == selectedWall ? 0.9f : 0.6f;
                foreach (var cellPos in wall.gridCells)
                {
                    EditorGUI.DrawRect(GetCellScreenRect(cellPos, canvasRect), wc);
                }

                if (wall.gridCells.Count > 0 && wall == selectedWall)
                {
                    Handles.BeginGUI();
                    Handles.color = Color.yellow;
                    Vector2Int mn = wall.gridCells[0], mx = wall.gridCells[0];
                    foreach (var cell in wall.gridCells)
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
        }

        private void HandlePlaceWallInput(Event e, Vector2Int gridPos)
        {
            // ── Eraser brush: remove cells from existing walls ──
            if (currentBrush == PaintBrush.Eraser)
            {
                if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && !e.alt)
                {
                    DungeonWall wall = GetWallAtPosition(gridPos, currentHeightLevel);
                    if (wall != null)
                    {
                        Undo.RegisterCompleteObjectUndo(dungeonData, "Erase Wall Cell");
                        wall.gridCells.Remove(gridPos);
                        if (wall.gridCells.Count == 0)
                            dungeonData.walls.Remove(wall);
                        EditorUtility.SetDirty(dungeonData);
                    }
                    e.Use(); Repaint();
                }
                return;
            }

            // ── Shape brushes: Rectangle / Circle / Line ──
            if (currentBrush == PaintBrush.Rectangle || currentBrush == PaintBrush.Circle || currentBrush == PaintBrush.Line)
            {
                if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
                {
                    isPainting = true;
                    brushDragStart = gridPos;
                    paintedCells.Clear();
                    e.Use(); Repaint();
                }
                else if (e.type == EventType.MouseDrag && e.button == 0 && isPainting && !e.alt)
                {
                    e.Use(); Repaint();
                }
                else if (e.type == EventType.MouseUp && e.button == 0 && isPainting)
                {
                    var shapeCells = GetBrushShapeCells(brushDragStart, gridPos, currentBrush);
                    if (shapeCells.Count > 0 && dungeonData != null)
                    {
                        Undo.RegisterCompleteObjectUndo(dungeonData, "Paint Wall (Shape)");
                        var newWall = new DungeonWall(currentHeightLevel);
                        newWall.gridCells.AddRange(shapeCells);
                        newWall.wallName = "Wall_" + (dungeonData.walls.Count + 1);
                        dungeonData.walls.Add(newWall);
                        EditorUtility.SetDirty(dungeonData);
                    }
                    isPainting = false; paintedCells.Clear();
                    e.Use(); Repaint();
                }
                return;
            }

            // ── Polygon brush ──
            if (currentBrush == PaintBrush.Polygon)
            {
                if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
                {
                    if (e.clickCount >= 2 && polygonVertices.Count >= 3)
                        FinishPolygonWall();
                    else
                        polygonVertices.Add(gridPos);
                    e.Use(); Repaint();
                }
                if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Return && polygonVertices.Count >= 3)
                {
                    FinishPolygonWall();
                    e.Use(); Repaint();
                }
                if (e.type == EventType.MouseMove) Repaint();
                return;
            }

            // ── Freeform fallback ──
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
                    Undo.RegisterCompleteObjectUndo(dungeonData, "Paint Wall");
                    var newWall = new DungeonWall(currentHeightLevel);
                    newWall.gridCells.AddRange(paintedCells);
                    newWall.wallName = "Wall_" + (dungeonData.walls.Count + 1);
                    dungeonData.walls.Add(newWall);
                    EditorUtility.SetDirty(dungeonData);
                }
                isPainting = false; paintedCells.Clear();
                e.Use(); Repaint();
            }

            // Safety catch
            if (e.type == EventType.MouseDrag && e.button == 0 && !isPainting && !e.alt)
            {
                isPainting = true; paintedCells.Clear(); paintedCells.Add(gridPos);
                e.Use(); Repaint();
            }
        }

        // ── Brush shape / polygon helpers ─────────────────────────
        private List<Vector2Int> GetBrushShapeCells(Vector2Int start, Vector2Int end, PaintBrush brush)
        {
            var cells = new List<Vector2Int>();
            switch (brush)
            {
                case PaintBrush.Rectangle:
                    int xMin = Mathf.Min(start.x, end.x), xMax = Mathf.Max(start.x, end.x);
                    int yMin = Mathf.Min(start.y, end.y), yMax = Mathf.Max(start.y, end.y);
                    for (int x = xMin; x <= xMax; x++)
                        for (int y = yMin; y <= yMax; y++)
                            cells.Add(new Vector2Int(x, y));
                    break;

                case PaintBrush.Circle:
                    float radius = Vector2Int.Distance(start, end);
                    int r = Mathf.Max(1, Mathf.RoundToInt(radius));
                    for (int x = start.x - r; x <= start.x + r; x++)
                        for (int y = start.y - r; y <= start.y + r; y++)
                        {
                            float dist = Mathf.Sqrt((x - start.x) * (x - start.x) + (y - start.y) * (y - start.y));
                            if (dist <= radius + 0.5f)
                                cells.Add(new Vector2Int(x, y));
                        }
                    break;

                case PaintBrush.Line:
                    // Bresenham's line with thickness
                    int dx = Mathf.Abs(end.x - start.x), dy = Mathf.Abs(end.y - start.y);
                    int sx = start.x < end.x ? 1 : -1, sy = start.y < end.y ? 1 : -1;
                    int err = dx - dy;
                    int cx = start.x, cy = start.y;
                    int halfThick = Mathf.Max(0, (brushSize - 1) / 2);
                    while (true)
                    {
                        for (int tx = -halfThick; tx <= halfThick; tx++)
                            for (int ty = -halfThick; ty <= halfThick; ty++)
                            {
                                var c = new Vector2Int(cx + tx, cy + ty);
                                if (!cells.Contains(c)) cells.Add(c);
                            }
                        if (cx == end.x && cy == end.y) break;
                        int e2 = 2 * err;
                        if (e2 > -dy) { err -= dy; cx += sx; }
                        if (e2 < dx)  { err += dx; cy += sy; }
                    }
                    break;
            }
            return cells;
        }

        private void FinishPolygon()
        {
            if (polygonVertices.Count < 3 || dungeonData == null) return;

            // Scanline fill of the polygon
            var cells = FillPolygon(polygonVertices);
            if (cells.Count > 0)
            {
                Undo.RegisterCompleteObjectUndo(dungeonData, "Paint Polygon Room");
                DungeonRoom newRoom = new DungeonRoom(currentHeightLevel, currentRoomHeight);
                newRoom.isClosed     = currentRoomClosed;
                newRoom.gridCells.AddRange(cells);
                newRoom.roomName = "Room_" + (dungeonData.rooms.Count + 1);
                dungeonData.rooms.Add(newRoom);
                EditorUtility.SetDirty(dungeonData);
                Debug.Log($"Created polygon room '{newRoom.roomName}' – {cells.Count} cells, height={newRoom.roomHeight}m");
            }
            polygonVertices.Clear();
            Repaint();
        }

        private void FinishPolygonWall()
        {
            if (polygonVertices.Count < 3 || dungeonData == null) return;

            var cells = FillPolygon(polygonVertices);
            if (cells.Count > 0)
            {
                Undo.RegisterCompleteObjectUndo(dungeonData, "Paint Polygon Wall");
                var newWall = new DungeonWall(currentHeightLevel);
                newWall.gridCells.AddRange(cells);
                newWall.wallName = "Wall_" + (dungeonData.walls.Count + 1);
                dungeonData.walls.Add(newWall);
                EditorUtility.SetDirty(dungeonData);
            }
            polygonVertices.Clear();
            Repaint();
        }

        private List<Vector2Int> FillPolygon(List<Vector2Int> verts)
        {
            var filled = new List<Vector2Int>();
            // Determine bounding box
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            foreach (var v in verts)
            {
                if (v.x < minX) minX = v.x; if (v.x > maxX) maxX = v.x;
                if (v.y < minY) minY = v.y; if (v.y > maxY) maxY = v.y;
            }

            // Scanline fill using ray-casting point-in-polygon
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (PointInPolygon(x, y, verts))
                        filled.Add(new Vector2Int(x, y));
                }
            }

            // Always include edges
            for (int i = 0; i < verts.Count; i++)
            {
                var a = verts[i];
                var b = verts[(i + 1) % verts.Count];
                var lineCells = GetBrushShapeCells(a, b, PaintBrush.Line);
                foreach (var c in lineCells)
                    if (!filled.Contains(c)) filled.Add(c);
            }
            return filled;
        }

        private bool PointInPolygon(int px, int py, List<Vector2Int> poly)
        {
            bool inside = false;
            int n = poly.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float yi = poly[i].y, yj = poly[j].y;
                float xi = poly[i].x, xj = poly[j].x;
                if ((yi > py) != (yj > py) &&
                    px < (xj - xi) * (py - yi) / (yj - yi) + xi)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        private void HandleSelectAndMoveInput(Event e, Vector2Int gridPos, Rect canvasRect)
        {
            if (e.type != EventType.MouseDown || e.button != 0) return;
            DungeonNode n = GetNodeAtPosition(gridPos, currentHeightLevel);
            if (n != null) { selectedNode = n; selectedConnection = null; selectedRoom = null; e.Use(); Repaint(); return; }
            DungeonRoom r = GetRoomAtPosition(gridPos, currentHeightLevel);
            if (r != null) { selectedRoom = r; selectedNode = null; selectedConnection = null; e.Use(); Repaint(); return; }
            
            DungeonConnection c = GetConnectionAtPosition(e.mousePosition, canvasRect);
            if (c != null) { selectedConnection = c; selectedNode = null; selectedRoom = null; selectedObject = null; selectedWall = null; e.Use(); Repaint(); return; }

            DungeonWall w = GetWallAtPosition(gridPos, currentHeightLevel);
            if (w != null) { selectedWall = w; selectedNode = null; selectedConnection = null; selectedRoom = null; selectedObject = null; e.Use(); Repaint(); return; }

            ClearSelection(); Repaint();
        }

        private void HandleEditConnectionInput(Event e, Rect canvasRect)
        {
            if (e.type != EventType.MouseDown || e.button != 0) return;
            DungeonConnection c = GetConnectionAtPosition(e.mousePosition, canvasRect);
            if (c != null) { selectedConnection = c; selectedNode = null; selectedRoom = null; e.Use(); Repaint(); return; }
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
                e.Use(); Repaint(); return;
            }
            // Delete object
            DungeonObject obj = GetObjectAtPosition(gridPos, currentHeightLevel);
            if (obj != null)
            {
                Undo.RegisterCompleteObjectUndo(dungeonData, "Delete Object");
                dungeonData.objects.Remove(obj);
                EditorUtility.SetDirty(dungeonData);
                e.Use(); Repaint(); return;
            }
            // Delete wall
            DungeonWall w = GetWallAtPosition(gridPos, currentHeightLevel);
            if (w != null)
            {
                Undo.RegisterCompleteObjectUndo(dungeonData, "Delete Wall");
                dungeonData.walls.Remove(w);
                EditorUtility.SetDirty(dungeonData);
                e.Use(); Repaint(); return;
            }
        }

        private void HandleCanvasControls(Rect canvasRect)
        {
            Event e = Event.current;
            // Block only drag/click from interfering with tool-specific input — NOT scroll
            bool isMouseAction = (e.type == EventType.MouseDown || e.type == EventType.MouseDrag);
            if (currentMode == ToolMode.PaintRoom && isMouseAction && e.button == 0 && !e.alt) return;
            if (currentMode == ToolMode.PlaceObject && isMouseAction && e.button == 0 && !e.alt) return;

            if ((e.type == EventType.MouseDrag && e.button == 2) ||
                (e.type == EventType.MouseDrag && e.button == 0 && (e.alt || tempPanMode)))
            { gridOffset += e.delta; e.Use(); Repaint(); }

            if (e.type == EventType.ScrollWheel && canvasRect.Contains(e.mousePosition))
            {
                float nz  = Mathf.Clamp(gridZoom + -e.delta.y * 0.05f, minZoom, maxZoom);
                Vector2 l = e.mousePosition - canvasRect.position - gridOffset;
                gridOffset += l * (1 - nz / gridZoom);
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
            if (newH != selectedNode.heightLevel) { Undo.RegisterCompleteObjectUndo(dungeonData, "Change Node Height"); selectedNode.heightLevel = newH; EditorUtility.SetDirty(dungeonData); }
            NodeType newT = (NodeType)EditorGUILayout.EnumPopup("Type:", selectedNode.type);
            if (newT != selectedNode.type) { Undo.RegisterCompleteObjectUndo(dungeonData, "Change Node Type"); selectedNode.type = newT; EditorUtility.SetDirty(dungeonData); }
            EditorGUILayout.Space();
            if (GUILayout.Button("Delete Node")) { DeleteNode(selectedNode); selectedNode = null; }
        }

        private void DrawConnectionProperties()
        {
            if (selectedConnection == null || dungeonData == null) { ClearSelection(); return; }
            EditorGUILayout.LabelField("Connection Properties", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("ID:", selectedConnection.id);

            ConnectionType newT = (ConnectionType)EditorGUILayout.EnumPopup("Type:", selectedConnection.transitionType);
            if (newT != selectedConnection.transitionType) { Undo.RegisterCompleteObjectUndo(dungeonData, "Change Connection Type"); selectedConnection.transitionType = newT; EditorUtility.SetDirty(dungeonData); }

            if (selectedConnection.transitionType == ConnectionType.Tunnel)
            {
                float newHeight = EditorGUILayout.FloatField("Height:", selectedConnection.height);
                if (newHeight != selectedConnection.height) { Undo.RegisterCompleteObjectUndo(dungeonData, "Change Connection Height"); selectedConnection.height = newHeight; EditorUtility.SetDirty(dungeonData); }
            }

            EditorGUILayout.LabelField("Width Points:", EditorStyles.boldLabel);
            if (selectedConnection.widthPoints == null)
            {
                Undo.RegisterCompleteObjectUndo(dungeonData, "Initialize Width Points");
                selectedConnection.widthPoints = new List<WidthPoint>
                {
                    new WidthPoint { normalizedPosition = 0f, width = 3f },
                    new WidthPoint { normalizedPosition = 1f, width = 3f }
                };
                EditorUtility.SetDirty(dungeonData);
            }

            for (int i = 0; i < selectedConnection.widthPoints.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Point {i}:", GUILayout.Width(55));
                float np = EditorGUILayout.Slider(selectedConnection.widthPoints[i].normalizedPosition, 0f, 1f);
                float nw = EditorGUILayout.FloatField(selectedConnection.widthPoints[i].width, GUILayout.Width(45));
                if (np != selectedConnection.widthPoints[i].normalizedPosition || nw != selectedConnection.widthPoints[i].width)
                { Undo.RegisterCompleteObjectUndo(dungeonData, "Change Width Point"); selectedConnection.widthPoints[i].normalizedPosition = np; selectedConnection.widthPoints[i].width = nw; EditorUtility.SetDirty(dungeonData); }
                if (GUILayout.Button("X", GUILayout.Width(20)) && selectedConnection.widthPoints.Count > 2)
                { Undo.RegisterCompleteObjectUndo(dungeonData, "Remove Width Point"); selectedConnection.widthPoints.RemoveAt(i); EditorUtility.SetDirty(dungeonData); }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Add Width Point"))
            { Undo.RegisterCompleteObjectUndo(dungeonData, "Add Width Point"); selectedConnection.widthPoints.Add(new WidthPoint { normalizedPosition = 0.5f, width = 3f }); EditorUtility.SetDirty(dungeonData); }

            if (selectedConnection.transitionType == ConnectionType.Ramp)
            {
                float ns = EditorGUILayout.FloatField("Custom Slope (°, -1=auto):", selectedConnection.customSlope);
                if (ns != selectedConnection.customSlope) { Undo.RegisterCompleteObjectUndo(dungeonData, "Change Custom Slope"); selectedConnection.customSlope = ns; EditorUtility.SetDirty(dungeonData); }
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
            if (newName != selectedRoom.roomName)
            { Undo.RegisterCompleteObjectUndo(dungeonData, "Change Room Name"); selectedRoom.roomName = newName; EditorUtility.SetDirty(dungeonData); }

            int newH = EditorGUILayout.IntField("Height Level:", selectedRoom.heightLevel);
            if (newH != selectedRoom.heightLevel)
            { Undo.RegisterCompleteObjectUndo(dungeonData, "Change Room Height Level"); selectedRoom.heightLevel = newH; EditorUtility.SetDirty(dungeonData); }

            // ── Per-room height & closed toggle ───────────────────
            float newRH = EditorGUILayout.FloatField("Room Height (m):", selectedRoom.roomHeight);
            newRH = Mathf.Max(0.5f, newRH);
            if (!Mathf.Approximately(newRH, selectedRoom.roomHeight))
            { Undo.RegisterCompleteObjectUndo(dungeonData, "Change Room Wall Height"); selectedRoom.roomHeight = newRH; EditorUtility.SetDirty(dungeonData); }

            bool newClosed = EditorGUILayout.Toggle("Closed (with ceiling):", selectedRoom.isClosed);
            if (newClosed != selectedRoom.isClosed)
            { Undo.RegisterCompleteObjectUndo(dungeonData, "Toggle Room Closed"); selectedRoom.isClosed = newClosed; EditorUtility.SetDirty(dungeonData); }

            if (selectedRoom.isClosed)
                EditorGUILayout.HelpBox($"Ceiling will be generated at {selectedRoom.roomHeight} m above floor.", MessageType.None);

            EditorGUILayout.LabelField("Cells:", selectedRoom.gridCells?.Count.ToString() ?? "0");
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
            EditorGUILayout.LabelField("General Settings", EditorStyles.boldLabel);

            float newCS = EditorGUILayout.Slider("Cell Size:", dungeonData.gridCellSize, 1f, 20f);
            if (newCS != dungeonData.gridCellSize) { dungeonData.gridCellSize = newCS; EditorUtility.SetDirty(dungeonData); }
            float newHL = EditorGUILayout.FloatField("Height Per Level (m):", dungeonData.heightPerLevel);
            if (newHL != dungeonData.heightPerLevel) { dungeonData.heightPerLevel = newHL; EditorUtility.SetDirty(dungeonData); }
            int newTS = EditorGUILayout.IntSlider("Tunnel Segments:", dungeonData.tunnelSegments, 4, 16);
            if (newTS != dungeonData.tunnelSegments) { dungeonData.tunnelSegments = newTS; EditorUtility.SetDirty(dungeonData); }
            float newTH = EditorGUILayout.FloatField("Tunnel Height (m):", dungeonData.defaultTunnelHeight);
            if (newTH != dungeonData.defaultTunnelHeight) { dungeonData.defaultTunnelHeight = newTH; EditorUtility.SetDirty(dungeonData); }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Mesh Replacement (Optional)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Sin Mesh Set se usan primitivas de Unity. Compatible URP/HDRP/Built-in.", MessageType.Info);
            MeshReplacementSet newMS = (MeshReplacementSet)EditorGUILayout.ObjectField("Mesh Set:", dungeonData.meshSet, typeof(MeshReplacementSet), false);
            if (newMS != dungeonData.meshSet) { dungeonData.meshSet = newMS; EditorUtility.SetDirty(dungeonData); }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Nodes:",       (dungeonData.nodes?.Count       ?? 0).ToString());
            EditorGUILayout.LabelField("Connections:", (dungeonData.connections?.Count ?? 0).ToString());
            EditorGUILayout.LabelField("Rooms:",       (dungeonData.rooms?.Count       ?? 0).ToString());

            if (currentMode == ToolMode.DefineRoomNumeric)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Numeric Room", EditorStyles.boldLabel);
                roomSizeNumeric     = EditorGUILayout.Vector2IntField("Size:",     roomSizeNumeric);
                roomPositionNumeric = EditorGUILayout.Vector2IntField("Position:", roomPositionNumeric);
                if (GUILayout.Button("Create Room")) CreateNumericRoom();
            }
        }
        #endregion

        #region Helpers
        private Vector2 GridToScreen(Vector2Int gp, Rect r)
        {
            float cs = dungeonData.gridCellSize * gridZoom;
            return new Vector2(r.x + gp.x * cs + gridOffset.x, r.y + gp.y * cs + gridOffset.y);
        }
        private Vector2 GridToScreenCenter(Vector2Int gp, Rect r)
        {
            float cs = dungeonData.gridCellSize * gridZoom;
            return new Vector2(r.x + (gp.x + 0.5f) * cs + gridOffset.x, r.y + (gp.y + 0.5f) * cs + gridOffset.y);
        }
        private Vector2Int ScreenToGrid(Vector2 sp, Rect r)
        {
            float cs = dungeonData.gridCellSize * gridZoom;
            return new Vector2Int(
                Mathf.FloorToInt((sp.x - r.x - gridOffset.x) / cs),
                Mathf.FloorToInt((sp.y - r.y - gridOffset.y) / cs));
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

        private DungeonConnection GetConnectionAtPosition(Vector2 mousePos, Rect canvasRect, float threshold = 15f)
        {
            if (dungeonData.connections == null) return null;
            
            foreach (var conn in dungeonData.connections)
            {
                var nA = dungeonData.GetNode(conn.nodeAId);
                var nB = dungeonData.GetNode(conn.nodeBId);
                if (nA == null || nB == null) continue;

                // Only detect links on current floor level conceptually, though connections can bridge floors.
                if (nA.heightLevel != currentHeightLevel && nB.heightLevel != currentHeightLevel) continue;

                Vector2 pA = GridToScreenCenter(nA.gridPosition, canvasRect);
                Vector2 pB = GridToScreenCenter(nB.gridPosition, canvasRect);

                float dist = DistancePointLine(mousePos, pA, pB);
                if (dist <= threshold)
                {
                    return conn;
                }
            }
            return null;
        }

        private float DistancePointLine(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
        {
            float l2 = (lineStart - lineEnd).sqrMagnitude;
            if (l2 == 0f) return Vector2.Distance(point, lineStart);
            float t = Mathf.Max(0, Mathf.Min(1, Vector2.Dot(point - lineStart, lineEnd - lineStart) / l2));
            Vector2 projection = lineStart + t * (lineEnd - lineStart);
            return Vector2.Distance(point, projection);
        }

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

        // ── Object Placement Methods ───────────────────────────────
        private void DrawObjects(Rect canvasRect)
        {
            if (dungeonData == null || dungeonData.objects == null) return;
            Handles.BeginGUI();
            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            labelStyle.normal.textColor = Color.white;

            foreach (var obj in dungeonData.objects)
            {
                if (obj.heightLevel != currentHeightLevel) continue;
                Vector2 sp = GridToScreenCenter(obj.gridPosition, canvasRect);
                float cs = dungeonData.gridCellSize * gridZoom;
                float halfCs = cs * 0.35f;

                Color oc = obj == selectedObject ? Color.white : obj.gizmoColor;

                switch (obj.primitiveShape)
                {
                    case ObjectPrimitiveShape.Cube:
                        Handles.color = oc;
                        Rect cubeR = new Rect(sp.x - halfCs, sp.y - halfCs, halfCs * 2, halfCs * 2);
                        Handles.DrawSolidRectangleWithOutline(cubeR, new Color(oc.r, oc.g, oc.b, 0.5f), oc);
                        break;
                    case ObjectPrimitiveShape.Sphere:
                        Handles.color = new Color(oc.r, oc.g, oc.b, 0.5f);
                        Handles.DrawSolidDisc(sp, Vector3.forward, halfCs);
                        Handles.color = oc;
                        Handles.DrawWireDisc(sp, Vector3.forward, halfCs);
                        break;
                    case ObjectPrimitiveShape.Cylinder:
                        Handles.color = oc;
                        Rect cylR = new Rect(sp.x - halfCs * 0.5f, sp.y - halfCs, halfCs, halfCs * 2);
                        Handles.DrawSolidRectangleWithOutline(cylR, new Color(oc.r, oc.g, oc.b, 0.5f), oc);
                        break;
                    case ObjectPrimitiveShape.Capsule:
                        Handles.color = new Color(oc.r, oc.g, oc.b, 0.5f);
                        Handles.DrawSolidDisc(sp, Vector3.forward, halfCs * 0.7f);
                        Handles.color = oc;
                        Handles.DrawWireDisc(sp, Vector3.forward, halfCs * 0.7f);
                        break;
                }

                // Rotation indicator line
                float radians = obj.rotationY * Mathf.Deg2Rad;
                Vector2 arrow = new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians)) * halfCs;
                Handles.color = Color.red;
                Handles.DrawLine(sp, sp + arrow);

                // Name label
                GUI.Label(new Rect(sp.x - 30, sp.y + halfCs + 2, 60, 14), obj.objectName, labelStyle);
            }
            Handles.EndGUI();
        }

        private void HandlePlaceObjectInput(Event e, Vector2Int gridPos)
        {
            // R key rotates by 90°
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.R)
            {
                currentObjectRotation = (currentObjectRotation + 90f) % 360f;
                e.Use(); Repaint(); return;
            }

            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
            {
                // Check if clicking on existing object (select it)
                DungeonObject existing = GetObjectAtPosition(gridPos, currentHeightLevel);
                if (existing != null)
                {
                    selectedObject = existing;
                    selectedNode = null; selectedConnection = null; selectedRoom = null;
                    e.Use(); Repaint(); return;
                }

                // Place new object
                Undo.RegisterCompleteObjectUndo(dungeonData, "Place Object");
                var newObj = new DungeonObject(gridPos, currentHeightLevel);
                newObj.primitiveShape = currentObjectShape;
                newObj.rotationY = currentObjectRotation;
                newObj.objectName = currentObjectShape.ToString() + "_" + (dungeonData.objects.Count + 1);
                dungeonData.objects.Add(newObj);
                selectedObject = newObj;
                selectedNode = null; selectedConnection = null; selectedRoom = null;
                EditorUtility.SetDirty(dungeonData);
                e.Use(); Repaint();
            }
            // Right-click to delete
            else if (e.type == EventType.MouseDown && e.button == 1)
            {
                DungeonObject obj = GetObjectAtPosition(gridPos, currentHeightLevel);
                if (obj != null)
                {
                    Undo.RegisterCompleteObjectUndo(dungeonData, "Delete Object");
                    dungeonData.objects.Remove(obj);
                    if (selectedObject == obj) selectedObject = null;
                    EditorUtility.SetDirty(dungeonData);
                    e.Use(); Repaint();
                }
            }
        }

        private void DrawObjectProperties()
        {
            if (selectedObject == null || dungeonData == null) { ClearSelection(); return; }
            EditorGUILayout.LabelField("Object Properties", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("ID:", selectedObject.id);

            string newName = EditorGUILayout.TextField("Name:", selectedObject.objectName);
            if (newName != selectedObject.objectName) { Undo.RegisterCompleteObjectUndo(dungeonData, "Change Object Name"); selectedObject.objectName = newName; EditorUtility.SetDirty(dungeonData); }

            string newTag = EditorGUILayout.TextField("Tag:", selectedObject.objectTag);
            if (newTag != selectedObject.objectTag) { Undo.RegisterCompleteObjectUndo(dungeonData, "Change Object Tag"); selectedObject.objectTag = newTag; EditorUtility.SetDirty(dungeonData); }

            EditorGUILayout.LabelField("Position:", selectedObject.gridPosition.ToString());

            int newH = EditorGUILayout.IntField("Height Level:", selectedObject.heightLevel);
            if (newH != selectedObject.heightLevel) { Undo.RegisterCompleteObjectUndo(dungeonData, "Change Object Height"); selectedObject.heightLevel = newH; EditorUtility.SetDirty(dungeonData); }

            float newRot = EditorGUILayout.Slider("Rotation Y°:", selectedObject.rotationY, 0f, 360f);
            if (newRot != selectedObject.rotationY) { Undo.RegisterCompleteObjectUndo(dungeonData, "Change Object Rotation"); selectedObject.rotationY = newRot; EditorUtility.SetDirty(dungeonData); }

            ObjectPrimitiveShape newShape = (ObjectPrimitiveShape)EditorGUILayout.EnumPopup("Primitive Shape:", selectedObject.primitiveShape);
            if (newShape != selectedObject.primitiveShape) { Undo.RegisterCompleteObjectUndo(dungeonData, "Change Object Shape"); selectedObject.primitiveShape = newShape; EditorUtility.SetDirty(dungeonData); }

            Vector3 newScale = EditorGUILayout.Vector3Field("Scale:", selectedObject.scale);
            if (newScale != selectedObject.scale) { Undo.RegisterCompleteObjectUndo(dungeonData, "Change Object Scale"); selectedObject.scale = newScale; EditorUtility.SetDirty(dungeonData); }

            Color newColor = EditorGUILayout.ColorField("Gizmo Color:", selectedObject.gizmoColor);
            if (newColor != selectedObject.gizmoColor) { Undo.RegisterCompleteObjectUndo(dungeonData, "Change Object Color"); selectedObject.gizmoColor = newColor; EditorUtility.SetDirty(dungeonData); }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Prefab Override (optional)", EditorStyles.miniLabel);
            GameObject newPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab:", selectedObject.prefabOverride, typeof(GameObject), false);
            if (newPrefab != selectedObject.prefabOverride) { Undo.RegisterCompleteObjectUndo(dungeonData, "Change Object Prefab"); selectedObject.prefabOverride = newPrefab; EditorUtility.SetDirty(dungeonData); }

            EditorGUILayout.Space();
            if (GUILayout.Button("Delete Object"))
            {
                Undo.RegisterCompleteObjectUndo(dungeonData, "Delete Object");
                dungeonData.objects.Remove(selectedObject);
                EditorUtility.SetDirty(dungeonData); selectedObject = null;
            }
        }

        private DungeonObject GetObjectAtPosition(Vector2Int gp, int h) =>
            dungeonData.objects.Find(o => o.gridPosition == gp && o.heightLevel == h);

        private DungeonWall GetWallAtPosition(Vector2Int gp, int h) =>
            dungeonData.walls.Find(w => w.gridCells.Contains(gp) && w.heightLevel == h);

        private void DrawWallProperties()
        {
            if (selectedWall == null || dungeonData == null) { ClearSelection(); return; }
            EditorGUILayout.LabelField("Wall Properties", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("ID:", selectedWall.id);

            string newName = EditorGUILayout.TextField("Name:", selectedWall.wallName);
            if (newName != selectedWall.wallName) { Undo.RegisterCompleteObjectUndo(dungeonData, "Change Wall Name"); selectedWall.wallName = newName; EditorUtility.SetDirty(dungeonData); }

            EditorGUILayout.LabelField("Cells:", selectedWall.gridCells.Count.ToString());

            int newH = EditorGUILayout.IntField("Height Level:", selectedWall.heightLevel);
            if (newH != selectedWall.heightLevel) { Undo.RegisterCompleteObjectUndo(dungeonData, "Change Wall Height Level"); selectedWall.heightLevel = newH; EditorUtility.SetDirty(dungeonData); }

            float newCH = EditorGUILayout.FloatField("Custom Height (0=Auto):", selectedWall.customHeight);
            if (newCH != selectedWall.customHeight) { Undo.RegisterCompleteObjectUndo(dungeonData, "Change Wall Custom Height"); selectedWall.customHeight = Mathf.Max(0f, newCH); EditorUtility.SetDirty(dungeonData); }

            Color newC = EditorGUILayout.ColorField("Color:", selectedWall.color);
            if (newC != selectedWall.color) { Undo.RegisterCompleteObjectUndo(dungeonData, "Change Wall Color"); selectedWall.color = newC; EditorUtility.SetDirty(dungeonData); }

            EditorGUILayout.Space();
            if (GUILayout.Button("Delete Wall"))
            {
                Undo.RegisterCompleteObjectUndo(dungeonData, "Delete Wall");
                dungeonData.walls.Remove(selectedWall);
                EditorUtility.SetDirty(dungeonData); selectedWall = null;
            }
        }

        private void CreateNumericRoom()
        {
            if (dungeonData == null) return;
            Undo.RegisterCompleteObjectUndo(dungeonData, "Create Numeric Room");
            DungeonRoom nr = new DungeonRoom(currentHeightLevel, currentRoomHeight);
            nr.isClosed   = currentRoomClosed;
            nr.roomName   = "Room_" + (dungeonData.rooms.Count + 1);
            nr.manualSize = roomSizeNumeric;
            nr.shape      = RoomShape.Rectangular;
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
            selectedObject = null;
        }
        #endregion

        #region v1.0.8 Features
        private void CopySelectedRoom()
        {
            if (selectedRoom == null) { Debug.Log("Select a room first."); return; }
            copiedRoom = new DungeonRoom(selectedRoom.heightLevel, selectedRoom.roomHeight);
            copiedRoom.isClosed  = selectedRoom.isClosed;
            copiedRoom.gridCells = new List<Vector2Int>(selectedRoom.gridCells);
            copiedRoom.roomName  = selectedRoom.roomName + "_Copy";
            Debug.Log($"Copied: {selectedRoom.roomName}");
        }

        private void PasteRoom()
        {
            if (copiedRoom == null || dungeonData == null) return;
            Undo.RegisterCompleteObjectUndo(dungeonData, "Paste Room");
            DungeonRoom nr = new DungeonRoom(copiedRoom.heightLevel, copiedRoom.roomHeight);
            nr.isClosed  = copiedRoom.isClosed;
            nr.roomName  = copiedRoom.roomName;
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
            DungeonRoom nr = new DungeonRoom(currentHeightLevel, currentRoomHeight);
            nr.isClosed  = currentRoomClosed;
            nr.roomName  = template.name + "_" + (dungeonData.rooms.Count + 1);
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
            if (generatedDungeon != null)
            {
                if (!EditorUtility.DisplayDialog("Replace?", "Delete existing and regenerate?", "Yes", "Cancel")) return;
                DestroyImmediate(generatedDungeon);
            }
            generatedDungeon = DungeonGenerator.Generate(dungeonData);
            if (generatedDungeon != null) { Selection.activeGameObject = generatedDungeon; EditorGUIUtility.PingObject(generatedDungeon); }
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
