using UnityEngine;
using DungeonPainter.Data;

namespace DungeonPainter.Core
{
    /// <summary>
    /// Utility class for validating and analyzing dungeon data
    /// </summary>
    public static class DungeonUtilities
    {
        public static class Validation
        {
            /// <summary>
            /// Validates the dungeon data for common issues
            /// </summary>
            public static ValidationResult ValidateDungeon(DungeonData data)
            {
                ValidationResult result = new ValidationResult();

                if (data == null)
                {
                    result.AddError("DungeonData is null");
                    return result;
                }

                // Check for orphaned nodes (nodes with no connections)
                foreach (var node in data.nodes)
                {
                    bool hasConnection = data.connections.Exists(c => 
                        c.nodeAId == node.id || c.nodeBId == node.id);
                    
                    if (!hasConnection)
                    {
                        result.AddWarning($"Node {node.id} at {node.gridPosition} has no connections");
                    }
                }

                // Check for invalid connections
                foreach (var connection in data.connections)
                {
                    var nodeA = data.GetNode(connection.nodeAId);
                    var nodeB = data.GetNode(connection.nodeBId);

                    if (nodeA == null)
                    {
                        result.AddError($"Connection {connection.id} references invalid node A: {connection.nodeAId}");
                    }
                    if (nodeB == null)
                    {
                        result.AddError($"Connection {connection.id} references invalid node B: {connection.nodeBId}");
                    }

                    // Check for duplicate connections
                    int duplicates = data.connections.FindAll(c =>
                        (c.nodeAId == connection.nodeAId && c.nodeBId == connection.nodeBId) ||
                        (c.nodeAId == connection.nodeBId && c.nodeBId == connection.nodeAId)
                    ).Count;

                    if (duplicates > 1)
                    {
                        result.AddWarning($"Duplicate connection between nodes {connection.nodeAId} and {connection.nodeBId}");
                    }
                }

                // Check for empty rooms
                foreach (var room in data.rooms)
                {
                    if (room.gridCells.Count == 0)
                    {
                        result.AddWarning($"Room {room.roomName} has no cells");
                    }
                }

                return result;
            }
        }

        public static class Statistics
        {
            /// <summary>
            /// Calculate statistics about the dungeon
            /// </summary>
            public static DungeonStats CalculateStats(DungeonData data)
            {
                DungeonStats stats = new DungeonStats();

                if (data == null)
                    return stats;

                stats.nodeCount = data.nodes.Count;
                stats.connectionCount = data.connections.Count;
                stats.roomCount = data.rooms.Count;

                // Calculate total area
                foreach (var room in data.rooms)
                {
                    stats.totalCells += room.gridCells.Count;
                }
                stats.totalArea = stats.totalCells * data.gridCellSize * data.gridCellSize;

                // Calculate height range
                int minHeight = 0;
                int maxHeight = 0;

                foreach (var node in data.nodes)
                {
                    minHeight = Mathf.Min(minHeight, node.heightLevel);
                    maxHeight = Mathf.Max(maxHeight, node.heightLevel);
                }

                foreach (var room in data.rooms)
                {
                    minHeight = Mathf.Min(minHeight, room.heightLevel);
                    maxHeight = Mathf.Max(maxHeight, room.heightLevel);
                }

                stats.heightLevels = maxHeight - minHeight + 1;
                stats.minHeight = minHeight;
                stats.maxHeight = maxHeight;

                // Calculate total corridor length (approximate)
                foreach (var connection in data.connections)
                {
                    var nodeA = data.GetNode(connection.nodeAId);
                    var nodeB = data.GetNode(connection.nodeBId);

                    if (nodeA != null && nodeB != null)
                    {
                        Vector3 worldPosA = new Vector3(
                            nodeA.gridPosition.x * data.gridCellSize,
                            nodeA.heightLevel * data.heightPerLevel,
                            nodeA.gridPosition.y * data.gridCellSize
                        );

                        Vector3 worldPosB = new Vector3(
                            nodeB.gridPosition.x * data.gridCellSize,
                            nodeB.heightLevel * data.heightPerLevel,
                            nodeB.gridPosition.y * data.gridCellSize
                        );

                        stats.totalCorridorLength += Vector3.Distance(worldPosA, worldPosB);
                    }
                }

                // Count connection types
                foreach (var connection in data.connections)
                {
                    switch (connection.transitionType)
                    {
                        case ConnectionType.Flat:
                            stats.flatCorridors++;
                            break;
                        case ConnectionType.Ramp:
                            stats.ramps++;
                            break;
                        case ConnectionType.Stairs:
                            stats.stairs++;
                            break;
                        case ConnectionType.Tunnel:
                            stats.tunnels++;
                            break;
                    }
                }

                return stats;
            }
        }

