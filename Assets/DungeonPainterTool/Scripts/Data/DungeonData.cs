using System.Collections.Generic;
using UnityEngine;

namespace DungeonPainter.Data
{
    /// <summary>
    /// Main data container for the entire dungeon layout
    /// Stores all nodes, connections, and rooms
    /// </summary>
    [CreateAssetMenu(fileName = "NewDungeonData", menuName = "Dungeon Painter/Dungeon Data")]
    public class DungeonData : ScriptableObject
    {
        [Header("Grid Configuration")]
        [Tooltip("Size of each grid cell in world units (minimum 1x1)")]
        [Range(1f, 20f)]
        public float gridCellSize = 5f;

        [Header("Dungeon Elements")]
        public List<DungeonNode> nodes = new List<DungeonNode>();
        public List<DungeonConnection> connections = new List<DungeonConnection>();
        public List<DungeonRoom> rooms = new List<DungeonRoom>();
        public List<DungeonObject> objects = new List<DungeonObject>();
        public List<DungeonWall> walls = new List<DungeonWall>();

        [Header("Mesh Replacement")]
        public MeshReplacementSet meshSet;

        [Header("Generation Settings")]
        public float heightPerLevel = 3f; // Meters per height level
        public int tunnelSegments = 8; // Segments for tunnel arc
        public float defaultTunnelHeight = 4f;

        // Runtime helpers
        private Dictionary<string, DungeonNode> nodeCache;

        public DungeonNode GetNode(string id)
        {
            if (nodeCache == null)
            {
                RebuildCache();
            }
            return nodeCache.ContainsKey(id) ? nodeCache[id] : null;
        }

        public void RebuildCache()
        {
            nodeCache = new Dictionary<string, DungeonNode>();
            foreach (var node in nodes)
            {
                if (!string.IsNullOrEmpty(node.id))
                {
                    nodeCache[node.id] = node;
                }
            }
        }

        public void ClearAll()
        {
            nodes.Clear();
            connections.Clear();
            rooms.Clear();
            objects.Clear();
            walls.Clear();
            nodeCache = null;
        }

        public string GenerateUniqueId(string prefix)
        {
            return $"{prefix}_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
        }
    }

    /// <summary>
    /// Represents a single point/node in the dungeon grid
    /// </summary>
    [System.Serializable]
    public class DungeonNode
    {
        public string id;
        public Vector2Int gridPosition;
        public int heightLevel; // 0 = base, -1 = lower, 1 = higher
        public NodeType type;

        public DungeonNode(Vector2Int pos, int height, NodeType nodeType)
        {
            id = System.Guid.NewGuid().ToString().Substring(0, 8);
            gridPosition = pos;
            heightLevel = height;
            type = nodeType;
        }
    }

    /// <summary>
    /// Connection between two nodes (corridor/hallway)
    /// </summary>
    [System.Serializable]
    public class DungeonConnection
    {
        public string id;
        public string nodeAId;
        public string nodeBId;
        
        [Header("Corridor Properties")]
        public ConnectionType transitionType = ConnectionType.Flat;
        public List<WidthPoint> widthPoints = new List<WidthPoint>();
        [Tooltip("Height of the corridor/tunnel")]
        public float height = 2f;
        
        [Header("Slope Control")]
        [Tooltip("Custom slope in degrees. Set to -1 for automatic calculation")]
        public float customSlope = -1f;
        
        [Header("Stairs Specific")]
        [Tooltip("Number of steps for stairs (only used if type is Stairs)")]
        public int stepCount = 10;
        public float stepHeight = 0.2f;
        public float stepDepth = 0.3f;

        public DungeonConnection(string nodeA, string nodeB, float defaultWidth = 3f)
        {
            id = System.Guid.NewGuid().ToString().Substring(0, 8);
            nodeAId = nodeA;
            nodeBId = nodeB;
            
            // Initialize with default width
            widthPoints.Add(new WidthPoint { normalizedPosition = 0f, width = defaultWidth });
            widthPoints.Add(new WidthPoint { normalizedPosition = 1f, width = defaultWidth });
        }

        public float GetWidthAt(float normalizedPosition)
        {
            if (widthPoints == null || widthPoints.Count == 0)
                return 3f;

            // Sort by position
            widthPoints.Sort((a, b) => a.normalizedPosition.CompareTo(b.normalizedPosition));

            // Clamp input
            normalizedPosition = Mathf.Clamp01(normalizedPosition);

            // Find surrounding points
            for (int i = 0; i < widthPoints.Count - 1; i++)
            {
                if (normalizedPosition >= widthPoints[i].normalizedPosition &&
                    normalizedPosition <= widthPoints[i + 1].normalizedPosition)
                {
                    float localT = (normalizedPosition - widthPoints[i].normalizedPosition) /
                                   (widthPoints[i + 1].normalizedPosition - widthPoints[i].normalizedPosition);
                    return Mathf.Lerp(widthPoints[i].width, widthPoints[i + 1].width, localT);
                }
            }

            return widthPoints[widthPoints.Count - 1].width;
        }
    }

