using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Torbellino.Rendering;
using Object=UnityEngine.Object;

public static class TelltaleBuild
{
    const string Art="Assets/Rendering/TelltaleEngraving";
    static string Output;
    static int Stage;
    [MenuItem("Tools/Rendering/Telltale Engraving/Generate final test scene")]
    public static void Final(){Generate(8);}
    public static void Lighting(){Generate(2);}
    public static void Hatch(){Generate(3);}
    public static void Cross(){Generate(4);}
    public static void Outline(){Generate(5);}
    public static void Ink(){Generate(6);}
    static void Generate(int stage){
        Stage=stage;
        var textureImporter=AssetImporter.GetAtPath(Art+"/Textures/HatchAtlas.png") as TextureImporter;
        if(textureImporter){textureImporter.sRGBTexture=false;textureImporter.wrapMode=TextureWrapMode.Repeat;textureImporter.mipmapEnabled=true;textureImporter.filterMode=FilterMode.Trilinear;textureImporter.textureCompression=TextureImporterCompression.Uncompressed;textureImporter.SaveAndReimport();}
        Output="Artifacts/TelltaleEngraving/"+stage.ToString("D2");
        Directory.CreateDirectory(Output);Directory.CreateDirectory(Art+"/Materials");
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new Exception("Save the active scene first.");
        var oldSetup=EditorSceneManager.GetSceneManagerSetup();
        var oldPipeline=GraphicsSettings.defaultRenderPipeline;var oldQuality=QualitySettings.renderPipeline;
        var report=new StringBuilder();
        try{
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var renderer=ScriptableObject.CreateInstance<UniversalRendererData>();
            var pipeline=UniversalRenderPipelineAsset.Create(renderer);
            pipeline.msaaSampleCount=4;pipeline.supportsHDR=false;
            GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
            AssetDatabase.CreateAsset(renderer,Art+"/Character Renderer.asset");
            AssetDatabase.CreateAsset(pipeline,Art+"/Character Pipeline.asset");
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.5f,.5f,.5f);var probe=new SphericalHarmonicsL2();probe.AddAmbientLight(new Color(.18f,.18f,.18f));RenderSettings.ambientProbe=probe;RenderSettings.fog=false;
            var sun=new GameObject("Directional key").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.25f;sun.transform.rotation=Quaternion.Euler(32,135,0);sun.shadows=LightShadows.Soft;
            var camera=new GameObject("Character camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.80f,.75f,.65f);camera.fieldOfView=36;camera.nearClipPlane=.05f;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var original=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Veterans/Ariete.fbx"));original.name="Ariete original (same imported mesh)";
            foreach(var r in original.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(m=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Veterans/Materials/"+m.name.Split('.')[0]+".mat")).ToArray();
            var bounds=original.GetComponentsInChildren<Renderer>().Aggregate(new Bounds(original.transform.position,Vector3.zero),(b,r)=>{b.Encapsulate(r.bounds);return b;});
            report.AppendLine("Source: Assets/Art/Veterans/Ariete.fbx; bounds="+bounds);
            var engraved=Object.Instantiate(original);engraved.name="Ariete engraved (same geometry and source colors)";
            var surface=Shader.Find("Torbellino/Telltale Engraving/Character");var outlineShader=Shader.Find("Torbellino/Telltale Engraving/Outline");
            Material outline=null; if(Stage>=5){outline=new Material(outlineShader);AssetDatabase.CreateAsset(outline,Art+"/Materials/Contour.mat");}
            int materialCount=0;var materialCache=new Dictionary<string,Material>();
            foreach(var filter in engraved.GetComponentsInChildren<MeshFilter>()){
                var r=filter.GetComponent<MeshRenderer>();if(!r)continue;
                var mesh=filter.sharedMesh;var center=mesh.bounds.center;var size=mesh.bounds.size;
                // Long axis is a per-part flow guide, not a camera-facing direction.
                Vector3 axis=size.x>size.y&&size.x>size.z?Vector3.right:(size.y>size.z?Vector3.up:Vector3.forward);
                float radius=axis.x>.5?(size.y+size.z)*.25f:(axis.y>.5?(size.x+size.z)*.25f:(size.x+size.y)*.25f);
                var mats=new List<Material>();
                foreach(var source in r.sharedMaterials){
                    float cacheScale=(Mathf.Abs(filter.transform.lossyScale.x)+Mathf.Abs(filter.transform.lossyScale.y)+Mathf.Abs(filter.transform.lossyScale.z))/3;
                    string cacheKey=source.GetInstanceID()+"/"+cacheScale;
                    if(Stage>=7&&materialCache.TryGetValue(cacheKey,out var cached)){mats.Add(cached);continue;}
                    var m=Stage>=7?TelltaleMaterialTools.Convert(source,TelltaleMaterialTools.Category.Armor):new Material(surface);m.name=source.name+" Telltale "+materialCount;
                    m.SetColor("_BaseColor",source.GetColor("_BaseColor"));m.SetColor("_EmissionColor",source.IsKeywordEnabled("_EMISSION")?source.GetColor("_EmissionColor"):Color.black);
                    if(source.HasProperty("_BaseMap")&&source.GetTexture("_BaseMap"))m.SetTexture("_BaseMap",source.GetTexture("_BaseMap"));
                    m.SetFloat("_UseEmission",source.IsKeywordEnabled("_EMISSION")?1:0);
                    foreach(string property in new[]{"_BumpMap","_OcclusionMap","_EmissionMap"})if(source.HasProperty(property)&&source.GetTexture(property))m.SetTexture(property,source.GetTexture(property));
                    m.SetFloat("_UseNormalMap",source.IsKeywordEnabled("_NORMALMAP")?1:0);
                    if(source.HasProperty("_Metallic"))m.SetFloat("_Metallic",source.GetFloat("_Metallic"));
                    if(source.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",source.GetFloat("_Smoothness"));
                    float unitScale=(Mathf.Abs(filter.transform.lossyScale.x)+Mathf.Abs(filter.transform.lossyScale.y)+Mathf.Abs(filter.transform.lossyScale.z))/3;
                    m.SetFloat("_Projection",1);m.SetFloat("_HatchScale",8*unitScale);
                    m.SetFloat("_HatchEnable",Stage>=3?1:0);m.SetFloat("_CrossStrength",Stage>=7?.15f:(Stage>=4?.25f:0));
                    m.SetFloat("_GrainIntensity",Stage>=7?.006f:(Stage>=6?.008f:0));
                    var hatch=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Textures/HatchAtlas.png");if(hatch)m.SetTexture("_HatchTex",hatch);
                    AssetDatabase.CreateAsset(m,Art+"/Materials/Part-"+(materialCount++)+".mat");mats.Add(m);if(Stage>=7)materialCache[cacheKey]=m;
                }
                r.sharedMaterials=mats.ToArray();
                if(Stage>=5){var hull=new GameObject("Ink contour");hull.transform.SetParent(filter.transform,false);
                hull.AddComponent<MeshFilter>().sharedMesh=mesh;
                var hullRenderer=hull.AddComponent<MeshRenderer>();hullRenderer.sharedMaterials=Enumerable.Repeat(outline,mesh.subMeshCount).ToArray();hullRenderer.shadowCastingMode=ShadowCastingMode.Off;}
            }
            var viewer=new GameObject("Character comparison controls").AddComponent<TelltaleViewer>();viewer.previewPipeline=pipeline;viewer.original=original;viewer.engraved=engraved;viewer.view=camera;viewer.keyLight=sun;viewer.target=bounds.center;viewer.distance=bounds.size.y*2.1f;viewer.yaw=155;viewer.pitch=6;viewer.Apply();viewer.PositionCamera();
            if(Stage>=7)TelltaleMaterialTools.CreatePresets();
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Art+"/TelltaleComparison.unity");
            // Warm both material variants after creating the pipeline, before saving comparison frames.
            CapturePair(viewer,"warmup",128,128);
            // Identical camera/light/geometry per pair. No fullscreen shader, texture overlay or AI.
            foreach(float yaw in new[]{155f,180f,210f}){
                viewer.yaw=yaw;viewer.PositionCamera();CapturePair(viewer,"view-"+yaw,1200,1200);
            }
            viewer.target=bounds.center+Vector3.up*bounds.size.y*.22f;viewer.distance=bounds.size.y*.95f;viewer.yaw=163;viewer.PositionCamera();CapturePair(viewer,"detail",1400,1400);
            viewer.target=bounds.center;viewer.distance=bounds.size.y*2.1f;
            for(int i=0;i<(Stage==8?24:0);i++){viewer.yaw=140+i*3.5f;viewer.PositionCamera();CapturePair(viewer,"orbit-"+i.ToString("D2"),640,640);}
            viewer.yaw=155;viewer.PositionCamera();
            for(int i=0;i<4;i++){sun.transform.rotation=Quaternion.Euler(32,65+i*45,0);CapturePair(viewer,"light-"+i,800,800);}
            
            report.AppendLine("Character source renderers: "+original.GetComponentsInChildren<Renderer>().Length+"; styled renderers including outlines: "+engraved.GetComponentsInChildren<Renderer>().Length+"; unique surface materials: "+materialCount);
            if(Stage==8)TelltaleFixtures.Run(Output,renderer);
            var errors=new[]{surface,outlineShader}.Where(s=>s!=null).SelectMany(s=>ShaderUtil.GetShaderMessages(s).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).Select(m=>m.message)).ToArray();
            report.AppendLine("Unity "+Application.unityVersion+" / "+SystemInfo.graphicsDeviceName);
            report.AppendLine("Telltale Engraving stage "+Stage+". Source colors and maps copied into independent materials.");
            report.AppendLine("Ariete rigid articulated source. Object-triplanar test mapping. Organic facial readability requires a separate human character test.");
            report.AppendLine("Shader compilation errors: "+errors.Length);report.AppendLine(string.Join("\n",errors));
            File.WriteAllText(Output+"/validation.txt",report.ToString());
            if(errors.Length>0)throw new Exception(string.Join("\n",errors));
            Debug.Log("TELLTALE STAGE COMPLETE");
        }finally{GraphicsSettings.defaultRenderPipeline=oldPipeline;QualitySettings.renderPipeline=oldQuality;if(oldSetup.Any(s=>s.isLoaded&&s.isActive))EditorSceneManager.RestoreSceneManagerSetup(oldSetup);}
    }
    static void CapturePair(TelltaleViewer v,string key,int width,int height){
        v.useEngraving=false;v.Apply();Capture(v.view,Output+"/"+key+"-original.png",width,height);
        v.useEngraving=true;v.Apply();Capture(v.view,Output+"/"+key+"-engraving.png",width,height);
    }
    static void Capture(Camera camera,string path,int width,int height){
        var target=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var previous=RenderTexture.active;var image=new Texture2D(width,height,TextureFormat.RGB24,false);
        try{RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});RenderTexture.active=target;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(image);}
    }
}
