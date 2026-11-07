using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Torbellino.Rendering
{
    // Installed only into the build's processed scene. Source scenes remain editable as before.
    [DefaultExecutionOrder(30000)]
    public sealed class TelltaleGameStyle : MonoBehaviour
    {
        public Material surface, contour;
        public UniversalRenderPipelineAsset pipeline;
        readonly Dictionary<Material, Material> converted = new Dictionary<Material, Material>();
        readonly HashSet<Renderer> outlined = new HashSet<Renderer>();
        readonly HashSet<Camera> configuredCameras = new HashSet<Camera>();
        readonly List<Material> slots = new List<Material>();
        Renderer[] renderers = Array.Empty<Renderer>();
        float nextScan;
        int total;
        static TelltaleGameStyle instance;
        void Awake()
        {
            if(instance && instance!=this){Destroy(gameObject);return;}
            instance=this;DontDestroyOnLoad(gameObject);
            GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
            SceneManager.sceneLoaded+=Loaded;
            Scan();
        }
        void Loaded(Scene s, LoadSceneMode mode){Scan();}
        void Scan()
        {
            renderers=FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            foreach(var camera in Camera.allCameras)if(configuredCameras.Add(camera))camera.GetUniversalAdditionalCameraData().SetRenderer(0);
            nextScan=Time.unscaledTime+.25f;
            Apply();
        }
        void LateUpdate(){if(Time.unscaledTime>=nextScan)Scan();else Apply();}
        static bool Eligible(Renderer r)=>r && r.name!="__TEContour" && (r is MeshRenderer || r is SkinnedMeshRenderer)
            && !r.GetComponentInParent<Canvas>() && !r.GetComponentInParent<TMPro.TMP_Text>() && !r.GetComponent<TextMesh>();
        void Apply()
        {
            foreach(var r in renderers)
            {
                if(!Eligible(r))continue;
                r.GetSharedMaterials(slots);bool change=false,opaque=false;
                for(int i=0;i<slots.Count;i++)
                {
                    var src=slots[i];if(!src || !src.shader)continue;
                    if(src.shader==surface.shader){opaque|=src.GetFloat("_Surface")<.5f;continue;}
                    if(!converted.TryGetValue(src,out var dst)){dst=Convert(src);converted.Add(src,dst);}
                    slots[i]=dst;change=true;opaque|=dst.GetFloat("_Surface")<.5f;
                }
                if(change){r.SetSharedMaterials(slots);total++;}
                if(opaque && !r.isPartOfStaticBatch && outlined.Add(r))
                {
                    // Each rig owns only this mesh. Nested renderers get their own rig later.
                    var rig=r.gameObject.AddComponent<TelltaleSingleOutline>();rig.source=r;rig.template=contour;
                }
            }
        }
        Material Convert(Material src)
        {
            var m=new Material(surface){name=src.name+" [Telltale]",enableInstancing=true};
            m.SetColor("_BaseColor",src.HasProperty("_BaseColor")?src.GetColor("_BaseColor"):src.HasProperty("_Color")?src.GetColor("_Color"):Color.white);
            string main=src.HasProperty("_BaseMap")?"_BaseMap":"_MainTex";
            if(src.HasProperty(main) && src.GetTexture(main))
            {m.SetTexture("_BaseMap",src.GetTexture(main));m.SetTextureScale("_BaseMap",src.GetTextureScale(main));m.SetTextureOffset("_BaseMap",src.GetTextureOffset(main));}
            foreach(string p in new[]{"_BumpMap","_OcclusionMap","_EmissionMap"})if(src.HasProperty(p)&&src.GetTexture(p))m.SetTexture(p,src.GetTexture(p));
            foreach(string p in new[]{"_BumpScale","_OcclusionStrength","_Metallic","_Smoothness","_Cutoff","_AlphaClip","_Cull"})if(src.HasProperty(p))m.SetFloat(p,src.GetFloat(p));
            if(src.HasProperty("_Glossiness")&&!src.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",src.GetFloat("_Glossiness"));
            m.SetFloat("_UseNormalMap",src.IsKeywordEnabled("_NORMALMAP")?1:0);
            m.SetFloat("_UseOcclusion",src.IsKeywordEnabled("_OCCLUSIONMAP")?1:0);
            // Property-block animated emitters also need emission enabled even when initially dark.
            m.SetFloat("_UseEmission",1);
            m.SetColor("_EmissionColor",src.HasProperty("_EmissionColor")?src.GetColor("_EmissionColor"):Color.black);
            bool cutout=src.IsKeywordEnabled("_ALPHATEST_ON") || (src.HasProperty("_AlphaClip")&&src.GetFloat("_AlphaClip")>.5f) || src.GetTag("RenderType",false)=="TransparentCutout";
            m.SetFloat("_AlphaClip",cutout?1:0);
            bool transparent=!cutout && ((src.HasProperty("_Surface")&&src.GetFloat("_Surface")>.5f)||src.renderQueue>=3000);
            if(transparent)
            {
                m.SetFloat("_Surface",1);m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);m.SetFloat("_ZWrite",0);m.renderQueue=3000;
                m.SetShaderPassEnabled("ShadowCaster",false);m.SetShaderPassEnabled("DepthOnly",false);m.SetShaderPassEnabled("DepthNormals",false);
            }
            else m.renderQueue=cutout?2450:2000;
            return m;
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-telltale-smoke");
            if(index<0)yield break;
            string folder=index+1<args.Length?args[index+1]:Application.persistentDataPath;Directory.CreateDirectory(folder);
            // Exercise scene changes and newly spawned actors using the actual player build.
            foreach(string scene in new[]{"LaboratoryFloor","RoboticForest","CrystalGarden","Level1","SampleScene"})
            {
                if(SceneManager.GetActiveScene().name!=scene)yield return SceneManager.LoadSceneAsync(scene);
                yield return new WaitForSecondsRealtime(scene=="LaboratoryFloor"?25:5);Scan();yield return new WaitForEndOfFrame();
                if(scene=="LaboratoryFloor" && Array.IndexOf(args,"-telltale-hatch-comparison")>=0)yield return CaptureHatchComparison(folder);
                int styled=0,remaining=0;
                foreach(var r in renderers)if(Eligible(r))foreach(var m in r.sharedMaterials)if(m){if(m.shader==surface.shader)styled++;else remaining++;}
                ScreenCapture.CaptureScreenshot(Path.Combine(folder,scene+".png"));
                int actors=FindObjectsByType<NormalEnemyStateMachine>(FindObjectsSortMode.None).Length;
                File.AppendAllText(Path.Combine(folder,"smoke.txt"),scene+": Telltale slots="+styled+", other mesh slots="+remaining+", enemies="+actors+", converted renderers="+total+", pipeline="+GraphicsSettings.currentRenderPipeline.name+"\n");
                yield return new WaitForSecondsRealtime(1);
            }
            Debug.Log("TELLTALE GAME SMOKE COMPLETE");Application.Quit();
        }
        IEnumerator CaptureHatchComparison(string folder)
        {
            string[] properties={"_HatchWorldUnits","_HatchScale","_HatchIntensity","_HatchThickness","_HatchStart","_HatchContrast","_CrossStrength","_HatchWaviness"};
            float[] previousPreset={0,8,.42f,.15f,.5f,1.2f,.3f,1};
            var snapshot=new Dictionary<Material,float[]>();
            float previousTimeScale=Time.timeScale;Time.timeScale=0;
            try
            {
                foreach(var m in converted.Values)if(m){var values=new float[properties.Length];for(int i=0;i<values.Length;i++){values[i]=m.GetFloat(properties[i]);m.SetFloat(properties[i],previousPreset[i]);}snapshot.Add(m,values);}
                yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(folder,"rayado-antes.png"));
                yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
                foreach(var pair in snapshot)if(pair.Key)for(int i=0;i<properties.Length;i++)pair.Key.SetFloat(properties[i],pair.Value[i]);
                yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(folder,"rayado-ahora.png"));
                yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
            }
            finally
            {
                foreach(var pair in snapshot)if(pair.Key)for(int i=0;i<properties.Length;i++)pair.Key.SetFloat(properties[i],pair.Value[i]);
                Time.timeScale=previousTimeScale;
            }
        }
        void OnDestroy(){if(instance!=this)return;SceneManager.sceneLoaded-=Loaded;foreach(var m in converted.Values)if(m)Destroy(m);instance=null;}
    }

    public sealed class TelltaleSingleOutline : MonoBehaviour
    {
        public Renderer source;public Material template;
        Renderer hull;Material[] owned;
        void Start()
        {
            if(!source||!template)return;
            Mesh mesh=source is SkinnedMeshRenderer s?s.sharedMesh:GetComponent<MeshFilter>()?.sharedMesh;if(!mesh)return;
            var go=new GameObject("__TEContour");go.layer=gameObject.layer;go.transform.SetParent(transform,false);
            if(source is SkinnedMeshRenderer skin){var h=go.AddComponent<SkinnedMeshRenderer>();h.sharedMesh=mesh;h.bones=skin.bones;h.rootBone=skin.rootBone;h.localBounds=skin.localBounds;h.updateWhenOffscreen=skin.updateWhenOffscreen;hull=h;}
            else{go.AddComponent<MeshFilter>().sharedMesh=mesh;hull=go.AddComponent<MeshRenderer>();}
            owned=new Material[mesh.subMeshCount];var slots=source.sharedMaterials;
            for(int i=0;i<owned.Length;i++)
            {
                var m=new Material(template);owned[i]=m;var src=slots[Mathf.Min(i,slots.Length-1)];
                if(!src)continue;
                m.SetTexture("_BaseMap",src.GetTexture("_BaseMap"));m.SetTextureScale("_BaseMap",src.GetTextureScale("_BaseMap"));m.SetTextureOffset("_BaseMap",src.GetTextureOffset("_BaseMap"));m.SetColor("_BaseColor",src.GetColor("_BaseColor"));m.SetFloat("_AlphaClip",src.GetFloat("_AlphaClip"));m.SetFloat("_Cutoff",src.GetFloat("_Cutoff"));m.SetFloat("_OutlineEnabled",src.GetFloat("_Surface")>.5f?0:1);
            }
            hull.sharedMaterials=owned;hull.shadowCastingMode=ShadowCastingMode.Off;hull.receiveShadows=false;
            // Include the hull in exactly the same LOD entries as its source.
            var group=source.GetComponentInParent<LODGroup>();if(group){var lods=group.GetLODs();for(int i=0;i<lods.Length;i++)if(Array.IndexOf(lods[i].renderers,source)>=0){var list=new List<Renderer>(lods[i].renderers){hull};lods[i].renderers=list.ToArray();}group.SetLODs(lods);}
        }
        void LateUpdate(){if(!hull||!source)return;hull.enabled=source.enabled;hull.forceRenderingOff=source.forceRenderingOff;if(source is SkinnedMeshRenderer s&&hull is SkinnedMeshRenderer h)for(int i=0;i<s.sharedMesh.blendShapeCount;i++)h.SetBlendShapeWeight(i,s.GetBlendShapeWeight(i));}
        void OnDestroy(){if(hull)Destroy(hull.gameObject);if(owned!=null)foreach(var m in owned)if(m)Destroy(m);}
    }
}
