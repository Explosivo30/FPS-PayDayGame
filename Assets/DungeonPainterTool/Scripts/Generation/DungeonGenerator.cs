using UnityEngine;
using System.Collections.Generic;
using DungeonPainter.Data;
using DungeonPainter.Core;

namespace DungeonPainter.Generation
{
    /// <summary>
    /// Main generator that creates 3D geometry from DungeonData
    /// Generates rooms, corridors, ramps, stairs, and tunnels
    /// </summary>
    public static class DungeonGenerator
    {
        public static GameObject Generate(DungeonData data, Transform parent = null)
        {
            if (data == null)
            {
                Debug.LogError("DungeonData is null!");
                return null;
            }

            // Create root object
            GameObject dungeonRoot = new GameObject("Dungeon_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            
            if (parent != null)
            {
                dungeonRoot.transform.SetParent(parent);
            }

            // Generate rooms first
            GameObject roomsContainer = new GameObject("Rooms");
            roomsContainer.transform.SetParent(dungeonRoot.transform);

            foreach (var room in data.rooms)
            {
                GenerateRoom(room, data, roomsContainer.transform);
            }

            // Generate connections (corridors, ramps, stairs)
            GameObject corridorsContainer = new GameObject("Corridors");
            corridorsContainer.transform.SetParent(dungeonRoot.transform);

            foreach (var connection in data.connections)
            {
                GenerateConnection(connection, data, corridorsContainer.transform);
            }

            // Generate placed objects
            if (data.objects != null && data.objects.Count > 0)
            {
                GameObject objectsContainer = new GameObject("Objects");
                objectsContainer.transform.SetParent(dungeonRoot.transform);

                foreach (var obj in data.objects)
                {
                    GenerateObject(obj, data, objectsContainer.transform);
                }
            }

            Debug.Log($"Dungeon generated with {data.rooms.Count} rooms, {data.connections.Count} connections, {(data.objects?.Count ?? 0)} objects");
            return dungeonRoot;
        }

        static void GenerateObject(DungeonObject obj, DungeonData data, Transform parent)
        {
            if (obj == null) return;

            Vector3 worldPos = GridToWorldCenter(obj.gridPosition, obj.heightLevel, data);
            Quaternion rotation = Quaternion.Euler(0f, obj.rotationY, 0f);

            GameObject go;

            if (obj.prefabOverride != null)
            {
                // Use provided prefab
                #if UNITY_EDITOR
                go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(obj.prefabOverride);
                #else
                go = GameObject.Instantiate(obj.prefabOverride);
                #endif
            }
            else
            {
                // Use Unity primitive
                PrimitiveType pt;
                switch (obj.primitiveShape)
                {
                    case ObjectPrimitiveShape.Sphere:   pt = PrimitiveType.Sphere;   break;
                    case ObjectPrimitiveShape.Cylinder:  pt = PrimitiveType.Cylinder; break;
                    case ObjectPrimitiveShape.Capsule:   pt = PrimitiveType.Capsule;  break;
                    default:                             pt = PrimitiveType.Cube;     break;
                }
                go = GameObject.CreatePrimitive(pt);
                go.GetComponent<Renderer>().material = CreateDefaultMaterial(obj.gizmoColor);
            }

            go.name = obj.objectName + "_" + obj.id;
            go.transform.SetParent(parent);
            go.transform.position = worldPos;
            go.transform.rotation = rotation;
            go.transform.localScale = obj.scale;
        }

        #region Room Generation

        // Wall side enumeration used for opening detection
        enum WallSide { Front, Back, Left, Right }

        static void GenerateRoom(DungeonRoom room, DungeonData data, Transform parent)
        {
            if (room == null || room.gridCells == null || room.gridCells.Count == 0)
            {
                Debug.LogWarning($"Room {room?.roomName ?? "Unknown"} has no cells to generate");
                return;
            }
            
            GameObject roomObj = new GameObject($"Room_{room.roomName}_{room.id}");
            roomObj.transform.SetParent(parent);

            Bounds bounds = room.CalculateBounds(data.gridCellSize);
            float worldHeight = room.heightLevel * data.heightPerLevel;

            Debug.Log($"Generating room {room.roomName}: bounds={bounds}, cells={room.gridCells.Count}");

            // ── Floor ──────────────────────────────────────────────
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(roomObj.transform);
            floor.transform.position = new Vector3(bounds.center.x, worldHeight - 0.25f, bounds.center.z);
            floor.transform.localScale = new Vector3(bounds.size.x, 0.5f, bounds.size.z);
            floor.tag = "DungeonFloor";
            
            Renderer floorRenderer = floor.GetComponent<Renderer>();
            if (floorRenderer != null)
                floorRenderer.material = CreateDefaultMaterial(new Color(0.6f, 0.6f, 0.6f));

            // ── Walls with doorway openings ────────────────────────
            float wallHeight = data.defaultTunnelHeight;
            float wallThickness = 0.5f;
            Color wallColor = new Color(0.4f, 0.35f, 0.3f);

            // Collect all connection openings that intersect this room
            var openings = GetRoomOpenings(room, data, bounds);

            // Front wall (Z-)  — runs along X axis at bounds.min.z
            GenerateWallWithOpenings(roomObj.transform, "Wall_Front",
                bounds.min.x, bounds.max.x,                       // wall span
                bounds.center.x, worldHeight + wallHeight / 2f, bounds.min.z, // pivot
                true,                                              // spanAlongX
                bounds.size.x, wallHeight, wallThickness,
                openings.TryGetValue(WallSide.Front, out var fO) ? fO : null,
                wallColor);

            // Back wall (Z+)   — runs along X axis at bounds.max.z
            GenerateWallWithOpenings(roomObj.transform, "Wall_Back",
                bounds.min.x, bounds.max.x,
                bounds.center.x, worldHeight + wallHeight / 2f, bounds.max.z,
                true,
                bounds.size.x, wallHeight, wallThickness,
                openings.TryGetValue(WallSide.Back, out var bO) ? bO : null,
                wallColor);

            // Left wall (X-)   — runs along Z axis at bounds.min.x
            GenerateWallWithOpenings(roomObj.transform, "Wall_Left",
                bounds.min.z, bounds.max.z,
                bounds.min.x, worldHeight + wallHeight / 2f, bounds.center.z,
                false,
                wallThickness, wallHeight, bounds.size.z,
                openings.TryGetValue(WallSide.Left, out var lO) ? lO : null,
                wallColor);

            // Right wall (X+)  — runs along Z axis at bounds.max.x
            GenerateWallWithOpenings(roomObj.transform, "Wall_Right",
                bounds.min.z, bounds.max.z,
                bounds.max.x, worldHeight + wallHeight / 2f, bounds.center.z,
                false,
                wallThickness, wallHeight, bounds.size.z,
                openings.TryGetValue(WallSide.Right, out var rO) ? rO : null,
                wallColor);
        }

        // ────────────────────────────────────────────────────────────
        //  Wall-opening helpers
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// For a given room, find every connection that passes through one of its
        /// four walls and record the opening interval on that wall.
        /// </summary>
        static Dictionary<WallSide, List<Vector2>> GetRoomOpenings(
            DungeonRoom room, DungeonData data, Bounds bounds)
        {
            var result = new Dictionary<WallSide, List<Vector2>>();

            // Collect node IDs that belong to this room:
            // A node is "in room" if its grid cell is in the room's cell list
            // OR if its world position falls within the room's bounding box (tolerance 0.1)
            HashSet<string> roomNodeIds = new HashSet<string>();
            Bounds expandedBounds = new Bounds(bounds.center, bounds.size + Vector3.one * 0.2f);
            expandedBounds = new Bounds(
                new Vector3(bounds.center.x, 0, bounds.center.z),
                new Vector3(bounds.size.x + 0.2f, 1000f, bounds.size.z + 0.2f)); // ignore Y for containment

            foreach (var node in data.nodes)
            {
                if (node.heightLevel != room.heightLevel) continue;

                // Check cell membership first
                if (room.gridCells.Contains(node.gridPosition))
                {
                    roomNodeIds.Add(node.id);
                    continue;
                }

                // Fallback: check if the node's world position is inside room bounds
                Vector3 nodeWorld = GridToWorldCenter(node.gridPosition, node.heightLevel, data);
                if (expandedBounds.Contains(new Vector3(nodeWorld.x, 0, nodeWorld.z)))
                {
                    roomNodeIds.Add(node.id);
                }
            }

            Debug.Log($"[WallOpenings] Room '{room.roomName}': found {roomNodeIds.Count} nodes inside room, bounds={bounds}");

            // For each connection, check if at least one end is inside this room
            foreach (var conn in data.connections)
            {
                bool aInRoom = roomNodeIds.Contains(conn.nodeAId);
                bool bInRoom = roomNodeIds.Contains(conn.nodeBId);

                // Skip if neither node is in this room
                if (!aInRoom && !bInRoom) continue;
                // Skip if both nodes are internal (corridor doesn't exit the room)
                if (aInRoom && bInRoom) continue;

                var nodeA = data.GetNode(conn.nodeAId);
                var nodeB = data.GetNode(conn.nodeBId);
                if (nodeA == null || nodeB == null) continue;

                Vector3 posA = GridToWorldCenter(nodeA.gridPosition, nodeA.heightLevel, data);
                Vector3 posB = GridToWorldCenter(nodeB.gridPosition, nodeB.heightLevel, data);

                // The inside node is the one in the room
                Vector3 insidePos  = aInRoom ? posA : posB;
                Vector3 outsidePos = aInRoom ? posB : posA;

                float corridorWidth = conn.GetWidthAt(0.5f);
                float halfW = corridorWidth * 0.5f;

                // Direction from inside toward outside
                Vector3 dir = (outsidePos - insidePos);
                float tol = 0.01f;

                Debug.Log($"[WallOpenings]   Connection {conn.id}: inside={insidePos}, outside={outsidePos}, width={corridorWidth}");

                // Check each wall plane
                TryAddWallCrossing(result, WallSide.Front, insidePos, dir, bounds,
                    insidePos.z, outsidePos.z, bounds.min.z, true, halfW, tol);
                TryAddWallCrossing(result, WallSide.Back,  insidePos, dir, bounds,
                    insidePos.z, outsidePos.z, bounds.max.z, true, halfW, tol);
                TryAddWallCrossing(result, WallSide.Left,  insidePos, dir, bounds,
                    insidePos.x, outsidePos.x, bounds.min.x, false, halfW, tol);
                TryAddWallCrossing(result, WallSide.Right, insidePos, dir, bounds,
                    insidePos.x, outsidePos.x, bounds.max.x, false, halfW, tol);
            }

            // Log results
            foreach (var kvp in result)
                Debug.Log($"[WallOpenings]   {kvp.Key} wall: {kvp.Value.Count} opening(s)");

            return result;
        }

        /// <summary>
        /// Check if the corridor line crosses a specific wall plane and, if so,
        /// record where along the wall span the opening should be.
        /// </summary>
        static void TryAddWallCrossing(
            Dictionary<WallSide, List<Vector2>> result,
            WallSide side, Vector3 origin, Vector3 dir, Bounds bounds,
            float insideCoord, float outsideCoord, float wallCoord,
            bool wallRunsAlongX, float halfWidth, float tol)
        {
            // The corridor must actually cross this plane (not be parallel)
            float dCoord = wallRunsAlongX ? dir.z : dir.x;
            if (Mathf.Abs(dCoord) < 0.001f) return;

            // Find where the line crosses the wall plane (t in 0..1 = within segment)
            float t = (wallCoord - (wallRunsAlongX ? origin.z : origin.x)) / dCoord;
            if (t < -tol || t > 1f + tol) return;

            // Calculate where along the wall span the crossing occurs
            Vector3 crossPoint = origin + dir * Mathf.Clamp01(t);
            float crossAlongWall = wallRunsAlongX ? crossPoint.x : crossPoint.z;

            // Clamp the opening to the wall span
            float wallMin = wallRunsAlongX ? bounds.min.x : bounds.min.z;
            float wallMax = wallRunsAlongX ? bounds.max.x : bounds.max.z;

            float openMin = Mathf.Max(crossAlongWall - halfWidth, wallMin);
            float openMax = Mathf.Min(crossAlongWall + halfWidth, wallMax);

            if (openMax <= openMin) return; // degenerate

            if (!result.ContainsKey(side))
                result[side] = new List<Vector2>();

            result[side].Add(new Vector2(openMin, openMax));
        }

        /// <summary>
        /// Generate one wall side, splitting it into solid segments that leave
        /// gaps (doorways) where openings are specified.
        /// </summary>
        static void GenerateWallWithOpenings(
            Transform parent, string baseName,
            float spanMin, float spanMax,             // world-space extent of wall along its run axis
            float pivotX, float pivotY, float pivotZ, // center position of the full wall
            bool spanAlongX,                           // true = wall runs along X, false = along Z
            float fullScaleX, float fullScaleY, float fullScaleZ,
            List<Vector2> openings,                    // nullable list of (min,max) intervals
            Color color)
        {
            // If there are no openings, generate single solid wall (unchanged behaviour)
            if (openings == null || openings.Count == 0)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = baseName;
                wall.transform.SetParent(parent);
                wall.transform.position = new Vector3(pivotX, pivotY, pivotZ);
                wall.transform.localScale = new Vector3(fullScaleX, fullScaleY, fullScaleZ);
                wall.tag = "DungeonWall";
                wall.GetComponent<Renderer>().material = CreateDefaultMaterial(color);
                return;
            }

            // Sort openings and merge overlapping ones
            openings.Sort((a, b) => a.x.CompareTo(b.x));
            var merged = new List<Vector2>();
            var current = openings[0];
            for (int i = 1; i < openings.Count; i++)
            {
                if (openings[i].x <= current.y)
                    current.y = Mathf.Max(current.y, openings[i].y);
                else { merged.Add(current); current = openings[i]; }
            }
            merged.Add(current);

            // Build solid segments around the openings
            float cursor = spanMin;
            int segIdx = 0;

            foreach (var opening in merged)
            {
                // Solid segment before this opening
                if (opening.x > cursor + 0.01f)
                {
                    CreateWallSegment(parent, $"{baseName}_Seg{segIdx}", cursor, opening.x,
                        spanMin, spanMax, pivotX, pivotY, pivotZ,
                        spanAlongX, fullScaleX, fullScaleY, fullScaleZ, color);
                    segIdx++;
                }
                cursor = opening.y;
            }

            // Final solid segment after last opening
            if (cursor < spanMax - 0.01f)
            {
                CreateWallSegment(parent, $"{baseName}_Seg{segIdx}", cursor, spanMax,
                    spanMin, spanMax, pivotX, pivotY, pivotZ,
                    spanAlongX, fullScaleX, fullScaleY, fullScaleZ, color);
            }
        }

