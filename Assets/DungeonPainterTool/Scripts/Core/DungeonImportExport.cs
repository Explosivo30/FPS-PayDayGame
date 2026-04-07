using UnityEngine;
using System.IO;
using System.Text;
using DungeonPainter.Data;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DungeonPainter.Core
{
    /// <summary>
    /// Utilities for importing and exporting dungeon layouts to JSON
    /// Useful for version control, sharing, and templates
    /// </summary>
    public static class DungeonImportExport
    {
        /// <summary>
        /// Export dungeon data to JSON file
        /// </summary>
        public static void ExportToJSON(DungeonData data, string filepath)
        {
            if (data == null)
            {
                Debug.LogError("Cannot export null DungeonData");
                return;
            }

            try
            {
                DungeonDataSerializable serializable = new DungeonDataSerializable(data);
                string json = JsonUtility.ToJson(serializable, true);
                
                File.WriteAllText(filepath, json, Encoding.UTF8);
                Debug.Log($"Dungeon exported successfully to: {filepath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to export dungeon: {e.Message}");
            }
        }

        /// <summary>
        /// Import dungeon data from JSON file
        /// </summary>
        public static void ImportFromJSON(DungeonData targetData, string filepath)
        {
            if (targetData == null)
            {
                Debug.LogError("Target DungeonData is null");
                return;
            }

            if (!File.Exists(filepath))
            {
                Debug.LogError($"File not found: {filepath}");
                return;
            }

            try
            {
                string json = File.ReadAllText(filepath, Encoding.UTF8);
                DungeonDataSerializable serializable = JsonUtility.FromJson<DungeonDataSerializable>(json);
                
                serializable.ApplyTo(targetData);
                targetData.RebuildCache();

#if UNITY_EDITOR
                EditorUtility.SetDirty(targetData);
#endif

                Debug.Log($"Dungeon imported successfully from: {filepath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to import dungeon: {e.Message}");
            }
        }

        /// <summary>
        /// Create a template from current dungeon (strips unique IDs)
        /// </summary>
        public static void ExportAsTemplate(DungeonData data, string filepath)
        {
            if (data == null)
            {
                Debug.LogError("Cannot export null DungeonData");
                return;
            }

            try
            {
                // Create a copy and regenerate IDs
                DungeonDataSerializable serializable = new DungeonDataSerializable(data);
                serializable.isTemplate = true;
                
                string json = JsonUtility.ToJson(serializable, true);
                File.WriteAllText(filepath, json, Encoding.UTF8);
                
                Debug.Log($"Template exported successfully to: {filepath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to export template: {e.Message}");
            }
        }

#if UNITY_EDITOR
        [MenuItem("Assets/Dungeon Painter/Export to JSON", true)]
        private static bool ValidateExportMenuItem()
        {
            return Selection.activeObject is DungeonData;
        }

        [MenuItem("Assets/Dungeon Painter/Export to JSON")]
        private static void ExportMenuItem()
        {
            DungeonData data = Selection.activeObject as DungeonData;
            if (data == null) return;

            string path = EditorUtility.SaveFilePanel(
                "Export Dungeon to JSON",
                Application.dataPath,
                data.name + ".json",
                "json"
            );

            if (!string.IsNullOrEmpty(path))
            {
                ExportToJSON(data, path);
            }
        }

        [MenuItem("Assets/Dungeon Painter/Import from JSON", true)]
        private static bool ValidateImportMenuItem()
        {
            return Selection.activeObject is DungeonData;
        }

        [MenuItem("Assets/Dungeon Painter/Import from JSON")]
        private static void ImportMenuItem()
        {
            DungeonData data = Selection.activeObject as DungeonData;
            if (data == null) return;

            string path = EditorUtility.OpenFilePanel(
                "Import Dungeon from JSON",
                Application.dataPath,
                "json"
            );

            if (!string.IsNullOrEmpty(path))
            {
                if (EditorUtility.DisplayDialog(
                    "Import Dungeon",
                    "This will replace all current dungeon data. Are you sure?",
                    "Yes", "Cancel"))
                {
                    ImportFromJSON(data, path);
                }
            }
        }

        [MenuItem("Assets/Dungeon Painter/Export as Template", true)]
        private static bool ValidateExportTemplateMenuItem()
        {
            return Selection.activeObject is DungeonData;
        }

        [MenuItem("Assets/Dungeon Painter/Export as Template")]
        private static void ExportTemplateMenuItem()
        {
            DungeonData data = Selection.activeObject as DungeonData;
            if (data == null) return;

            string path = EditorUtility.SaveFilePanel(
                "Export Dungeon Template",
                Application.dataPath,
                data.name + "_template.json",
                "json"
            );

            if (!string.IsNullOrEmpty(path))
            {
                ExportAsTemplate(data, path);
            }
        }
#endif
    }

    /// <summary>
    /// Serializable version of DungeonData for JSON export/import
    /// Unity's JsonUtility doesn't handle ScriptableObjects directly
    /// </summary>
    [System.Serializable]
    public class DungeonDataSerializable
    {
        public float gridCellSize;
        public float heightPerLevel;
        public int tunnelSegments;
        public float defaultTunnelHeight;
        public bool isTemplate;

        public DungeonNode[] nodes;
        public DungeonConnection[] connections;
        public DungeonRoom[] rooms;
        public DungeonObject[] objects;

        public DungeonDataSerializable() { }

        public DungeonDataSerializable(DungeonData data)
        {
            gridCellSize = data.gridCellSize;
            heightPerLevel = data.heightPerLevel;
            tunnelSegments = data.tunnelSegments;
            defaultTunnelHeight = data.defaultTunnelHeight;
            isTemplate = false;

            nodes = data.nodes.ToArray();
            connections = data.connections.ToArray();
            rooms = data.rooms.ToArray();
            objects = data.objects != null ? data.objects.ToArray() : new DungeonObject[0];
        }

        public void ApplyTo(DungeonData data)
        {
            data.gridCellSize = gridCellSize;
            data.heightPerLevel = heightPerLevel;
            data.tunnelSegments = tunnelSegments;
            data.defaultTunnelHeight = defaultTunnelHeight;

            data.nodes.Clear();
            data.connections.Clear();
            data.rooms.Clear();
            data.objects.Clear();

            // If it's a template, regenerate IDs
            if (isTemplate)
            {
                System.Collections.Generic.Dictionary<string, string> idMap = 
                    new System.Collections.Generic.Dictionary<string, string>();

                // Copy nodes with new IDs
                foreach (var node in nodes)
                {
                    string oldId = node.id;
                    string newId = System.Guid.NewGuid().ToString().Substring(0, 8);
                    idMap[oldId] = newId;

                    DungeonNode newNode = new DungeonNode(node.gridPosition, node.heightLevel, node.type);
                    newNode.id = newId;
                    data.nodes.Add(newNode);
                }

                // Copy connections with updated node references
                foreach (var conn in connections)
                {
                    string newNodeAId = idMap.ContainsKey(conn.nodeAId) ? idMap[conn.nodeAId] : conn.nodeAId;
                    string newNodeBId = idMap.ContainsKey(conn.nodeBId) ? idMap[conn.nodeBId] : conn.nodeBId;

                    DungeonConnection newConn = new DungeonConnection(newNodeAId, newNodeBId);
                    newConn.transitionType = conn.transitionType;
                    newConn.widthPoints = new System.Collections.Generic.List<WidthPoint>(conn.widthPoints);
                    newConn.customSlope = conn.customSlope;
                    newConn.stepCount = conn.stepCount;
                    newConn.stepHeight = conn.stepHeight;
                    newConn.stepDepth = conn.stepDepth;
                    data.connections.Add(newConn);
                }

                // Copy rooms with new IDs
                foreach (var room in rooms)
                {
                    DungeonRoom newRoom = new DungeonRoom(room.heightLevel);
                    newRoom.roomName = room.roomName;
                    newRoom.shape = room.shape;
                    newRoom.manualSize = room.manualSize;
                    newRoom.gridCells = new System.Collections.Generic.List<Vector2Int>(room.gridCells);
                    data.rooms.Add(newRoom);
                }
            }
            else
            {
                // Direct copy
                data.nodes.AddRange(nodes);
                data.connections.AddRange(connections);
                data.rooms.AddRange(rooms);
                if (objects != null) data.objects.AddRange(objects);
            }
        }
    }
}
