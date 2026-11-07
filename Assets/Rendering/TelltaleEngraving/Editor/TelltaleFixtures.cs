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

public static class TelltaleFixtures{
 const string Art=TelltaleMaterialTools.Root;
 static string Out;
 static Camera view;
 static void Save(string key){var rt=RenderTexture.GetTemporary(1000,700,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var previous=RenderTexture.active;var im=new Texture2D(1000,700,TextureFormat.RGB24,false);try{RenderPipeline.SubmitRenderRequest(view,new RenderPipeline.StandardRequest{destination=rt});RenderTexture.active=rt;im.ReadPixels(new Rect(0,0,1000,700),0,0);im.Apply();File.WriteAllBytes(Out+"/"+key+".png",im.EncodeToPNG());}finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(im);}}
 public static void Run(string output,UniversalRendererData renderer){
  bool oldBatch=GraphicsSettings.useScriptableRenderPipelineBatching;try{GraphicsSettings.useScriptableRenderPipelineBatching=false;Out=output;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var preview=new GameObject("Isolated preview pipeline").AddComponent<TelltalePreviewPipeline>();preview.pipeline=GraphicsSettings.defaultRenderPipeline;
  var probe=new SphericalHarmonicsL2();probe.AddAmbientLight(new Color(.2f,.2f,.2f));RenderSettings.ambientProbe=probe;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.fog=false;
  var sun=new GameObject("Fixture key").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1;sun.transform.rotation=Quaternion.Euler(35,-30,0);
  view=new GameObject("Fixture camera").AddComponent<Camera>();view.backgroundColor=new Color(.08f,.09f,.12f);view.clearFlags=CameraClearFlags.SolidColor;view.transform.position=new Vector3(0,1,-8);view.transform.LookAt(new Vector3(0,1,0));view.fieldOfView=35;
  Color[] colors={new Color(.7f,.42f,.28f),new Color(.08f,.04f,.025f),new Color(.6f,.05f,.08f),new Color(.08f,.27f,.65f),new Color(.18f,.45f,.22f)};
  var spheres=new GameObject("Preset and color fixtures");var samples=new Material[5];
  for(int i=0;i<5;i++){var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name=((TelltaleMaterialTools.Category)i).ToString()+" calibration (not an authored character)";go.transform.SetParent(spheres.transform);go.transform.position=new Vector3((i-2)*1.2f,1,0);var material=TelltaleMaterialTools.Convert(null,(TelltaleMaterialTools.Category)i);material.SetColor("_BaseColor",colors[i]);material.SetFloat("_HatchIntensity",i==2?.5f:material.GetFloat("_HatchIntensity"));go.GetComponent<Renderer>().sharedMaterial=material;AssetDatabase.CreateAsset(material,Art+"/Materials/Presets/Sample-"+i+".mat");samples[i]=material;}
  Save("fixtures-warmup");Save("presets-colors");
  foreach(var m in samples)m.SetFloat("_HatchEnable",0);Save("presets-no-hatch");foreach(var m in samples)m.SetFloat("_HatchEnable",1);
  view.transform.position+=new Vector3(.5f,.25f,0);view.transform.LookAt(new Vector3(0,1,0));Save("presets-camera-shift");view.transform.position=new Vector3(0,1,-8);
  sun.transform.rotation=Quaternion.Euler(35,110,0);Save("presets-backlight");sun.transform.rotation=Quaternion.Euler(35,-30,0);
  sun.intensity=.12f;Save("presets-dark");sun.intensity=2;Save("presets-bright");sun.intensity=1;
  var point=new GameObject("Additional point light").AddComponent<Light>();point.type=LightType.Point;point.range=10;point.intensity=4;point.transform.position=new Vector3(0,2,-2);point.color=new Color(.6f,.8f,1);
  renderer.renderingMode=RenderingMode.Forward;renderer.SetDirty();Save("forward-point-warmup");Save("forward-point");
  renderer.renderingMode=RenderingMode.ForwardPlus;renderer.SetDirty();Save("forwardplus-warmup");Save("forwardplus-point");
  renderer.renderingMode=RenderingMode.Forward;renderer.SetDirty();point.enabled=false;Save("forward-no-point");
  view.transform.position=new Vector3(0,1,-2.5f);view.transform.LookAt(new Vector3(0,1,0));Save("cloth-close");samples[2].SetFloat("_HatchEnable",0);Save("cloth-close-no-hatch");samples[2].SetFloat("_HatchEnable",1);
  spheres.SetActive(false);var root=new GameObject("Two bone skinned fixture");var bone0=new GameObject("Root bone").transform;bone0.SetParent(root.transform,false);var bone1=new GameObject("Tip bone").transform;bone1.SetParent(bone0,false);bone1.localPosition=Vector3.up;
  int rings=20,sides=32,count=(rings+1)*(sides+1);var mesh=new Mesh();mesh.name="UV skinned test tube";var vertices=new Vector3[count];var normals=new Vector3[count];var uv=new Vector2[count];var weights=new BoneWeight[count];var triangles=new int[rings*sides*6];
  for(int r=0;r<=rings;r++)for(int side=0;side<=sides;side++){int id=r*(sides+1)+side;float a=side/(float)sides*Mathf.PI*2,y=r/(float)rings*2;vertices[id]=new Vector3(Mathf.Cos(a)*.4f,y,Mathf.Sin(a)*.4f);normals[id]=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));uv[id]=new Vector2(side/(float)sides,r/(float)rings);float w=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1.4f,y));weights[id]=new BoneWeight{boneIndex0=0,weight0=1-w,boneIndex1=1,weight1=w};}
  int t=0;for(int r=0;r<rings;r++)for(int side=0;side<sides;side++){int a=r*(sides+1)+side,b=a+sides+1;triangles[t++]=a;triangles[t++]=b;triangles[t++]=a+1;triangles[t++]=a+1;triangles[t++]=b;triangles[t++]=b+1;}
  mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.triangles=triangles;mesh.boneWeights=weights;mesh.bindposes=new[]{bone0.worldToLocalMatrix*root.transform.localToWorldMatrix,bone1.worldToLocalMatrix*root.transform.localToWorldMatrix};var delta=new Vector3[count];for(int i=0;i<count;i++)delta[i]=normals[i]*Mathf.Max(vertices[i].y-1.1f,0)*.3f;mesh.AddBlendShapeFrame("Expression test",100,delta,new Vector3[count],new Vector3[count]);mesh.RecalculateBounds();mesh.RecalculateTangents();AssetDatabase.CreateAsset(mesh,Art+"/SkinnedValidationMesh.asset");
  var skin=root.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.bones=new[]{bone0,bone1};skin.rootBone=bone0;skin.localBounds=new Bounds(Vector3.up,Vector3.one*5);skin.updateWhenOffscreen=true;skin.forceMatrixRecalculationPerRender=true;skin.sharedMaterial=samples[2];
  var outline=root.AddComponent<TelltaleOutlineRig>();outline.outlineTemplate=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/Contour.mat");outline.Rebuild();
  view.transform.position=new Vector3(0,1,-4);view.transform.LookAt(Vector3.up);Save("skinned-rest");skin.SetBlendShapeWeight(0,100);outline.SyncBlendShapes();Save("skinned-blendshape");skin.SetBlendShapeWeight(0,0);outline.SyncBlendShapes();
  bone1.localRotation=Quaternion.Euler(0,0,38);Save("skinned-bent");bone1.localRotation=Quaternion.identity;
  var motion=root.AddComponent<TelltaleSkinMotion>();motion.tip=bone1;
  var second=Object.Instantiate(root);second.name="Overlap fixture";foreach(var child in second.GetComponentsInChildren<Transform>().Where(t0=>t0.name=="__TEContour").ToArray())Object.DestroyImmediate(child.gameObject);second.GetComponent<TelltaleOutlineRig>().Rebuild();second.transform.position=new Vector3(.45f,0,.4f);second.transform.rotation=Quaternion.Euler(0,22,0);Save("skinned-overlap");second.SetActive(false);
  view.transform.position=new Vector3(0,1,-9);view.transform.LookAt(Vector3.up);Save("skinned-far");view.transform.position=new Vector3(0,1,-4);
  foreach(var hull in root.GetComponentsInChildren<Renderer>().Where(r=>r.name=="__TEContour"))hull.enabled=false;Save("skinned-no-outline");foreach(var hull in root.GetComponentsInChildren<Renderer>().Where(r=>r.name=="__TEContour"))hull.enabled=true;
  // The saved scene lets a human inspect the actual bones in Play. Runtime rebuilds its own outlines.
  Object.DestroyImmediate(outline);foreach(var tr in root.GetComponentsInChildren<Transform>().Where(t0=>t0.name=="__TEContour").ToArray())Object.DestroyImmediate(tr.gameObject);
  var cleanOutline=root.AddComponent<TelltaleOutlineRig>();cleanOutline.outlineTemplate=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/Contour.mat");
  AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Art+"/ValidationFixtures.unity");}finally{GraphicsSettings.useScriptableRenderPipelineBatching=oldBatch;}
 }
}
