using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class LabInspection
{
    public static void Run()
    {
        Directory.CreateDirectory("Artifacts");
        var text = new System.Text.StringBuilder();
        foreach (string path in new[] { "Assets/Prefabs/Player/-----Player.prefab", "Assets/Prefabs/Weapons/Metralleta/Survival Carbine/Carbine.prefab", "Assets/Prefabs/Weapons/PISTOLA_PLASMA/PISTOLA_PLASMA_FIX.prefab", "Assets/Prefabs/Shotgun Set/Prefabs/BenelliM4.prefab", "Assets/Prefabs/EnemyRobots/Prefabs/MachineGunRobot.prefab" })
        {
            text.AppendLine(path);
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                text.AppendLine($"{AnimationUtility.CalculateTransformPath(t, root.transform)} pos={t.localPosition} rot={t.localEulerAngles} scale={t.localScale} components={string.Join(",", t.GetComponents<Component>().Select(c => c == null ? "MISSING" : c.GetType().Name))}");
                if (t.TryGetComponent<Renderer>(out var r)) text.AppendLine($"   bounds={r.bounds}");
            }
            Object.DestroyImmediate(root);
        }
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) text.AppendLine($"SCENE ROOT {root.name} {root.transform.position}");
        File.WriteAllText("Artifacts/inspection.txt", text.ToString());
        Debug.Log("LAB INSPECTION COMPLETE");
    }
}