using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
namespace Torbellino.Rendering{
// Geometry-only companion. Meshes, bones and source materials remain owned by the character.
public sealed class TelltaleOutlineRig:MonoBehaviour{
 public Material outlineTemplate;
 sealed class BlendPair{public SkinnedMeshRenderer source,target;public float[] previous;}
 readonly List<BlendPair> blends=new List<BlendPair>();
 readonly List<GameObject> objects=new List<GameObject>();readonly List<Material> materials=new List<Material>();
 void Start(){Rebuild();}
 [ContextMenu("Rebuild geometry outlines")]
 public void Rebuild(){Clear();if(!outlineTemplate)return;
  foreach(var source in GetComponentsInChildren<Renderer>()){
   if(source.name=="__TEContour"||!source.enabled||source.sharedMaterials.Length==0)continue;
   Mesh mesh=null;var skin=source as SkinnedMeshRenderer;var filter=source.GetComponent<MeshFilter>();if(skin)mesh=skin.sharedMesh;else if(filter)mesh=filter.sharedMesh;if(!mesh)continue;
   var go=new GameObject("__TEContour");go.layer=source.gameObject.layer;go.transform.SetParent(source.transform,false);objects.Add(go);Renderer hull;
   if(skin){var h=go.AddComponent<SkinnedMeshRenderer>();h.sharedMesh=mesh;h.bones=skin.bones;h.rootBone=skin.rootBone;h.localBounds=skin.localBounds;h.updateWhenOffscreen=skin.updateWhenOffscreen;h.quality=skin.quality;h.forceMatrixRecalculationPerRender=skin.forceMatrixRecalculationPerRender;hull=h;if(mesh.blendShapeCount>0)blends.Add(new BlendPair{source=skin,target=h,previous=System.Linq.Enumerable.Repeat(float.NaN,mesh.blendShapeCount).ToArray()});}
   else{go.AddComponent<MeshFilter>().sharedMesh=mesh;hull=go.AddComponent<MeshRenderer>();}
   var slots=new Material[mesh.subMeshCount];var original=source.sharedMaterials;
   for(int i=0;i<slots.Length;i++){var m=new Material(outlineTemplate);var from=original[Mathf.Min(i,original.Length-1)];if(!from){slots[i]=m;materials.Add(m);continue;}foreach(string name in new[]{"_BaseMap"})if(from.HasProperty(name)&&from.GetTexture(name))m.SetTexture(name,from.GetTexture(name));if(from.HasProperty("_BaseMap")){m.SetTextureScale("_BaseMap",from.GetTextureScale("_BaseMap"));m.SetTextureOffset("_BaseMap",from.GetTextureOffset("_BaseMap"));}if(from.HasProperty("_BaseColor"))m.SetColor("_BaseColor",from.GetColor("_BaseColor"));foreach(string name in new[]{"_AlphaClip","_Cutoff"})if(from.HasProperty(name))m.SetFloat(name,from.GetFloat(name));if(from.HasProperty("_OutlineWeight")){float weight=from.GetFloat("_OutlineWeight");m.SetFloat("_OutlineWidth",m.GetFloat("_OutlineWidth")*weight);m.SetFloat("_OutlineEnabled",weight>0?1:0);}slots[i]=m;materials.Add(m);}
   hull.sharedMaterials=slots;hull.shadowCastingMode=ShadowCastingMode.Off;hull.receiveShadows=false;
  }
 SyncBlendShapes();}
 public void SyncBlendShapes(){foreach(var pair in blends)for(int i=0;i<pair.previous.Length;i++){float w=pair.source.GetBlendShapeWeight(i);if(w!=pair.previous[i]){pair.target.SetBlendShapeWeight(i,w);pair.previous[i]=w;}}}
 void LateUpdate(){if(blends.Count>0)SyncBlendShapes();}
 void OnDisable(){Clear();}
 void Clear(){blends.Clear();foreach(var go in objects)if(go){if(Application.isPlaying)Destroy(go);else DestroyImmediate(go);}objects.Clear();foreach(var m in materials)if(m){if(Application.isPlaying)Destroy(m);else DestroyImmediate(m);}materials.Clear();}
}
}

