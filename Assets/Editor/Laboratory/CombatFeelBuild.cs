using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class CombatFeelBuild
{
    [MenuItem("Tools/HELIX/Update combat feedback")]
    public static void Configure()
    {
        const string directory="Assets/Art/CombatFeel";
        Directory.CreateDirectory(directory);Directory.CreateDirectory("Artifacts/CombatFeel");AssetDatabase.Refresh();
        string path=directory+"/Impact particles.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null) { material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));AssetDatabase.CreateAsset(material,path); }
        material.SetColor("_BaseColor",Color.white);material.SetFloat("_Surface",1);material.SetFloat("_Blend",2);
        material.SetFloat("_SrcBlend",(int)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(int)BlendMode.One);
        material.SetFloat("_ZWrite",0);material.SetFloat("_Cull",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3000;
        EditorUtility.SetDirty(material);
        string robotPath="Assets/Art/Laboratory/LaboratorySentinel.prefab";
        var robot=PrefabUtility.LoadPrefabContents(robotPath);
        robot.GetComponent<RobotPresentation>().hitEffectMaterial=material;
        PrefabUtility.SaveAsPrefabAsset(robot,robotPath);PrefabUtility.UnloadPrefabContents(robot);
        foreach(string effect in new[]{"Muzzle","Impact"})
        {
            string effectPath="Assets/Art/Laboratory/"+effect+".prefab";
            var root=PrefabUtility.LoadPrefabContents(effectPath);
            foreach(var renderer in root.GetComponentsInChildren<ParticleSystemRenderer>())renderer.sharedMaterial=material;
            PrefabUtility.SaveAsPrefabAsset(root,effectPath);PrefabUtility.UnloadPrefabContents(root);
        }
        const string playerPath="Assets/Prefabs/Player/-----Player.prefab";
        var player=PrefabUtility.LoadPrefabContents(playerPath);
        var controller=player.GetComponentInChildren<GunController>(true);
        if(controller.GetComponent<ImpactFeedbackPlayer>()==null)controller.gameObject.AddComponent<ImpactFeedbackPlayer>();
        PrefabUtility.SaveAsPrefabAsset(player,playerPath);PrefabUtility.UnloadPrefabContents(player);
        AssetDatabase.SaveAssets();Debug.Log("COMBAT FEEL ASSETS READY");
    }
    public static void Play()
    {
        Configure();
        EditorSceneManager.OpenScene("Assets/Scenes/RoboticForest.unity");
        EditorApplication.isPlaying=true;
    }
    public static void Build() { Configure();TowerValidation.Build(); }
}
