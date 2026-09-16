using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LabValidation
{
    public static void Play()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        EditorApplication.isPlaying=true;
    }
    public static void Build()
    {
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes=new[]{"Assets/Scenes/SampleScene.unity","Assets/Scenes/Level1.unity"},
            locationPathName="Builds/HELIX/HELIX.exe",
            target=BuildTarget.StandaloneWindows64,
            options=BuildOptions.Development
        });
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new System.Exception("Windows build failed: "+report.summary.result);
        Debug.Log("HELIX WINDOWS BUILD COMPLETE");
    }
}