    /// <summary>
    /// Defines width at a specific point along a corridor
    /// </summary>
    [System.Serializable]
    public class WidthPoint
    {
        [Range(0f, 1f)]
        public float normalizedPosition; // 0 = start, 1 = end
        [Range(1f, 20f)]
        public float width;
    }

    /// <summary>
    /// Represents a room (area painted or defined)
    /// </summary>
    [System.Serializable]
    public class DungeonRoom
    {
        public string id;
        public List<Vector2Int> gridCells = new List<Vector2Int>();
        public int heightLevel;
        public Vector2Int manualSize; // Used when defined numerically
        public RoomShape shape;
        public string roomName;

        [Tooltip("Height of this room's walls and ceiling in world units (metres).")]
        public float roomHeight = 3f;

        [Tooltip("If true, a ceiling slab will be generated over this room.")]
        public bool isClosed = false;

        public DungeonRoom(int height, float wallHeight = 3f)
        {
            id = System.Guid.NewGuid().ToString().Substring(0, 8);
            heightLevel = height;
            roomHeight  = wallHeight;
            shape = RoomShape.Custom;
            roomName = "Room";
        }

        public Bounds CalculateBounds(float cellSize)
        {
            if (gridCells.Count == 0)
            {
                return new Bounds(Vector3.zero, Vector3.zero);
            }

            Vector2Int min = gridCells[0];
            Vector2Int max = gridCells[0];

            foreach (var cell in gridCells)
            {
                min.x = Mathf.Min(min.x, cell.x);
                min.y = Mathf.Min(min.y, cell.y);
                max.x = Mathf.Max(max.x, cell.x);
                max.y = Mathf.Max(max.y, cell.y);
            }

            // Use same coordinate system as GridToWorldCenter:
            // cell (x,y) has its center at ((x+0.5)*cellSize, (y+0.5)*cellSize)
            // So the room spans from min.x*cellSize to (max.x+1)*cellSize in world space
            float worldMinX = min.x * cellSize;
            float worldMaxX = (max.x + 1) * cellSize;
            float worldMinZ = min.y * cellSize;
            float worldMaxZ = (max.y + 1) * cellSize;

            Vector3 center = new Vector3(
                (worldMinX + worldMaxX) * 0.5f,
                0f,
                (worldMinZ + worldMaxZ) * 0.5f
            );

            Vector3 size = new Vector3(
                worldMaxX - worldMinX,
                0f,
                worldMaxZ - worldMinZ
            );

            return new Bounds(center, size);
        }
    }

    /// <summary>
    /// Enumerations for different types
    /// </summary>
    public enum NodeType
    {
        Room,
        Corridor,
        Intersection
    }

    public enum ConnectionType
    {
        Flat,      // Same height level
        Ramp,      // Smooth incline
        Stairs,    // Steps
        Tunnel     // Enclosed passage
    }

    public enum RoomShape
    {
        Rectangular,
        Custom
    }

    /// <summary>
    /// Primitive shape used when no custom prefab is assigned to a dungeon object
    /// </summary>
    public enum ObjectPrimitiveShape
    {
        Cube,
        Sphere,
        Cylinder,
        Capsule
    }

    /// <summary>
    /// A placeable object within the dungeon (torch, chest, trap, decoration, etc.)
    /// </summary>
    [System.Serializable]
    public class DungeonObject
    {
        public string id;
        public Vector2Int gridPosition;
        public int heightLevel;
        public float rotationY;                               // degrees
        public string objectName = "Object";
        public string objectTag  = "Decoration";
        public ObjectPrimitiveShape primitiveShape = ObjectPrimitiveShape.Cube;
        public GameObject prefabOverride;                      // null = use primitive
        public Vector3 scale = Vector3.one;
        public Color gizmoColor = Color.yellow;

        public DungeonObject(Vector2Int pos, int height)
        {
            id = System.Guid.NewGuid().ToString().Substring(0, 8);
            gridPosition = pos;
            heightLevel = height;
        }
    }

    /// <summary>
    /// Represents an independent wall placed on the grid
    /// </summary>
    [System.Serializable]
    public class DungeonWall
    {
        public string id;
        public List<Vector2Int> gridCells = new List<Vector2Int>();
        public int heightLevel;
        public float customHeight = 0f; // 0f means auto (inherits from room or 2m default)
        public Color color = new Color(0.4f, 0.35f, 0.3f);
        public string wallName = "Wall";

        public DungeonWall(int height)
        {
            id = System.Guid.NewGuid().ToString().Substring(0, 8);
            heightLevel = height;
        }
    }
}
