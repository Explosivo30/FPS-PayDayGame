using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Torbellino.Rendering;
using Object=UnityEngine.Object;

public static class RisographGameComparison{
 const string Art="Assets/Rendering/RisographGame";
 const string Output="Artifacts/RisographGame";
 [MenuItem("Tools/Rendering/Risograph Game/Generate game comparison")]
 public static void Generate(){
  Directory.CreateDirectory(Output);Directory.CreateDirectory(Art+"/Materials");
  if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new Exception("Save the active scene first.");
  var setup=EditorSceneManager.GetSceneManagerSetup();var oldPipeline=GraphicsSettings.defaultRenderPipeline;var oldQuality=QualitySettings.renderPipeline;
  Material legacy=null;
  try{
   var shader=Shader.Find("Hidden/Torbellino/RisographGame");if(!shader)throw new Exception("Missing game shader");
   var game=new Material(shader);AssetDatabase.CreateAsset(game,Art+"/Materials/HELIX Game.mat");
   EditorSceneManager.OpenScene("Assets/Rendering/TelltaleEngraving/TelltaleComparison.unity");
   legacy=Legacy();
   var previous=Object.FindFirstObjectByType<TelltaleViewer>();var original=previous.original;var telltale=previous.engraved;var camera=previous.view;var sun=previous.keyLight;var target=previous.target;float distance=previous.distance;Object.DestroyImmediate(previous);
   var plainData=ScriptableObject.CreateInstance<UniversalRendererData>();var plain=UniversalRenderPipelineAsset.Create(plainData);plain.msaaSampleCount=4;plain.supportsHDR=false;AssetDatabase.CreateAsset(plainData,Art+"/Plain Renderer.asset");AssetDatabase.CreateAsset(plain,Art+"/Plain Pipeline.asset");
   var gameData=ScriptableObject.CreateInstance<UniversalRendererData>();var gamePipeline=UniversalRenderPipelineAsset.Create(gameData);gamePipeline.msaaSampleCount=4;gamePipeline.supportsHDR=false;AssetDatabase.CreateAsset(gameData,Art+"/Game Renderer.asset");AssetDatabase.CreateAsset(gamePipeline,Art+"/Game Pipeline.asset");
   var feature=ScriptableObject.CreateInstance<RisographGameRendererFeature>();feature.name="Risograph Game / once per final stack";feature.material=game;feature.Create();AssetDatabase.AddObjectToAsset(feature,gameData);gameData.rendererFeatures.Add(feature);var serialized=new SerializedObject(gameData);var map=serialized.FindProperty("m_RendererFeatureMap");map.arraySize=1;AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature,out string _,out long id);map.GetArrayElementAtIndex(0).longValue=id;serialized.ApplyModifiedPropertiesWithoutUndo();gameData.SetDirty();EditorUtility.SetDirty(gameData);EditorUtility.SetDirty(feature);
   original.SetActive(true);telltale.SetActive(false);var woodcut=Object.Instantiate(original);woodcut.name="Ariete Woodcut Relief";var woodShader=Shader.Find("Torbellino/Characters/Woodcut Relief");var outline=AssetDatabase.LoadAssetAtPath<Material>("Assets/Rendering/WoodcutRelief/Materials/Contour.mat");
   int count=0;foreach(var mf in woodcut.GetComponentsInChildren<MeshFilter>()){
    var r=mf.GetComponent<MeshRenderer>();var mesh=mf.sharedMesh;if(!r||!mesh)continue;Vector3 size=mesh.bounds.size;Vector3 axis=size.x>size.y&&size.x>size.z?Vector3.right:(size.y>size.z?Vector3.up:Vector3.forward);float radius=axis.x>.5?(size.y+size.z)*.25f:(axis.y>.5?(size.x+size.z)*.25f:(size.x+size.y)*.25f);float scale=(Mathf.Abs(mf.transform.lossyScale.x)+Mathf.Abs(mf.transform.lossyScale.y)+Mathf.Abs(mf.transform.lossyScale.z))/3;
    r.sharedMaterials=r.sharedMaterials.Select(source=>{var m=new Material(woodShader);m.SetColor("_BaseColor",source.GetColor("_BaseColor"));m.SetColor("_EmissionColor",source.IsKeywordEnabled("_EMISSION")?source.GetColor("_EmissionColor"):Color.black);m.SetFloat("_Mapping",2);if(new[]{"actuator","piston","axle","hinge","Hip joint","Knee hub"}.Any(k=>mf.name.IndexOf(k,StringComparison.OrdinalIgnoreCase)>=0))m.SetFloat("_Mapping",1);m.SetVector("_HatchAxis",axis);m.SetVector("_HatchOrigin",mesh.bounds.center);m.SetFloat("_Density",100*scale);m.SetFloat("_WrapCount",Mathf.Max(8,Mathf.Round(radius*scale*Mathf.PI*2*100)));AssetDatabase.CreateAsset(m,Art+"/Materials/Woodcut Part "+count+++".mat");return m;}).ToArray();
    var go=new GameObject("Woodcut contour");go.transform.SetParent(mf.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var hr=go.AddComponent<MeshRenderer>();hr.sharedMaterials=Enumerable.Repeat(outline,mesh.subMeshCount).ToArray();hr.shadowCastingMode=ShadowCastingMode.Off;
   }
   var viewer=new GameObject("Game style controls").AddComponent<RisographGameShowcase>();viewer.original=original;viewer.telltale=telltale;viewer.woodcut=woodcut;viewer.view=camera;viewer.keyLight=sun;viewer.target=target;viewer.distance=distance;viewer.plainPipeline=plain;viewer.gamePipeline=gamePipeline;viewer.mode=1;
   // Warm both renderers/material buffers before the comparative frames.
   viewer.Apply(0);viewer.PositionCamera();Capture(camera,Output+"/warmup-original.png",128,128);viewer.Apply(1);Capture(camera,Output+"/warmup-game.png",128,128);
   foreach(float yaw in new[]{155f,180f,210f}){viewer.yaw=yaw;viewer.PositionCamera();Pair(viewer,"character-"+yaw,1200,1200,legacy,game);}
   viewer.target=target+Vector3.up*2.12f*.22f;viewer.distance=2.12f*.95f;viewer.yaw=163;viewer.PositionCamera();Pair(viewer,"detail",1400,1400,legacy,game);
   viewer.target=target;viewer.distance=distance;for(int i=0;i<12;i++){viewer.yaw=140+i*7;viewer.PositionCamera();Pair(viewer,"orbit-"+i.ToString("D2"),720,720,legacy,game);}
   viewer.yaw=155;viewer.PositionCamera();var rotation=sun.transform.rotation;sun.transform.rotation=Quaternion.Euler(32,65,0);Pair(viewer,"backlight",1000,1000,legacy,game);sun.transform.rotation=rotation;viewer.Apply(1);
   AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Art+"/GameStyleComparison.unity");
   // Archived real FPS screenshots are only used for original vs Riso comparisons; never simulate surface shaders over a PNG.
   foreach(string name in new[]{"RoboticForest","LaboratoryFloor","CrystalGarden"}){
    string file="Artifacts/Gameplay-2026-10-21/Windows/"+name+"-reactive.png";string key="fps-"+name;File.Copy(file,Output+"/"+key+"-original.png",true);Process(file,legacy,Output+"/"+key+"-riso-old.png");Process(file,game,Output+"/"+key+"-riso-game.png");
   }
   if(File.Exists(Output+"/signals-source.png")){Process(Output+"/signals-source.png",legacy,Output+"/signals-riso-old.png");Process(Output+"/signals-source.png",game,Output+"/signals-riso-game.png");}
   Process(Output+"/fps-RoboticForest-original.png",game,Output+"/repeat-riso-game.png");
   var errors=new[]{shader,woodShader,Shader.Find("Torbellino/Telltale Engraving/Character"),legacy.shader}.SelectMany(s=>ShaderUtil.GetShaderMessages(s).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).Select(m=>s.name+": "+m.message)).ToArray();
   File.WriteAllText(Output+"/validation.txt","Unity "+Application.unityVersion+" / "+SystemInfo.graphicsDeviceName+"\nNew game Risograph shader errors: "+errors.Length+"\n"+string.Join("\n",errors)+"\nCharacter views: same geometry, camera, lighting and environment. Original/game processed through actual camera feature. Surface styles are real mesh renders. Legacy Fantasy is GPU-processed from the exact original camera frame.\nWorld inputs are archived 2026-10-21 gameplay screenshots without HUD, not new gameplay or current-scene validation.\n");
   File.WriteAllText(Output+"/game-material-settings.json",EditorJsonUtility.ToJson(game,true));if(errors.Length>0)throw new Exception(string.Join("\n",errors));
   Debug.Log("RISOGRAPH GAME COMPARISON COMPLETE");
  }finally{if(legacy)Object.DestroyImmediate(legacy);GraphicsSettings.defaultRenderPipeline=oldPipeline;QualitySettings.renderPipeline=oldQuality;if(setup.Any(s=>s.isLoaded&&s.isActive))EditorSceneManager.RestoreSceneManagerSetup(setup);}
 }
 static void Pair(RisographGameShowcase v,string key,int width,int height,Material legacy,Material game){v.Apply(0);Capture(v.view,Output+"/"+key+"-original.png",width,height);Process(Output+"/"+key+"-original.png",legacy,Output+"/"+key+"-riso-old.png");v.Apply(1);Capture(v.view,Output+"/"+key+"-riso-game.png",width,height);v.Apply(2);Capture(v.view,Output+"/"+key+"-telltale.png",width,height);v.Apply(3);Capture(v.view,Output+"/"+key+"-woodcut.png",width,height);}
 static Material Legacy(){var m=new Material(Shader.Find("Hidden/Torbellino/Risograph"));SetLegacy(m);return m;}
 static void SetLegacy(Material m){var p=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Rendering/Risograph/RisographFantasy.asset");p.TryGet<RisographVolume>(out var s);m.SetVector("_RisoTone",new Vector4(s.intensity.value,Mathf.Pow(2,s.exposure.value),s.contrast.value,s.saturation.value));m.SetVector("_RisoLightness",new Vector4(s.midtoneLift.value,s.paperHighlights.value,0,0));m.SetVector("_RisoScreen",new Vector4(s.dotSize.value,s.screenAngle.value*Mathf.Deg2Rad,s.halftone.value,(int)s.paletteSize.value));m.SetVector("_RisoTexture",new Vector4(s.paperGrain.value,s.inkWear.value,s.misregistration.value,0));m.SetVector("_ShadowInk",s.shadowInk.value);m.SetVector("_BlueInk",s.blueInk.value);m.SetVector("_PinkInk",s.pinkInk.value);m.SetVector("_Paper",s.paper.value);}
 static void Capture(Camera camera,string path,int width,int height){var target=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var previous=RenderTexture.active;var image=new Texture2D(width,height,TextureFormat.RGB24,false);try{RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});RenderTexture.active=target;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(image);}}
 static void Process(string file,Material material,string path){if(material.shader.name=="Hidden/Torbellino/Risograph")SetLegacy(material);var image=new Texture2D(2,2,TextureFormat.RGBA32,false,false);image.LoadImage(File.ReadAllBytes(file));var target=RenderTexture.GetTemporary(image.width,image.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var previous=RenderTexture.active;var result=new Texture2D(image.width,image.height,TextureFormat.RGB24,false);var cmd=new CommandBuffer();try{var props=new MaterialPropertyBlock();props.SetTexture("_BlitTexture",image);props.SetVector("_BlitScaleBias",new Vector4(1,1,0,0));props.SetVector("_BlitTexture_TexelSize",new Vector4(1f/image.width,1f/image.height,image.width,image.height));cmd.SetGlobalVector("_ScreenParams",new Vector4(image.width,image.height,1+1f/image.width,1+1f/image.height));cmd.SetRenderTarget(target);cmd.DrawProcedural(Matrix4x4.identity,material,0,MeshTopology.Triangles,3,1,props);Graphics.ExecuteCommandBuffer(cmd);RenderTexture.active=target;result.ReadPixels(new Rect(0,0,image.width,image.height),0,0);result.Apply();File.WriteAllBytes(path,result.EncodeToPNG());}finally{RenderTexture.active=previous;cmd.Release();RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(result);Object.DestroyImmediate(image);}}
}