        /// <summary>
        /// Create a single wall segment cube covering [segStart, segEnd] along the wall span.
        /// </summary>
        static void CreateWallSegment(
            Transform parent, string name,
            float segStart, float segEnd,
            float spanMin, float spanMax,
            float pivotX, float pivotY, float pivotZ,
            bool spanAlongX,
            float fullScaleX, float fullScaleY, float fullScaleZ,
            Color color)
        {
            float segLen = segEnd - segStart;
            float segCenter = (segStart + segEnd) * 0.5f;
            float fullSpan = spanMax - spanMin;

            var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seg.name = name;
            seg.transform.SetParent(parent);
            seg.tag = "DungeonWall";

            if (spanAlongX)
            {
                seg.transform.position = new Vector3(segCenter, pivotY, pivotZ);
                seg.transform.localScale = new Vector3(segLen, fullScaleY, fullScaleZ);
            }
            else
            {
                seg.transform.position = new Vector3(pivotX, pivotY, segCenter);
                seg.transform.localScale = new Vector3(fullScaleX, fullScaleY, segLen);
            }

            seg.GetComponent<Renderer>().material = CreateDefaultMaterial(color);
        }

        static GameObject CreateFloorMesh(Bounds bounds, float baseHeight, string roomId)
        {
            GameObject floorObj = new GameObject("Floor_" + roomId);
            MeshFilter mf = floorObj.AddComponent<MeshFilter>();
            MeshRenderer mr = floorObj.AddComponent<MeshRenderer>();
            MeshCollider mc = floorObj.AddComponent<MeshCollider>();

            Mesh mesh = new Mesh();
            mesh.name = "FloorMesh_" + roomId;

            // Simple quad for floor
            Vector3[] vertices = new Vector3[4]
            {
                new Vector3(bounds.min.x, baseHeight, bounds.min.z),
                new Vector3(bounds.max.x, baseHeight, bounds.min.z),
                new Vector3(bounds.max.x, baseHeight, bounds.max.z),
                new Vector3(bounds.min.x, baseHeight, bounds.max.z)
            };

            int[] triangles = new int[6]
            {
                0, 2, 1,
                0, 3, 2
            };

            Vector2[] uvs = new Vector2[4]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            mf.mesh = mesh;
            mc.sharedMesh = mesh;
            mr.material = CreateDefaultMaterial(new Color(0.6f, 0.6f, 0.6f));

            return floorObj;
        }

