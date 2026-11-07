using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Torbellino.Rendering;

public static class EngravingRisoComparison
{
    const string Output="Artifacts/EngravingCharacter";
    public static void Generate(){
        var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Rendering/Risograph/RisographFantasy.asset");
        profile.TryGet<RisographVolume>(out var s);
        var material=new Material(Shader.Find("Hidden/Torbellino/Risograph"));
        material.SetVector("_RisoTone",new Vector4(s.intensity.value,Mathf.Pow(2,s.exposure.value),s.contrast.value,s.saturation.value));
        material.SetVector("_RisoLightness",new Vector4(s.midtoneLift.value,s.paperHighlights.value,0,0));
        material.SetVector("_RisoScreen",new Vector4(s.dotSize.value,s.screenAngle.value*Mathf.Deg2Rad,s.halftone.value,(int)s.paletteSize.value));
        material.SetVector("_RisoTexture",new Vector4(s.paperGrain.value,s.inkWear.value,s.misregistration.value,0));
        material.SetVector("_ShadowInk",s.shadowInk.value);material.SetVector("_BlueInk",s.blueInk.value);material.SetVector("_PinkInk",s.pinkInk.value);material.SetVector("_Paper",s.paper.value);
        foreach(string file in Directory.GetFiles(Output,"*-original.png")){
            var image=new Texture2D(2,2,TextureFormat.RGBA32,false,false);image.LoadImage(File.ReadAllBytes(file));
            var target=RenderTexture.GetTemporary(image.width,image.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var previous=RenderTexture.active;var cmd=new CommandBuffer();
            var result=new Texture2D(image.width,image.height,TextureFormat.RGB24,false,false);
            try{
                var props=new MaterialPropertyBlock();props.SetTexture("_BlitTexture",image);props.SetVector("_BlitScaleBias",new Vector4(1,1,0,0));
                props.SetVector("_BlitTexture_TexelSize",new Vector4(1f/image.width,1f/image.height,image.width,image.height));
                cmd.SetGlobalVector("_ScreenParams",new Vector4(image.width,image.height,1+1f/image.width,1+1f/image.height));
                cmd.SetRenderTarget(target);cmd.DrawProcedural(Matrix4x4.identity,material,0,MeshTopology.Triangles,3,1,props);Graphics.ExecuteCommandBuffer(cmd);
                RenderTexture.active=target;result.ReadPixels(new Rect(0,0,image.width,image.height),0,0);result.Apply();File.WriteAllBytes(file.Replace("-original.png","-risograph.png"),result.EncodeToPNG());
            }finally{RenderTexture.active=previous;cmd.Release();RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(result);Object.DestroyImmediate(image);}
        }
        Object.DestroyImmediate(material);
    }
}
