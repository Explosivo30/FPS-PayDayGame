using UnityEngine;

namespace DungeonPainter.Data
{
    /// <summary>
    /// Container for custom mesh prefabs to replace generated primitives
    /// Create this asset and assign your custom dungeon pieces
    /// </summary>
    [CreateAssetMenu(fileName = "NewMeshReplacementSet", menuName = "Dungeon Painter/Mesh Replacement Set")]
    public class MeshReplacementSet : ScriptableObject
    {
        [Header("Floor Meshes")]
        [Tooltip("Prefab to replace floor tiles")]
        public GameObject floorPrefab;
        
        [Tooltip("Prefab for flat corridor floors")]
        public GameObject corridorFloorPrefab;

        [Header("Wall Meshes")]
        [Tooltip("Prefab to replace tunnel walls")]
        public GameObject wallPrefab;
        
        [Tooltip("Prefab for curved tunnel sections")]
        public GameObject tunnelWallPrefab;

        [Header("Ceiling Meshes")]
        [Tooltip("Optional ceiling prefab")]
        public GameObject ceilingPrefab;

        [Header("Transition Meshes")]
        [Tooltip("Prefab for ramps")]
        public GameObject rampPrefab;
        
        [Tooltip("Prefab for stairs")]
        public GameObject stairsPrefab;
        
        [Tooltip("Prefab for enclosed tunnels")]
        public GameObject tunnelPrefab;

        [Header("Props")]
        [Tooltip("Optional corner pieces")]
        public GameObject cornerPrefab;
        
        [Tooltip("Optional pillar/column")]
        public GameObject pillarPrefab;

        public bool HasFloorReplacement => floorPrefab != null;
        public bool HasWallReplacement => wallPrefab != null;
        public bool HasRampReplacement => rampPrefab != null;
        public bool HasStairsReplacement => stairsPrefab != null;
    }
}