        static GameObject CreateTunnelWalls(Bounds bounds, float baseHeight, int segments, float tunnelHeight, string roomId)
        {
            GameObject wallObj = new GameObject("TunnelWalls_" + roomId);
            MeshFilter mf = wallObj.AddComponent<MeshFilter>();
            MeshRenderer mr = wallObj.AddComponent<MeshRenderer>();
            MeshCollider mc = wallObj.AddComponent<MeshCollider>();

            Mesh mesh = new Mesh();
            mesh.name = "TunnelWallMesh_" + roomId;

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>();

            float width = bounds.size.x;
            float depth = bounds.size.z;
            Vector3 center = bounds.center;

            // Generate arc segments for all 4 walls
            // Front wall (Z-)
            GenerateWallArc(vertices, triangles, uvs, 
                new Vector3(center.x - width/2, baseHeight, bounds.min.z),
                new Vector3(center.x + width/2, baseHeight, bounds.min.z),
                Vector3.back, segments, tunnelHeight);

            // Back wall (Z+)
            GenerateWallArc(vertices, triangles, uvs,
                new Vector3(center.x + width/2, baseHeight, bounds.max.z),
                new Vector3(center.x - width/2, baseHeight, bounds.max.z),
                Vector3.forward, segments, tunnelHeight);

            // Left wall (X-)
            GenerateWallArc(vertices, triangles, uvs,
                new Vector3(bounds.min.x, baseHeight, center.z - depth/2),
                new Vector3(bounds.min.x, baseHeight, center.z + depth/2),
                Vector3.left, segments, tunnelHeight);

            // Right wall (X+)
            GenerateWallArc(vertices, triangles, uvs,
                new Vector3(bounds.max.x, baseHeight, center.z + depth/2),
                new Vector3(bounds.max.x, baseHeight, center.z - depth/2),
                Vector3.right, segments, tunnelHeight);

            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            mf.mesh = mesh;
            mc.sharedMesh = mesh;
            mr.material = CreateDefaultMaterial(new Color(0.4f, 0.35f, 0.3f));

            return wallObj;
        }

