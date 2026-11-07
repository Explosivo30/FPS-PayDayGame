using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Torbellino.Rendering;

// Isolated art study. Never replaces project materials, scenes or the active pipeline.
public static class PrintStyleComparison
{
    const string Art = "Assets/Art/PrintStyles";
    const string Output = "Artifacts/PrintStyles";
    [MenuItem("Tools/HELIX/Print styles/Generate comparison")]
    public static void Generate()
    {
        Directory.CreateDirectory(Output);
        var wood = Material("Woodcut", "HELIX/Print/Woodcut Engraving");
        var riso = new Material(Shader.Find("Hidden/Torbellino/Risograph"));
        ConfigureRiso(riso);
        Renderer("Woodcut", wood);
        var sources = new[] {
            "Artifacts/Gameplay-2026-10-21/Windows/RoboticForest-reactive.png",
            "Artifacts/Gameplay-2026-10-21/Windows/CrystalGarden-reactive.png",
            "Artifacts/EnemyPolish/EditorFeel/pistol-impact.png",
            "Artifacts/FoundryArena/overview.png"
        };
        foreach (var source in sources)
        {
            if (!File.Exists(source)) throw new FileNotFoundException("Missing comparison source",source);
            var image = new Texture2D(2,2,TextureFormat.RGBA32,false,false);
            image.LoadImage(File.ReadAllBytes(source));
            string key = Path.GetFileNameWithoutExtension(source);
            File.Copy(source, Output+"/"+key+"-original.png",true);
            Render(image,riso,Output+"/"+key+"-risograph.png");
            Render(image,wood,Output+"/"+key+"-woodcut.png");
            UnityEngine.Object.DestroyImmediate(image);
        }
        AssetDatabase.SaveAssets();
        var shaders = new[]{wood.shader,riso.shader};
        var errors = shaders.SelectMany(s => ShaderUtil.GetShaderMessages(s)
            .Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
            .Select(m => s.name+": "+m.message)).ToArray();
        File.WriteAllText(Output+"/validation.txt", "Unity "+Application.unityVersion+
            "\nGPU: "+SystemInfo.graphicsDeviceName+"\nFour identical source frames processed with both real shader passes.\n"+
            "Sources are archived gameplay captures, not a new gameplay run.\n"+
            "Shader compilation errors: "+errors.Length+"\n"+string.Join("\n",errors));
        if(errors.Length>0) throw new Exception(string.Join("\n",errors));
        Debug.Log("PRINT STYLE COMPARISON COMPLETE: "+Path.GetFullPath(Output));
    }
    static void ConfigureRiso(Material material)
    {
        var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Rendering/Risograph/RisographFantasy.asset");
        if(!profile.TryGet<RisographVolume>(out var s)) throw new Exception("No Risograph Fantasy preset");
        material.SetVector("_RisoTone",new Vector4(s.intensity.value,Mathf.Pow(2,s.exposure.value),s.contrast.value,s.saturation.value));
        material.SetVector("_RisoLightness",new Vector4(s.midtoneLift.value,s.paperHighlights.value,0,0));
        material.SetVector("_RisoScreen",new Vector4(s.dotSize.value,s.screenAngle.value*Mathf.Deg2Rad,s.halftone.value,(int)s.paletteSize.value));
        material.SetVector("_RisoTexture",new Vector4(s.paperGrain.value,s.inkWear.value,s.misregistration.value,0));
        material.SetVector("_ShadowInk",s.shadowInk.value); material.SetVector("_BlueInk",s.blueInk.value);
        material.SetVector("_PinkInk",s.pinkInk.value); material.SetVector("_Paper",s.paper.value);
        File.WriteAllText(Output+"/risograph-settings.json",JsonUtility.ToJson(s,true));
    }
    static Material Material(string name,string shaderName)
    {
        var shader=Shader.Find(shaderName);
        if(!shader || !shader.isSupported) throw new Exception("Shader unavailable: "+shaderName);
        string path=Art+"/"+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material) { material=new Material(shader); AssetDatabase.CreateAsset(material,path); }
        return material;
    }
    static void Renderer(string name,Material material)
    {
        string path=Art+"/"+name+" Renderer.asset";
        if(AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path)) return;
        var renderer=ScriptableObject.CreateInstance<UniversalRendererData>();
        renderer.name=name+" Renderer";
        AssetDatabase.CreateAsset(renderer,path);
        var feature=ScriptableObject.CreateInstance<WoodcutRendererFeature>();
        feature.name=name+" / after post processing";
        feature.material=material;
        AssetDatabase.AddObjectToAsset(feature,renderer);
        renderer.rendererFeatures.Add(feature);
        var so=new SerializedObject(renderer);
        var map=so.FindProperty("m_RendererFeatureMap"); map.arraySize=1;
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature,out string _,out long id);
        map.GetArrayElementAtIndex(0).longValue=id; so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(renderer); EditorUtility.SetDirty(feature);
    }
    static void Render(Texture2D source,Material material,string path)
    {
        var previous=RenderTexture.active;
        var target=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var cmd=new CommandBuffer { name="Print style comparison" };
        var result=new Texture2D(source.width,source.height,TextureFormat.RGB24,false,false);
        // Use precisely the procedural full-screen pass used by URP; no Python approximation.
        var props=new MaterialPropertyBlock();
        props.SetTexture("_BlitTexture",source);
        props.SetVector("_BlitTexture_TexelSize",new Vector4(1f/source.width,1f/source.height,source.width,source.height));
        props.SetVector("_BlitScaleBias",new Vector4(1,1,0,0));
        try {
            cmd.SetGlobalVector("_ScreenParams",new Vector4(source.width,source.height,1f+1f/source.width,1f+1f/source.height));
            cmd.SetRenderTarget(target); cmd.ClearRenderTarget(false,true,Color.magenta);
            cmd.DrawProcedural(Matrix4x4.identity,material,0,MeshTopology.Triangles,3,1,props);
            Graphics.ExecuteCommandBuffer(cmd);
            RenderTexture.active=target;
            result.ReadPixels(new Rect(0,0,source.width,source.height),0,0); result.Apply();
            File.WriteAllBytes(path,result.EncodeToPNG());
        } finally {
            RenderTexture.active=previous; cmd.Release(); RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(result);
        }
    }
}
