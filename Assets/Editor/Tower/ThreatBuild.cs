using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;
public static class ThreatBuild
{
    [MenuItem("Tools/HELIX/Configure enemy threat")]
    public static void Configure()
    {
        Directory.CreateDirectory("Assets/Art/AI");AssetDatabase.Refresh();
        const string path="Assets/Art/AI/ThreatProfile.asset";
        var profile=AssetDatabase.LoadAssetAtPath<ThreatProfile>(path);
        if(profile==null){profile=ScriptableObject.CreateInstance<ThreatProfile>();AssetDatabase.CreateAsset(profile,path);}
        var setup=EditorSceneManager.GetSceneManagerSetup();
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/TowerCore.unity");
        var session=Object.FindFirstObjectByType<TowerSession>();
        var threat=session.GetComponent<RunThreat>();if(threat==null)threat=session.gameObject.AddComponent<RunThreat>();
        threat.profile=profile;EditorUtility.SetDirty(threat);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        if(setup.Length>0&&!string.IsNullOrEmpty(setup[0].path))EditorSceneManager.RestoreSceneManagerSetup(setup);
        Debug.Log("THREAT CONFIGURED");
    }
    public static void Play(){Configure();TowerValidation.Play();}
    public static void Build(){Configure();TowerValidation.Build();}
}