        static void GenerateWallArc(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs,
            Vector3 startPoint, Vector3 endPoint, Vector3 normal, int segments, float height)
        {
            int startIdx = vertices.Count;
            Vector3 right = (endPoint - startPoint).normalized;
            float wallLength = Vector3.Distance(startPoint, endPoint);

            // Generate vertices for the arc
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float angle = Mathf.PI * t; // 0 to PI (half circle)
                
                float arcHeight = Mathf.Sin(angle) * height;
                float arcDepth = (1f - Mathf.Cos(angle)) * (height * 0.3f); // Slight curve inward

                Vector3 basePos = Vector3.Lerp(startPoint, endPoint, t);
                Vector3 offset = normal * arcDepth;
                
                vertices.Add(basePos + Vector3.up * arcHeight + offset);
                uvs.Add(new Vector2(t, arcHeight / height));
            }

            // Generate triangles
            for (int i = 0; i < segments; i++)
            {
                int current = startIdx + i;
                int next = startIdx + i + 1;

                // Create quad strip
                triangles.Add(current);
                triangles.Add(next);
                triangles.Add(current);
            }
        }

        #endregion

        #region Connection Generation

        static void GenerateConnection(DungeonConnection connection, DungeonData data, Transform parent)
        {
            var nodeA = data.GetNode(connection.nodeAId);
            var nodeB = data.GetNode(connection.nodeBId);

            if (nodeA == null || nodeB == null)
            {
                Debug.LogWarning($"Connection {connection.id} has invalid node references");
                return;
            }

            GameObject corridorObj = new GameObject($"Corridor_{connection.id}");
            corridorObj.transform.SetParent(parent);

            Vector3 startPos = GridToWorldCenter(nodeA.gridPosition, nodeA.heightLevel, data);
            Vector3 endPos = GridToWorldCenter(nodeB.gridPosition, nodeB.heightLevel, data);

            switch (connection.transitionType)
            {
                case ConnectionType.Flat:
                    GenerateFlatCorridor(startPos, endPos, connection, corridorObj.transform, data);
                    break;
                case ConnectionType.Ramp:
                    GenerateRamp(startPos, endPos, connection, corridorObj.transform, data);
                    break;
                case ConnectionType.Stairs:
                    GenerateStairs(startPos, endPos, connection, corridorObj.transform, data);
                    break;
                case ConnectionType.Tunnel:
                    GenerateTunnel(startPos, endPos, connection, corridorObj.transform, data);
                    break;
            }
        }

