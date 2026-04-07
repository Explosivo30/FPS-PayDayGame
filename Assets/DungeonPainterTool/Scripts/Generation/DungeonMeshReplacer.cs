using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using DungeonPainter.Data;

namespace DungeonPainter.Generation
{
    /// <summary>
    /// Replaces generated primitive meshes with custom prefabs
    /// Allows you to swap out default cubes with your own dungeon pieces
    /// </summary>
    public static class DungeonMeshReplacer
    {
        public static void ReplaceMeshes(DungeonData data, GameObject dungeonRoot)
        {
            if (data == null || data.meshSet == null)
            {
                Debug.LogError("DungeonData or MeshReplacementSet is null!");
                return;
            }

            if (dungeonRoot == null)
            {
                Debug.LogError("Dungeon root GameObject is null!");
                return;
            }

            int replacedCount = 0;

            // Find all tagged objects
            Transform[] allTransforms = dungeonRoot.GetComponentsInChildren<Transform>(true);

            foreach (Transform t in allTransforms)
            {
                GameObject obj = t.gameObject;

                switch (obj.tag)
                {
                    case "DungeonFloor":
                        if (data.meshSet.floorPrefab != null)
                        {
                            ReplaceWithPrefab(obj, data.meshSet.floorPrefab);
                            replacedCount++;
                        }
                        break;

                    case "DungeonWall":
                        if (data.meshSet.wallPrefab != null)
                        {
                            ReplaceWithPrefab(obj, data.meshSet.wallPrefab);
                            replacedCount++;
                        }
                        break;

                    case "DungeonCeiling":
                        if (data.meshSet.ceilingPrefab != null)
                        {
                            ReplaceWithPrefab(obj, data.meshSet.ceilingPrefab);
                            replacedCount++;
                        }
                        break;
                }

                // Check by name for specific types
                if (obj.name.Contains("Ramp") && data.meshSet.rampPrefab != null)
                {
                    ReplaceWithPrefab(obj, data.meshSet.rampPrefab);
                    replacedCount++;
                }
                else if (obj.name.Contains("Stairs") && data.meshSet.stairsPrefab != null)
                {
                    ReplaceWithPrefab(obj, data.meshSet.stairsPrefab);
                    replacedCount++;
                }
            }

            Debug.Log($"Mesh replacement complete. Replaced {replacedCount} objects.");
        }

        static void ReplaceWithPrefab(GameObject original, GameObject prefab)
        {
            if (prefab == null || original == null)
                return;

            // Store original transform data
            Vector3 position = original.transform.position;
            Quaternion rotation = original.transform.rotation;
            Vector3 scale = original.transform.localScale;
            Transform parent = original.transform.parent;
            string originalName = original.name;

#if UNITY_EDITOR
            // Instantiate prefab properly in editor
            GameObject replacement = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
#else
            GameObject replacement = GameObject.Instantiate(prefab);
#endif

            // Apply transform
            replacement.transform.position = position;
            replacement.transform.rotation = rotation;
            replacement.transform.localScale = scale;
            replacement.transform.SetParent(parent);

            // Preserve naming for organization
            replacement.name = originalName + "_Custom";

            // Try to match bounds if possible
            TryMatchBounds(original, replacement);

            // Deactivate original (keep it for reference/undo)
            original.SetActive(false);
            original.name = originalName + "_OLD";
        }

        static void TryMatchBounds(GameObject original, GameObject replacement)
        {
            MeshFilter origMF = original.GetComponent<MeshFilter>();
            MeshFilter replMF = replacement.GetComponent<MeshFilter>();

            if (origMF != null && replMF != null && origMF.sharedMesh != null && replMF.sharedMesh != null)
            {
                Bounds origBounds = origMF.sharedMesh.bounds;
                Bounds replBounds = replMF.sharedMesh.bounds;

                // Scale replacement to match original bounds
                Vector3 scaleAdjustment = new Vector3(
                    origBounds.size.x / replBounds.size.x,
                    origBounds.size.y / replBounds.size.y,
                    origBounds.size.z / replBounds.size.z
                );

                replacement.transform.localScale = Vector3.Scale(replacement.transform.localScale, scaleAdjustment);
            }
        }

        /// <summary>
        /// Restore original meshes by re-enabling them and removing replacements
        /// </summary>
        public static void RestoreOriginalMeshes(GameObject dungeonRoot)
        {
            if (dungeonRoot == null)
            {
                Debug.LogError("Dungeon root is null!");
                return;
            }

            int restoredCount = 0;
            Transform[] allTransforms = dungeonRoot.GetComponentsInChildren<Transform>(true);

            List<GameObject> toDestroy = new List<GameObject>();

            foreach (Transform t in allTransforms)
            {
                GameObject obj = t.gameObject;

                // Re-enable originals
                if (obj.name.EndsWith("_OLD"))
                {
                    obj.SetActive(true);
                    obj.name = obj.name.Replace("_OLD", "");
                    restoredCount++;
                }

                // Mark custom replacements for deletion
                if (obj.name.EndsWith("_Custom"))
                {
                    toDestroy.Add(obj);
                }
            }

            // Delete replacements
            foreach (GameObject obj in toDestroy)
            {
                GameObject.DestroyImmediate(obj);
            }

            Debug.Log($"Restored {restoredCount} original meshes.");
        }
    }
}
