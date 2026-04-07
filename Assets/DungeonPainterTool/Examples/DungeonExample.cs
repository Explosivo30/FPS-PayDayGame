using UnityEngine;
using DungeonPainter.Data;

namespace DungeonPainter.Examples
{
    /// <summary>
    /// Example script that programmatically creates a simple dungeon
    /// Useful for testing or creating procedural templates
    /// </summary>
    public class DungeonExample : MonoBehaviour
    {
        [Header("References")]
        public DungeonData dungeonData;

        [Header("Example Settings")]
        public bool createOnStart = false;

        private void Start()
        {
            if (createOnStart && dungeonData != null)
            {
                CreateExampleDungeon();
            }
        }

        [ContextMenu("Create Example Dungeon")]
        public void CreateExampleDungeon()
        {
            if (dungeonData == null)
            {
                Debug.LogError("DungeonData is not assigned!");
                return;
            }

            // Clear existing data
            dungeonData.ClearAll();

            // Configure basic settings
            dungeonData.gridCellSize = 5f;
            dungeonData.heightPerLevel = 3f;

            // === Create Example: Two Rooms Connected by a Corridor ===

            // Room 1: Starting room (level 0)
            DungeonRoom room1 = new DungeonRoom(0);
            room1.roomName = "Starting Chamber";
            room1.shape = RoomShape.Rectangular;
            
            // 5x5 room at origin
            for (int x = 0; x < 5; x++)
            {
                for (int y = 0; y < 5; y++)
                {
                    room1.gridCells.Add(new Vector2Int(x, y));
                }
            }
            dungeonData.rooms.Add(room1);

            // Room 2: Treasure room (level -1, one level below)
            DungeonRoom room2 = new DungeonRoom(-1);
            room2.roomName = "Treasure Chamber";
            room2.shape = RoomShape.Rectangular;
            
            // 4x4 room offset from first room
            for (int x = 10; x < 14; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    room2.gridCells.Add(new Vector2Int(x, y));
                }
            }
            dungeonData.rooms.Add(room2);

            // Connection nodes
            DungeonNode node1 = new DungeonNode(new Vector2Int(4, 2), 0, NodeType.Corridor);
            DungeonNode node2 = new DungeonNode(new Vector2Int(10, 2), -1, NodeType.Corridor);

            dungeonData.nodes.Add(node1);
            dungeonData.nodes.Add(node2);

            // Create connection with a ramp
            DungeonConnection connection = new DungeonConnection(node1.id, node2.id, 3f);
            connection.transitionType = ConnectionType.Ramp;
            
            // Variable width corridor (narrow -> wide -> narrow)
            connection.widthPoints.Clear();
            connection.widthPoints.Add(new WidthPoint { normalizedPosition = 0f, width = 2f });
            connection.widthPoints.Add(new WidthPoint { normalizedPosition = 0.5f, width = 5f });
            connection.widthPoints.Add(new WidthPoint { normalizedPosition = 1f, width = 2f });

            dungeonData.connections.Add(connection);

            // Rebuild cache
            dungeonData.RebuildCache();

            Debug.Log("Example dungeon created! Open Dungeon Painter window to visualize and modify.");
            Debug.Log($"Created {dungeonData.rooms.Count} rooms, {dungeonData.nodes.Count} nodes, and {dungeonData.connections.Count} connections");

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(dungeonData);
#endif
        }

        [ContextMenu("Create Multi-Level Example")]
        public void CreateMultiLevelExample()
        {
            if (dungeonData == null)
            {
                Debug.LogError("DungeonData is not assigned!");
                return;
            }

            dungeonData.ClearAll();
            dungeonData.gridCellSize = 5f;

            // Level 0: Main floor
            DungeonRoom mainHall = new DungeonRoom(0);
            mainHall.roomName = "Main Hall";
            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    mainHall.gridCells.Add(new Vector2Int(x, y));
                }
            }
            dungeonData.rooms.Add(mainHall);

            // Level 1: Upper balcony
            DungeonRoom balcony = new DungeonRoom(1);
            balcony.roomName = "Balcony";
            for (int x = 0; x < 4; x++)
            {
                for (int y = 10; y < 14; y++)
                {
                    balcony.gridCells.Add(new Vector2Int(x, y));
                }
            }
            dungeonData.rooms.Add(balcony);

            // Level -2: Dungeon depths
            DungeonRoom depths = new DungeonRoom(-2);
            depths.roomName = "Deep Chamber";
            for (int x = 10; x < 15; x++)
            {
                for (int y = 0; y < 5; y++)
                {
                    depths.gridCells.Add(new Vector2Int(x, y));
                }
            }
            dungeonData.rooms.Add(depths);

            // Connect with stairs to balcony
            DungeonNode nodeMainToBalcony1 = new DungeonNode(new Vector2Int(2, 8), 0, NodeType.Corridor);
            DungeonNode nodeMainToBalcony2 = new DungeonNode(new Vector2Int(2, 10), 1, NodeType.Corridor);
            dungeonData.nodes.Add(nodeMainToBalcony1);
            dungeonData.nodes.Add(nodeMainToBalcony2);

            DungeonConnection stairsUp = new DungeonConnection(nodeMainToBalcony1.id, nodeMainToBalcony2.id, 3f);
            stairsUp.transitionType = ConnectionType.Stairs;
            stairsUp.stepCount = 10;
            dungeonData.connections.Add(stairsUp);

            // Connect with ramp to depths
            DungeonNode nodeMainToDepths1 = new DungeonNode(new Vector2Int(8, 2), 0, NodeType.Corridor);
            DungeonNode nodeMainToDepths2 = new DungeonNode(new Vector2Int(10, 2), -2, NodeType.Corridor);
            dungeonData.nodes.Add(nodeMainToDepths1);
            dungeonData.nodes.Add(nodeMainToDepths2);

            DungeonConnection rampDown = new DungeonConnection(nodeMainToDepths1.id, nodeMainToDepths2.id, 4f);
            rampDown.transitionType = ConnectionType.Ramp;
            dungeonData.connections.Add(rampDown);

            dungeonData.RebuildCache();

            Debug.Log("Multi-level example created!");

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(dungeonData);
#endif
        }
    }
}