        static void GenerateFlatCorridor(Vector3 start, Vector3 end, DungeonConnection connection, Transform parent, DungeonData data)
        {
            GameObject corridor = new GameObject("FlatCorridor");
            corridor.transform.SetParent(parent);
            corridor.tag = "DungeonFloor";

            // Simple cube corridor
            Vector3 center = (start + end) * 0.5f;
            float length = Vector3.Distance(start, end) + data.gridCellSize;
            float width = connection.GetWidthAt(0.5f);
            
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "CorridorFloor";
            floor.transform.SetParent(corridor.transform);
            floor.transform.position = center;
            floor.transform.LookAt(end);
            floor.transform.localScale = new Vector3(width, 0.5f, length);
            floor.GetComponent<Renderer>().material = CreateDefaultMaterial(new Color(0.55f, 0.55f, 0.55f));
            
            Debug.Log($"Generated flat corridor from {start} to {end}, length={length}, width={width}");
        }

        static void GenerateRamp(Vector3 start, Vector3 end, DungeonConnection connection, Transform parent, DungeonData data)
        {
            GameObject ramp = new GameObject("Ramp");
            ramp.transform.SetParent(parent);
            ramp.tag = "DungeonFloor";

            // Simple cube ramp
            Vector3 center = (start + end) * 0.5f;
            float length = Vector3.Distance(start, end) + data.gridCellSize;
            float width = connection.GetWidthAt(0.5f);
            
            GameObject rampFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rampFloor.name = "RampFloor";
            rampFloor.transform.SetParent(ramp.transform);
            rampFloor.transform.position = center;
            rampFloor.transform.LookAt(end);
            
            // Calculate pitch for ramp (useful for debug logging, but LookAt handles the actual rotation)
            float heightDiff = end.y - start.y;
            float horizontalDist = new Vector2(end.x - start.x, end.z - start.z).magnitude;
            float pitch = Mathf.Atan2(heightDiff, horizontalDist) * Mathf.Rad2Deg;
            
            // rampFloor.transform.Rotate(Vector3.right, pitch, Space.Self); // REMOVED: LookAt(end) already correctly pitches the Z-axis towards the target
            rampFloor.transform.localScale = new Vector3(width, 0.5f, length);
            rampFloor.GetComponent<Renderer>().material = CreateDefaultMaterial(new Color(0.5f, 0.6f, 0.5f));
            
            Debug.Log($"Generated ramp from {start} to {end}, pitch={pitch}, length={length}");
        }

