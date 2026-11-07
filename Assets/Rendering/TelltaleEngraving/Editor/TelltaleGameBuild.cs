using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Torbellino.Rendering;

public sealed class TelltaleGameBuild : IProcessSceneWithReport
{
    const string Root="Assets/Rendering/TelltaleEngraving/GameBuild";
    static bool building;
    public int callbackOrder=>10000;
    public void OnProcessScene(Scene scene,BuildReport report)
    {
        if(!building)return;
        // Keep mesh-local coordinates for the geometry hull and engraving pattern.
        foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject,GameObjectUtility.GetStaticEditorFlags(t.gameObject)&~StaticEditorFlags.BatchingStatic);
        var go=new GameObject("Telltale game style (build only)");SceneManager.MoveGameObjectToScene(go,scene);
        var style=go.AddComponent<TelltaleGameStyle>();style.surface=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Surface.mat");style.contour=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Contour.mat");style.pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Root+"/Pipeline.asset");
    }
    public static void Build()
    {
        string oldDefaultPath=AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline),oldQualityPath=AssetDatabase.GetAssetPath(QualitySettings.renderPipeline);
        Directory.CreateDirectory(Root);Directory.CreateDirectory("Artifacts/TelltaleGameBuild");
        var renderer=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(renderer,Root+"/Renderer.asset");
        var pipeline=UniversalRenderPipelineAsset.Create(renderer);pipeline.supportsHDR=true;pipeline.msaaSampleCount=2;pipeline.supportsCameraDepthTexture=true;pipeline.supportsCameraOpaqueTexture=true;pipeline.shadowDistance=60;pipeline.mainLightShadowmapResolution=2048;
        AssetDatabase.CreateAsset(pipeline,Root+"/Pipeline.asset");
        var surface=TelltaleMaterialTools.Convert(null,TelltaleMaterialTools.Category.Cloth);
        surface.SetFloat("_Projection",1);surface.SetFloat("_HatchWorldUnits",1);surface.SetFloat("_HatchScale",2);
        surface.SetFloat("_HatchIntensity",.85f);surface.SetFloat("_HatchThickness",.23f);surface.SetFloat("_HatchWaviness",.22f);surface.SetFloat("_HatchStart",.8f);
        surface.SetFloat("_HatchContrast",.65f);surface.SetFloat("_CrossStrength",.45f);
        surface.SetFloat("_AmbientStrength",.65f);AssetDatabase.CreateAsset(surface,Root+"/Surface.mat");
        var outline=new Material(AssetDatabase.LoadAssetAtPath<Material>(TelltaleMaterialTools.Root+"/Materials/Contour.mat"));outline.SetFloat("_OutlineWidth",.8f);AssetDatabase.CreateAsset(outline,Root+"/Contour.mat");AssetDatabase.SaveAssets();
        var graphics=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);var inc=graphics.FindProperty("m_AlwaysIncludedShaders");int count=inc.arraySize;
        try
        {
            foreach(var shader in new[]{surface.shader,outline.shader}){inc.arraySize++;inc.GetArrayElementAtIndex(inc.arraySize-1).objectReferenceValue=shader;}
            graphics.ApplyModifiedPropertiesWithoutUndo();GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;AssetDatabase.SaveAssets();building=true;
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),locationPathName="Builds/TelltaleEngraving/HELIX-Telltale.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            File.WriteAllText("Artifacts/TelltaleGameBuild/build-report.txt",result.summary.result+"\nBytes="+result.summary.totalSize+"\nErrors="+result.summary.totalErrors+"\nDuration="+result.summary.totalTime);
            if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Telltale build failed: "+result.summary.result);
            Debug.Log("TELLTALE GAME BUILD SUCCEEDED");
        }
        finally{building=false;GraphicsSettings.defaultRenderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(oldDefaultPath);QualitySettings.renderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(oldQualityPath);graphics.Update();graphics.FindProperty("m_AlwaysIncludedShaders").arraySize=count;graphics.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssets();}
    }
}
