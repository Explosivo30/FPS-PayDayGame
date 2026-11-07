using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Updates only gameplay additions; never regenerates the tower or its art.</summary>
public static class ReactiveGameplayBuild
{
    [MenuItem("Tools/HELIX/Gameplay/Install reactive arenas")]
    public static void Configure()
    {
        TowerBuild.InstallReactiveArena();
        AssetDatabase.SaveAssets();
        Debug.Log("REACTIVE GAMEPLAY CONFIGURATION COMPLETE");
    }

    public static void PlayValidation()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/LaboratoryFloor.unity");
        EditorApplication.isPlaying=true;
    }

    public static void ConfigureAndValidate(){Configure();PlayValidation();}

    public static void BuildAndValidateAI()
    {
        Configure();
        BuildWindows();
        EditorSceneManager.OpenScene("Assets/Scenes/RoboticForest.unity");
        EditorApplication.isPlaying=true;
    }

    [MenuItem("Tools/HELIX/Gameplay/Build Windows")]
    public static void BuildWindows()
    {
        var args=Environment.GetCommandLineArgs();
        int index=Array.IndexOf(args,"-gameplay-build-output");
        string output=index>=0&&index+1<args.Length?args[index+1]:"Builds/Gameplay-2026-10-21/RobotTower.exe";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
        var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
        var included=settings.FindProperty("m_AlwaysIncludedShaders");
        int originalCount=included.arraySize;
        var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/AnimeTonemapper/AnimeTonemapper.shader");
        bool present=Enumerable.Range(0,originalCount).Any(i=>included.GetArrayElementAtIndex(i).objectReferenceValue==shader);
        try
        {
            if(shader!=null&&!present)
            {
                included.arraySize++;
                included.GetArrayElementAtIndex(originalCount).objectReferenceValue=shader;
                settings.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssets();
            }
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=TowerBuild.ScenePaths,locationPathName=output,
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Gameplay build failed: "+report.summary.result);
            Debug.Log("REACTIVE GAMEPLAY BUILD SUCCEEDED: "+output+" | bytes="+report.summary.totalSize);
        }
        finally
        {
            if(shader!=null&&!present)
            {
                settings.Update();settings.FindProperty("m_AlwaysIncludedShaders").arraySize=originalCount;
                settings.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssets();
            }
        }
    }
}