        static void GenerateStairs(Vector3 start, Vector3 end, DungeonConnection connection, Transform parent, DungeonData data)
        {
            GameObject stairs = new GameObject("Stairs");
            stairs.transform.SetParent(parent);
            stairs.tag = "DungeonFloor";

            float heightDiff = end.y - start.y;
            int steps = connection.stepCount > 0 ? connection.stepCount : Mathf.CeilToInt(Mathf.Abs(heightDiff) / connection.stepHeight);

            Vector3 horizontalDir = new Vector3(end.x - start.x, 0, end.z - start.z);
            float horizontalDist = horizontalDir.magnitude;
            horizontalDir.Normalize();

            float stepDepth = connection.stepDepth;
            float stepHeight = heightDiff / steps;

            Vector3 perpendicular = Vector3.Cross(horizontalDir, Vector3.up).normalized;
            float width = connection.GetWidthAt(0.5f);

            Vector3 direction = (end - start).normalized;
            Vector3 startExtended = start - direction * (data.gridCellSize * 0.5f);
            Vector3 endExtended = end + direction * (data.gridCellSize * 0.5f);
            
            for (int i = 0; i < steps; i++)
            {
                float t = i / (float)steps;
                Vector3 stepStart = Vector3.Lerp(startExtended, endExtended, t);
                
                GameObject step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                step.name = $"Step_{i}";
                step.transform.SetParent(stairs.transform);
                
                step.transform.position = stepStart + horizontalDir * (stepDepth * 0.5f) + Vector3.up * (stepHeight * 0.5f);
                step.transform.localScale = new Vector3(width, stepHeight, stepDepth);
                step.transform.rotation = Quaternion.LookRotation(horizontalDir);
                
                step.tag = "DungeonFloor";
                step.GetComponent<Renderer>().material = CreateDefaultMaterial(new Color(0.45f, 0.45f, 0.5f));
            }
        }

