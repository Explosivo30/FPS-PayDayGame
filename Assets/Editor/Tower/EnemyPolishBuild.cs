using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class EnemyPolishBuild
{
    [MenuItem("Tools/HELIX/Update enemy presentation")]
    public static void Configure()
    {
        const string path = "Assets/Art/Laboratory/LaboratorySentinel.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var presentation = root.GetComponent<RobotPresentation>();
            presentation.ConfigureRangedRig();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Directory.CreateDirectory("Artifacts/EnemyPolish");
            File.WriteAllLines("Artifacts/EnemyPolish/rig.txt", root.GetComponentsInChildren<MeshRenderer>().Select(r =>
                r.name + " | center=" + r.bounds.center + " | size=" + r.bounds.size));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }
    public static void Play() { Configure(); TowerValidation.Play(); }
    public static void Build() { Configure(); ShareBuild.BuildWindows(); }
}