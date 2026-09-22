using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class TowerValidation
{
    public static void Play()
    {
        var args=System.Environment.GetCommandLineArgs();
        EditorSceneManager.OpenScene(args.Contains("-tower-lab")?"Assets/Scenes/LaboratoryFloor.unity":
            args.Contains("-tower-garden")?"Assets/Scenes/CrystalGarden.unity":"Assets/Scenes/RoboticForest.unity");
        EditorApplication.isPlaying=true;
    }
    public static void GenerateAndPlay() { TowerBuild.Generate();Play(); }
    public static void GenerateAndBuild() { TowerBuild.Generate();Build(); }
    public static void Build()
    {
        EditorBuildSettings.scenes=TowerBuild.ScenePaths.Select(p=>new EditorBuildSettingsScene(p,true)).ToArray();
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
            scenes=TowerBuild.ScenePaths,locationPathName="Builds/Tower/RobotTower.exe",
            target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new System.Exception("Tower build failed: "+report.summary.result);
        Debug.Log("TOWER WINDOWS BUILD COMPLETE");
    }
}