        static void GenerateTunnel(Vector3 start, Vector3 end, DungeonConnection connection, Transform parent, DungeonData data)
        {
            // Similar to ramp but with walls and ceiling
            GameObject tunnel = new GameObject("Tunnel");
            tunnel.transform.SetParent(parent);

            // Floor
            GenerateRamp(start, end, connection, tunnel.transform, data);
            
            Vector3 center = (start + end) * 0.5f;
            float length = Vector3.Distance(start, end) + data.gridCellSize;
            float width = connection.GetWidthAt(0.5f);
            float wallHeight = connection.height;
            float wallThickness = 0.5f;

            // Determine rotation based on LookAt, just like the ramp floor
            Quaternion rotation = Quaternion.LookRotation(end - start);
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;

            // Left Wall
            GameObject leftWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftWall.name = "Wall_Left";
            leftWall.transform.SetParent(tunnel.transform);
            leftWall.transform.position = center - right * (width * 0.5f - wallThickness * 0.5f) + up * (wallHeight * 0.5f);
            leftWall.transform.rotation = rotation;
            leftWall.transform.localScale = new Vector3(wallThickness, wallHeight, length);
            leftWall.tag = "DungeonWall";
            leftWall.GetComponent<Renderer>().material = CreateDefaultMaterial(new Color(0.4f, 0.35f, 0.3f));

            // Right Wall
            GameObject rightWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightWall.name = "Wall_Right";
            rightWall.transform.SetParent(tunnel.transform);
            rightWall.transform.position = center + right * (width * 0.5f - wallThickness * 0.5f) + up * (wallHeight * 0.5f);
            rightWall.transform.rotation = rotation;
            rightWall.transform.localScale = new Vector3(wallThickness, wallHeight, length);
            rightWall.tag = "DungeonWall";
            rightWall.GetComponent<Renderer>().material = CreateDefaultMaterial(new Color(0.4f, 0.35f, 0.3f));

            // Ceiling
            GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "Ceiling";
            ceiling.transform.SetParent(tunnel.transform);
            ceiling.transform.position = center + up * wallHeight;
            ceiling.transform.rotation = rotation;
            ceiling.transform.localScale = new Vector3(width, wallThickness, length);
            ceiling.tag = "DungeonCeiling";
            ceiling.GetComponent<Renderer>().material = CreateDefaultMaterial(new Color(0.4f, 0.35f, 0.3f));
        }

        #endregion

        #region Mesh Creation Helpers

