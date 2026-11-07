using System.IO;
using UnityEditor;
using UnityEngine;
using Torbellino.Rendering;

public static class TelltaleMaterialTools{
 public const string Root="Assets/Rendering/TelltaleEngraving";
 public enum Category{Skin,Hair,Cloth,Armor,Accessory,Low}
 public static Material Convert(Material source,Category kind){
  var m=new Material(Shader.Find("Torbellino/Telltale Engraving/Character"));Apply(m,kind);
  if(source){
   m.SetColor("_BaseColor",source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):source.color);
   foreach(string property in new[]{"_BaseMap","_BumpMap","_OcclusionMap","_EmissionMap"})if(source.HasProperty(property)&&source.GetTexture(property)){m.SetTexture(property,source.GetTexture(property));m.SetTextureScale(property,source.GetTextureScale(property));m.SetTextureOffset(property,source.GetTextureOffset(property));}
   if(source.HasProperty("_MainTex")&&!source.HasProperty("_BaseMap"))m.SetTexture("_BaseMap",source.GetTexture("_MainTex"));
   foreach(string property in new[]{"_BumpScale","_OcclusionStrength","_Metallic","_Smoothness","_Cutoff","_AlphaClip"})if(source.HasProperty(property))m.SetFloat(property,source.GetFloat(property));
   m.SetFloat("_UseNormalMap",source.IsKeywordEnabled("_NORMALMAP")?1:0);m.SetFloat("_UseOcclusion",source.IsKeywordEnabled("_OCCLUSIONMAP")?1:0);m.SetFloat("_UseEmission",source.IsKeywordEnabled("_EMISSION")?1:0);
   if(source.HasProperty("_EmissionColor"))m.SetColor("_EmissionColor",source.GetColor("_EmissionColor"));
   if(m.GetFloat("_AlphaClip")>.5)m.renderQueue=2450;
  }
  if(kind==Category.Low){m.SetFloat("_UseNormalMap",0);m.SetFloat("_UseOcclusion",0);}
  return m;
 }
 public static void Apply(Material m,Category kind){
  m.SetTexture("_HatchTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/HatchAtlas.png"));m.SetFloat("_HatchEnable",1);m.SetFloat("_Projection",0);m.SetFloat("_OutlineWeight",1);m.SetFloat("_GrainIntensity",.006f);
  switch(kind){
   case Category.Skin:m.SetFloat("_Transition",.09f);m.SetFloat("_HatchIntensity",.10f);m.SetFloat("_HatchThickness",.08f);m.SetFloat("_CrossStrength",.05f);m.SetFloat("_HatchStart",.32f);m.SetFloat("_OutlineWeight",.7f);m.SetFloat("_GrainIntensity",.002f);break;
   case Category.Hair:m.SetFloat("_HatchAngle",0);m.SetFloat("_CrossStrength",0);m.SetFloat("_HatchIntensity",.35f);m.SetFloat("_HatchStart",.48f);m.SetFloat("_GrainIntensity",.002f);break;
   case Category.Cloth:m.SetFloat("_HatchIntensity",.42f);m.SetFloat("_CrossStrength",.3f);m.SetFloat("_HatchStart",.5f);m.SetFloat("_Transition",.07f);break;
   case Category.Armor:m.SetFloat("_HatchIntensity",.24f);m.SetFloat("_CrossStrength",.15f);m.SetFloat("_Transition",.035f);m.SetFloat("_Metallic",.25f);break;
   case Category.Accessory:m.SetFloat("_HatchEnable",0);m.SetFloat("_GrainIntensity",0);m.SetFloat("_OutlineWeight",0);break;
   case Category.Low:m.SetFloat("_CrossStrength",0);m.SetFloat("_GrainIntensity",0);m.SetFloat("_HatchIntensity",.2f);m.SetFloat("_UseNormalMap",0);m.SetFloat("_UseOcclusion",0);break;
  }
 }
 public static void CreatePresets(){Directory.CreateDirectory(Root+"/Materials/Presets");foreach(Category kind in System.Enum.GetValues(typeof(Category))){var m=Convert(null,kind);AssetDatabase.CreateAsset(m,Root+"/Materials/Presets/"+kind+".mat");}AssetDatabase.SaveAssets();}
 [MenuItem("Tools/Rendering/Telltale Engraving/Duplicate selected character with shader")]
 public static void Duplicate(){var selected=Selection.activeGameObject;if(!selected)throw new System.InvalidOperationException("Select one character GameObject first.");foreach(var r0 in selected.GetComponentsInChildren<Renderer>())foreach(var m0 in r0.sharedMaterials)if(m0&&m0.HasProperty("_Surface")&&m0.GetFloat("_Surface")>.5f)throw new System.NotSupportedException("Transparent blend material: "+m0.name+". This shader supports opaque and cutout; use an individual supported material selection.");var clone=Object.Instantiate(selected);clone.name=selected.name+" Telltale test";Undo.RegisterCreatedObjectUndo(clone,"Duplicate character with Telltale materials");Directory.CreateDirectory(Root+"/Materials/Converted");foreach(var r in clone.GetComponentsInChildren<Renderer>()){var mats=r.sharedMaterials;for(int i=0;i<mats.Length;i++){if(!mats[i])continue;var m=Convert(mats[i],Category.Armor);m.name=mats[i].name+" Telltale";AssetDatabase.CreateAsset(m,AssetDatabase.GenerateUniqueAssetPath(Root+"/Materials/Converted/"+m.name.Replace('/','_')+".mat"));mats[i]=m;}r.sharedMaterials=mats;}
 var outline=clone.GetComponent<TelltaleOutlineRig>();if(!outline)outline=clone.AddComponent<TelltaleOutlineRig>();outline.outlineTemplate=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Contour.mat");var bounds=new Bounds(clone.transform.position,Vector3.zero);foreach(var r in clone.GetComponentsInChildren<Renderer>())bounds.Encapsulate(r.bounds);clone.transform.position+=Vector3.right*Mathf.Max(bounds.size.x*1.2f,1);Selection.activeGameObject=clone;AssetDatabase.SaveAssets();
 }
}