        public static class Bounds
        {
            /// <summary>
            /// Calculate the world-space bounds of the entire dungeon
            /// </summary>
            public static UnityEngine.Bounds CalculateBounds(DungeonData data)
            {
                if (data == null || (data.nodes.Count == 0 && data.rooms.Count == 0))
                {
                    return new UnityEngine.Bounds(Vector3.zero, Vector3.zero);
                }

                Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

                // Check nodes
                foreach (var node in data.nodes)
                {
                    Vector3 worldPos = new Vector3(
                        node.gridPosition.x * data.gridCellSize,
                        node.heightLevel * data.heightPerLevel,
                        node.gridPosition.y * data.gridCellSize
                    );

                    min = Vector3.Min(min, worldPos);
                    max = Vector3.Max(max, worldPos);
                }

                // Check rooms
                foreach (var room in data.rooms)
                {
                    foreach (var cell in room.gridCells)
                    {
                        Vector3 worldPos = new Vector3(
                            cell.x * data.gridCellSize,
                            room.heightLevel * data.heightPerLevel,
                            cell.y * data.gridCellSize
                        );

                        min = Vector3.Min(min, worldPos);
                        max = Vector3.Max(max, worldPos);
                    }
                }

                Vector3 center = (min + max) * 0.5f;
                Vector3 size = max - min;

                return new UnityEngine.Bounds(center, size);
            }
        }
    }

    /// <summary>
    /// Result of dungeon validation
    /// </summary>
    public class ValidationResult
    {
        public System.Collections.Generic.List<string> errors = new System.Collections.Generic.List<string>();
        public System.Collections.Generic.List<string> warnings = new System.Collections.Generic.List<string>();

        public bool IsValid => errors.Count == 0;
        public bool HasWarnings => warnings.Count > 0;

        public void AddError(string error)
        {
            errors.Add(error);
        }

        public void AddWarning(string warning)
        {
            warnings.Add(warning);
        }

        public string GetReport()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            if (IsValid && !HasWarnings)
            {
                sb.AppendLine("✓ Dungeon validation passed with no issues");
            }
            else
            {
                if (errors.Count > 0)
                {
                    sb.AppendLine($"❌ {errors.Count} Error(s):");
                    foreach (var error in errors)
                    {
                        sb.AppendLine($"  - {error}");
                    }
                }

                if (warnings.Count > 0)
                {
                    sb.AppendLine($"⚠ {warnings.Count} Warning(s):");
                    foreach (var warning in warnings)
                    {
                        sb.AppendLine($"  - {warning}");
                    }
                }
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Statistics about a dungeon
    /// </summary>
    public class DungeonStats
    {
        public int nodeCount;
        public int connectionCount;
        public int roomCount;
        public int totalCells;
        public float totalArea;
        public int heightLevels;
        public int minHeight;
        public int maxHeight;
        public float totalCorridorLength;

        // Connection type counts
        public int flatCorridors;
        public int ramps;
        public int stairs;
        public int tunnels;

        public override string ToString()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Dungeon Statistics ===");
            sb.AppendLine($"Nodes: {nodeCount}");
            sb.AppendLine($"Connections: {connectionCount}");
            sb.AppendLine($"Rooms: {roomCount}");
            sb.AppendLine($"Total Cells: {totalCells}");
            sb.AppendLine($"Total Area: {totalArea:F2} m²");
            sb.AppendLine($"Height Levels: {heightLevels} (from {minHeight} to {maxHeight})");
            sb.AppendLine($"Total Corridor Length: {totalCorridorLength:F2} m");
            sb.AppendLine($"\nConnection Types:");
            sb.AppendLine($"  Flat: {flatCorridors}");
            sb.AppendLine($"  Ramps: {ramps}");
            sb.AppendLine($"  Stairs: {stairs}");
            sb.AppendLine($"  Tunnels: {tunnels}");
            return sb.ToString();
        }
    }
}