        static Mesh CreateCorridorMesh(Vector3 start, Vector3 end, List<WidthPoint> widthPoints, int segments, float gridCellSize = 5f)
        {
            Mesh mesh = new Mesh();
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>();

            Vector3 direction = (end - start);
            direction.y = 0; // Keep horizontal
            direction.Normalize();

            // Extend start and end to penetrate into grid corners/rooms
            float ext = gridCellSize * 0.5f;
            Vector3 extendedStart = start - direction * ext;
            Vector3 extendedEnd = end + direction * ext;

            float length = new Vector2(extendedEnd.x - extendedStart.x, extendedEnd.z - extendedStart.z).magnitude;
            Vector3 perpendicular = Vector3.Cross(direction, Vector3.up).normalized;

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 centerPos = Vector3.Lerp(extendedStart, extendedEnd, t);
                
                float width = GetInterpolatedWidth(widthPoints, t);

                vertices.Add(centerPos - perpendicular * (width / 2f));
                vertices.Add(centerPos + perpendicular * (width / 2f));
                
                uvs.Add(new Vector2(0, t));
                uvs.Add(new Vector2(1, t));
            }

            for (int i = 0; i < segments; i++)
            {
                int current = i * 2;
                int next = (i + 1) * 2;

                triangles.Add(current);
                triangles.Add(next);
                triangles.Add(current + 1);

                triangles.Add(current + 1);
                triangles.Add(next);
                triangles.Add(next + 1);
            }

            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        static Mesh CreateRampMesh(Vector3 start, Vector3 end, List<WidthPoint> widthPoints, float slope, int segments, float gridCellSize = 5f)
        {
            Mesh mesh = new Mesh();
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>();

            Vector3 direction = (end - start).normalized;
            Vector3 horizontalDir = new Vector3(direction.x, 0, direction.z).normalized;
            Vector3 perpendicular = Vector3.Cross(horizontalDir, Vector3.up).normalized;

            float ext = gridCellSize * 0.5f;
            Vector3 extendedStart = start - direction * ext;
            Vector3 extendedEnd = end + direction * ext;

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 centerPos = Vector3.Lerp(extendedStart, extendedEnd, t);
                
                float width = GetInterpolatedWidth(widthPoints, t);

                vertices.Add(centerPos - perpendicular * (width / 2f));
                vertices.Add(centerPos + perpendicular * (width / 2f));
                
                uvs.Add(new Vector2(0, t));
                uvs.Add(new Vector2(1, t));
            }

            for (int i = 0; i < segments; i++)
            {
                int current = i * 2;
                int next = (i + 1) * 2;

                triangles.Add(current);
                triangles.Add(next);
                triangles.Add(current + 1);

                triangles.Add(current + 1);
                triangles.Add(next);
                triangles.Add(next + 1);
            }

            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        static float GetInterpolatedWidth(List<WidthPoint> points, float t)
        {
            if (points == null || points.Count == 0)
                return 3f;

            points.Sort((a, b) => a.normalizedPosition.CompareTo(b.normalizedPosition));

            for (int i = 0; i < points.Count - 1; i++)
            {
                if (t >= points[i].normalizedPosition && t <= points[i + 1].normalizedPosition)
                {
                    float localT = (t - points[i].normalizedPosition) /
                                   (points[i + 1].normalizedPosition - points[i].normalizedPosition);
                    return Mathf.Lerp(points[i].width, points[i + 1].width, localT);
                }
            }

            return points[points.Count - 1].width;
        }

        #endregion

        #region Utility Methods

        static Vector3 GridToWorld(Vector2Int gridPos, int heightLevel, DungeonData data)
        {
            return new Vector3(
                gridPos.x * data.gridCellSize,
                heightLevel * data.heightPerLevel,
                gridPos.y * data.gridCellSize
            );
        }

        static Vector3 GridToWorldCenter(Vector2Int gridPos, int heightLevel, DungeonData data)
        {
            return new Vector3(
                (gridPos.x + 0.5f) * data.gridCellSize,
                heightLevel * data.heightPerLevel,
                (gridPos.y + 0.5f) * data.gridCellSize
            );
        }

        static float CalculateAutoSlope(Vector3 start, Vector3 end)
        {
            float heightDiff = end.y - start.y;
            float horizontalDist = new Vector2(end.x - start.x, end.z - start.z).magnitude;
            
            if (horizontalDist < 0.01f) return 0f;
            
            return Mathf.Atan2(heightDiff, horizontalDist) * Mathf.Rad2Deg;
        }

        static Material CreateDefaultMaterial(Color color)
        {
            return MaterialHelper.CreateMaterial(color);
        }

        #endregion
    }
}
