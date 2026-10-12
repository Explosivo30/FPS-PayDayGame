using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Builds a Windows package for external playtesting without regenerating scenes.</summary>
public static class ShareBuild
{
    public static void BuildWindows()
    {
        var args = Environment.GetCommandLineArgs();
        var index = Array.IndexOf(args, "-share-output");
        var output = index >= 0 && index + 1 < args.Length
            ? args[index + 1]
            : "Builds/Share/RobotTower.exe";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
        // Shader.Find alone is not a build dependency. Include the color effect for
        // this build, then restore the project's existing graphics configuration.
        var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
        var included = graphics.FindProperty("m_AlwaysIncludedShaders");
        var previousCount = included.arraySize;
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/AnimeTonemapper/AnimeTonemapper.shader");
        if (shader == null) throw new Exception("The anime tonemapper shader is missing.");
        var alreadyIncluded = false;
        for (var i = 0; i < previousCount; i++)
            alreadyIncluded |= included.GetArrayElementAtIndex(i).objectReferenceValue == shader;
        try
        {
            if (!alreadyIncluded)
            {
                included.arraySize++;
                included.GetArrayElementAtIndex(previousCount).objectReferenceValue = shader;
                graphics.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            }
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = TowerBuild.ScenePaths,
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Windows share build failed: " + report.summary.result);
            Debug.Log("SHARE BUILD SUCCEEDED: " + output + " | bytes=" + report.summary.totalSize);
        }
        finally
        {
            if (!alreadyIncluded)
            {
                graphics.Update();
                graphics.FindProperty("m_AlwaysIncludedShaders").arraySize = previousCount;
                graphics.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            }
        }
    }
}
