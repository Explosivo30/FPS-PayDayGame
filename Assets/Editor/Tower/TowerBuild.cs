using System;
using System.IO;
using System.Linq;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static partial class TowerBuild
{
    const string Art="Assets/Art/Tower";
    static Transform world;
    static Material copper,ink,jade,mint,ivory,gold,glow,pink,violet,soil;
    static Mesh hex,leaf,crystal;
    public static readonly string[] ScenePaths={"Assets/Scenes/LaboratoryFloor.unity","Assets/Scenes/RoboticForest.unity","Assets/Scenes/CrystalGarden.unity","Assets/Scenes/TowerCore.unity","Assets/Scenes/Level1.unity","Assets/Scenes/SampleScene.unity"};
    [MenuItem("Tools/Tower/Build playable floors")]
    public static void Generate()
    {
        Directory.CreateDirectory(Art);Directory.CreateDirectory("Artifacts/Tower");
        AssetDatabase.Refresh();
        Materials();Meshes();
        BuildCore();
        BuildLaboratory();
        BuildForest();
        BuildGarden();
        EditorBuildSettings.scenes=ScenePaths.Select(p=>new EditorBuildSettingsScene(p,true)).ToArray();
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/Scenes/RoboticForest.unity");
        Debug.Log("TOWER SCENES COMPLETE");
    }
    static bool SharedRoot(GameObject root)
    {
        return root.GetComponentInChildren<PlayerStateMachine>(true)!=null ||
            root.GetComponentInChildren<GameManager>(true)!=null ||
            root.GetComponentInChildren<Canvas>(true)!=null ||
            root.GetComponentInChildren<EventSystem>(true)!=null;
    }
    static void BuildCore()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        foreach(var root in scene.GetRootGameObjects()) if(!SharedRoot(root)) Object.DestroyImmediate(root);
        var gm=Object.FindFirstObjectByType<GameManager>();
        gm.AutomaticWaves=false;
        LabBuild.Set(gm,"spawnPoints",new Transform[0]);LabBuild.Set(gm,"playerPoints",100);
        var player=Object.FindFirstObjectByType<PlayerStateMachine>();
        var session=new GameObject("Tower session").AddComponent<TowerSession>();
        session.player=player;session.weapons=player.GetComponentInChildren<GunController>(true);
        foreach(var gun in player.GetComponentsInChildren<BaseGun>(true))
        {
            LabBuild.Set(gun,"limitedReserve",true);
            int reserve=gun is Pistol?72:gun is MachineGun?180:36;
            LabBuild.Set(gun,"reserveAmmo",reserve);LabBuild.Set(gun,"maxReserveAmmo",reserve);
        }
        ConvertLegacyMaterials(player);
        var hud=Object.FindFirstObjectByType<LabHUD>();
        foreach(var label in hud.GetComponentsInChildren<TMP_Text>(true))
        {
            if(label.text.Contains("CONTENCIÓN")) label.gameObject.SetActive(false);
            if(label.text.Contains("E  TIENDA")) label.text="R  RECARGAR     Q  CAMBIAR     E  INTERACTUAR";
        }
        session.overlay=CreateOverlay();
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/TowerCore.unity");
    }
    static void ConvertLegacyMaterials(PlayerStateMachine player)
    {
        foreach(var renderer in player.GetComponentsInChildren<Renderer>(true))
        {
            var materials=renderer.sharedMaterials;bool changed=false;
            for(int i=0;i<materials.Length;i++)
            {
                var source=materials[i];
                if(source==null||source.shader==null) continue;
                string shader=source.shader.name;
                if(!shader.StartsWith("Standard")&&!shader.StartsWith("Legacy Shaders")&&shader!="Autodesk Interactive") continue;
                string id=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
                string path=Art+"/Weapon "+id+" "+source.name.Replace("/","_")+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(material==null) { material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path); }
                material.SetColor("_BaseColor",source.HasProperty("_Color")?source.GetColor("_Color"):Color.white);
                if(source.HasProperty("_MainTex"))
                {
                    material.SetTexture("_BaseMap",source.GetTexture("_MainTex"));
                    material.SetTextureScale("_BaseMap",source.GetTextureScale("_MainTex"));
                    material.SetTextureOffset("_BaseMap",source.GetTextureOffset("_MainTex"));
                }
                material.SetFloat("_Metallic",source.HasProperty("_Metallic")?source.GetFloat("_Metallic"):.4f);
                material.SetFloat("_Smoothness",source.HasProperty("_Glossiness")?source.GetFloat("_Glossiness"):.35f);
                if(source.HasProperty("_BumpMap")&&source.GetTexture("_BumpMap")!=null)
                { material.SetTexture("_BumpMap",source.GetTexture("_BumpMap"));material.EnableKeyword("_NORMALMAP"); }
                EditorUtility.SetDirty(material);materials[i]=material;changed=true;
            }
            if(changed) LabBuild.Set(renderer,"m_Materials",materials);
        }
    }
    static void BuildLaboratory()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        foreach(var root in scene.GetRootGameObjects()) if(SharedRoot(root)) Object.DestroyImmediate(root);
        world=new GameObject("Tower integration").transform;
        var floor=world.gameObject.AddComponent<TowerFloor>();
        floor.displayName="LABORATORIO";floor.floorNumber=1;floor.nextScene="RoboticForest";
        floor.briefing="LABORATORIO · 3 oleadas desbloquean el ascensor. E en terminales para mejorar o reponer.";
        floor.enemyHealth=85;floor.enemyDamage=8;
        floor.spawnPoints=GameObject.Find("Wave entries").GetComponentsInChildren<Transform>().Skip(1).ToArray();
        var wall=GameObject.Find("South wall");if(wall!=null)Object.DestroyImmediate(wall);
        Box("South wall left",new Vector3(-7,4,-20),new Vector3(34,8,.6f),ivory);
        Box("South wall right",new Vector3(19,4,-20),new Vector3(10,8,.6f),ivory);
        floor.elevator=Elevator(new Vector3(12,0,-19.7f),1);floor.arrival=floor.elevator.transform.Find("Arrival");
        Supplies(new Vector3(-5,0,-16),false);
        Bake(scene,"LaboratoryFloor");
    }
    static void Materials()
    {
        copper=Mat("Copper",new Color(.7f,.28f,.105f),.75f);
        ink=Mat("Ink",new Color(.025f,.055f,.095f),.1f);
        jade=Mat("Jade enamel",new Color(.075f,.42f,.29f),.35f);
        mint=Mat("Mint enamel",new Color(.22f,.72f,.44f),.4f);
        ivory=Mat("Ivory enamel",new Color(.65f,.77f,.65f),.1f);
        gold=Mat("Signal gold",new Color(1,.57f,.095f),.4f);
        glow=Mat("Live mint",new Color(.12f,.85f,.62f),.1f,new Color(.05f,.6f,.3f));
        pink=Mat("Live coral",new Color(.95f,.19f,.39f),.3f,new Color(.55f,.025f,.1f));
        violet=Mat("Crystal violet",new Color(.36f,.22f,.7f),.6f);
        soil=Mat("Forest deck",new Color(.065f,.14f,.14f),0);
    }
    static Material Mat(string name,Color color,float gloss,Color? emission=null)
    {
        string path=Art+"/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null) { m=new Material(Shader.Find("Tower/Cel Metal"));AssetDatabase.CreateAsset(m,path); }
        m.SetColor("_BaseColor",color);m.SetFloat("_Gloss",gloss);m.SetColor("_EmissionColor",emission??Color.black);
        m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
    }
    static Mesh SaveMesh(string name,Vector3[] vertices,int[] triangles)
    {
        string path=Art+"/"+name+".asset";
        var m=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(m==null) { m=new Mesh();m.name=name;AssetDatabase.CreateAsset(m,path); }
        m.Clear();
        // Split vertices at every facet to keep sharp graphic normals.
        var flat=triangles.Select(i=>vertices[i]).ToArray();
        m.vertices=flat;m.triangles=Enumerable.Range(0,flat.Length).ToArray();m.RecalculateNormals();m.RecalculateBounds();
        EditorUtility.SetDirty(m);return m;
    }
    static void Meshes()
    {
        var v=new System.Collections.Generic.List<Vector3>();
        var t=new System.Collections.Generic.List<int>();
        for(int i=0;i<6;i++) { float a=i*Mathf.PI/3;v.Add(new Vector3(Mathf.Cos(a)*.5f,0,Mathf.Sin(a)*.5f));v.Add(new Vector3(Mathf.Cos(a)*.5f,1,Mathf.Sin(a)*.5f)); }
        v.Add(Vector3.zero);v.Add(Vector3.up);
        for(int i=0;i<6;i++) { int a=i*2,b=(i*2+2)%12;t.AddRange(new[]{a,b,a+1,b,b+1,a+1,12,b,a,13,a+1,b+1}); }
        for(int i=0;i<t.Count;i+=3) { int swap=t[i+1];t[i+1]=t[i+2];t[i+2]=swap; }
        hex=SaveMesh("Hex column",v.ToArray(),t.ToArray());
        leaf=SaveMesh("Folded canopy blade",new[]{new Vector3(0,0,0),new Vector3(-.5f,.06f,.45f),new Vector3(0,.24f,.55f),new Vector3(.5f,.06f,.45f),new Vector3(0,0,1),new Vector3(0,-.07f,.55f)},
            new[]{0,1,2,0,2,3,1,4,2,2,4,3,0,5,1,0,3,5,1,5,4,3,4,5});
        crystal=SaveMesh("Crystal spear",new[]{new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(.5f,0,.5f),new Vector3(-.5f,0,.5f),new Vector3(0,1,0),new Vector3(0,-.2f,0)},
            new[]{0,4,1,1,4,2,2,4,3,3,4,0,0,1,5,1,2,5,2,3,5,3,0,5});
    }
    static GameObject Shape(string name,Mesh mesh,Vector3 p,Vector3 scale,Material material,bool collision=false,Transform parent=null)
    {
        var go=new GameObject(name);go.transform.SetParent(parent!=null?parent:world,false);go.transform.localPosition=p;go.transform.localScale=scale;
        go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;go.layer=collision?3:2;go.isStatic=true;
        if(collision) { var c=go.AddComponent<MeshCollider>();c.sharedMesh=mesh; }
        return go;
    }
    static GameObject Box(string name,Vector3 p,Vector3 scale,Material mat,bool collision=true,Transform parent=null)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent!=null?parent:world,false);
        go.transform.localPosition=p;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mat;go.layer=collision?3:2;
        if(!collision)Object.DestroyImmediate(go.GetComponent<Collider>());
        go.isStatic=true;return go;
    }
    static void Beam(string name,Vector3 a,Vector3 b,float radius,Material mat,Transform parent=null)
    {
        var go=Shape(name,hex,a,new Vector3(radius,Vector3.Distance(a,b),radius),mat,false,parent);
        go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
    }
    static TextMeshPro Sign(string text,Vector3 p,float size,Color color,Quaternion? rotation=null,Transform parent=null)
    {
        var go=new GameObject("Sign / "+text);go.transform.SetParent(parent!=null?parent:world,false);go.transform.localPosition=p;go.transform.localRotation=rotation??Quaternion.identity;
        var label=go.AddComponent<TextMeshPro>();label.text=text;label.fontSize=size;label.color=color;label.alignment=TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta=new Vector2(18,4);go.layer=2;return label;
    }
    static void Lighting(bool garden)
    {
        RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=garden?new Color(.45f,.4f,.59f):new Color(.34f,.53f,.46f);
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Exponential;RenderSettings.fogDensity=.009f;
        RenderSettings.fogColor=garden?new Color(.15f,.15f,.3f):new Color(.10f,.22f,.21f);
        var sun=new GameObject("Canopy skylight").AddComponent<Light>();sun.transform.SetParent(world);
        sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(52,-35,0);sun.intensity=1.5f;sun.color=new Color(1,.9f,.73f);sun.shadows=LightShadows.Soft;
        var volume=new GameObject("Cel color grade").AddComponent<Volume>();volume.transform.SetParent(world);volume.isGlobal=true;
        string path=Art+"/"+(garden?"Garden":"Forest")+" grade.asset";
        var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if(profile==null) { profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,path); }
        if(!profile.TryGet<Bloom>(out var bloom))bloom=profile.Add<Bloom>();bloom.intensity.Override(.15f);bloom.threshold.Override(1.25f);
        if(!profile.TryGet<Tonemapping>(out var tone))tone=profile.Add<Tonemapping>();tone.mode.Override(TonemappingMode.Neutral);
        foreach(var c in profile.components)if(string.IsNullOrEmpty(AssetDatabase.GetAssetPath(c)))AssetDatabase.AddObjectToAsset(c,profile);
        volume.sharedProfile=profile;EditorUtility.SetDirty(profile);
    }
    static void Bake(Scene scene,string name)
    {
        var nav=Object.FindFirstObjectByType<NavMeshSurface>();
        if(nav==null)nav=new GameObject(name+" Navigation").AddComponent<NavMeshSurface>();
        nav.RemoveData();nav.navMeshData=null;nav.collectObjects=CollectObjects.All;
        nav.useGeometry=NavMeshCollectGeometry.PhysicsColliders;nav.layerMask=1<<3;nav.overrideVoxelSize=true;nav.voxelSize=.14f;nav.BuildNavMesh();
        string path=Art+"/"+name+" Navigation.asset";
        var existing=AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
        if(existing!=null) { EditorUtility.CopySerialized(nav.navMeshData,existing);Object.DestroyImmediate(nav.navMeshData);nav.navMeshData=existing; }
        else AssetDatabase.CreateAsset(nav.navMeshData,path);
        nav.RemoveData();nav.AddData();
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/"+name+".unity");
    }
    static TowerOverlay CreateOverlay()
    {
        var root=new GameObject("Tower HUD");var canvas=root.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=40;
        var scaler=root.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
        var ui=root.AddComponent<TowerOverlay>();
        ui.floorText=Label(root.transform,"Floor",new Vector2(50,-92),new Vector2(1300,32),18,new Vector2(0,1));
        ui.floorText.color=new Color(.5f,1,.73f);
        ui.objectiveText=Label(root.transform,"Objective",new Vector2(50,-126),new Vector2(1400,40),18,new Vector2(0,1));
        ui.messageText=Label(root.transform,"Notice",new Vector2(0,-190),new Vector2(1500,90),24,new Vector2(.5f,1));ui.messageText.alignment=TextAlignmentOptions.Center;ui.messageText.rectTransform.pivot=new Vector2(.5f,1);
        ui.interactionText=Label(root.transform,"Interaction",new Vector2(0,-120),new Vector2(1000,60),23,new Vector2(.5f,.5f));ui.interactionText.alignment=TextAlignmentOptions.Center;ui.interactionText.rectTransform.pivot=new Vector2(.5f,1);
        var cover=new GameObject("Travel fade");cover.transform.SetParent(root.transform,false);
        var image=cover.AddComponent<Image>();image.color=new Color(.015f,.025f,.04f);image.raycastTarget=false;
        image.rectTransform.anchorMin=Vector2.zero;image.rectTransform.anchorMax=Vector2.one;image.rectTransform.sizeDelta=Vector2.zero;
        ui.fade=cover.AddComponent<CanvasGroup>();ui.fade.alpha=0;ui.fade.blocksRaycasts=false;
        return ui;
    }
    static TextMeshProUGUI Label(Transform parent,string text,Vector2 position,Vector2 size,float font,Vector2 anchor)
    {
        var go=new GameObject(text);go.transform.SetParent(parent,false);var t=go.AddComponent<TextMeshProUGUI>();
        t.text=text;t.fontSize=font;t.color=Color.white;t.raycastTarget=false;
        t.rectTransform.anchorMin=t.rectTransform.anchorMax=anchor;t.rectTransform.pivot=new Vector2(0,1);t.rectTransform.anchoredPosition=position;t.rectTransform.sizeDelta=size;return t;
    }
}
