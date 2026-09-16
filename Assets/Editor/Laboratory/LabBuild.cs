using System;
using System.IO;
using System.Linq;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static partial class LabBuild
{
    const string Folder="Assets/Art/Laboratory";
    const string PlayerPath="Assets/Prefabs/Player/-----Player.prefab";
    static Material white,dark,cyan,amber,glass,steel,unlit;
    static Transform world;
    [MenuItem("Tools/HELIX/Rebuild laboratory")]
    public static void Generate()
    {
        Directory.CreateDirectory(Folder);Directory.CreateDirectory("Artifacts");
        AssetDatabase.Refresh();
        Materials();
        ImportArms();
        var playerPrefab=CreatePlayer();
        var enemyPrefab=CreateRobot();
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach(var root in scene.GetRootGameObjects())
        {
            if(root.name=="Canvases"||root.name=="EventSystem"||root.name=="Managers Of the game") continue;
            Object.DestroyImmediate(root);
        }
        var managers=Object.FindFirstObjectByType<GameManager>();
        if(managers==null) throw new Exception("Existing game managers were not found.");
        foreach(var surface in Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None))
            Object.DestroyImmediate(surface.gameObject);
        foreach(var enemy in Object.FindObjectsByType<NormalEnemyStateMachine>(FindObjectsSortMode.None)) Object.DestroyImmediate(enemy.gameObject);
        world=new GameObject("HELIX / Containment Laboratory").transform;
        Arena();
        var player=(GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
        player.transform.SetPositionAndRotation(new Vector3(0,1.05f,-15),Quaternion.identity);
        var playerState=player.GetComponentInChildren<PlayerStateMachine>();
        var shop=Object.FindFirstObjectByType<ShopManager>();
        var shopPanel=(GameObject)Get(shop,"shopUI");
        foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(shopPanel!=null && (canvas.gameObject==shopPanel || shopPanel.transform.IsChildOf(canvas.transform))) continue;
            if(canvas.name.StartsWith("Canvas ")) canvas.gameObject.SetActive(false);
        }
        if(shopPanel!=null)
        {
            foreach(var c in shopPanel.GetComponentsInParent<Canvas>(true)) { c.gameObject.SetActive(true);c.renderMode=RenderMode.ScreenSpaceOverlay;c.sortingOrder=20; }
            shopPanel.SetActive(false);
        }
        var legacyCanvas=GameObject.Find("Canvases");
        if(legacyCanvas!=null)
            foreach(var label in legacyCanvas.GetComponentsInChildren<TMP_Text>(true))
                if(shopPanel==null||!label.transform.IsChildOf(shopPanel.transform)) label.gameObject.SetActive(false);
        var portalParent=new GameObject("Wave entries").transform;portalParent.SetParent(world);
        var entries=new Transform[4];
        Vector3[] points={new Vector3(-21,0,-12),new Vector3(21,0,-12),new Vector3(-21,0,13),new Vector3(21,0,13)};
        for(int i=0;i<4;i++) { entries[i]=new GameObject("Entry "+(i+1)).transform;entries[i].SetParent(portalParent);entries[i].position=points[i]; }
        Set(managers,"enemyPrefab",enemyPrefab);Set(managers,"spawnPoints",entries);
        Set(managers,"baseEnemyCount",5);Set(managers,"incrementPerWave",2);Set(managers,"maxAlive",12);
        Set(managers,"baseSpeed",3.4f);Set(managers,"speedIncrement",.15f);Set(managers,"announcementDuration",8f);Set(managers,"spawnInterval",.65f);
        var nav=new GameObject("Lab Navigation").AddComponent<NavMeshSurface>();
        nav.collectObjects=CollectObjects.All;nav.useGeometry=NavMeshCollectGeometry.PhysicsColliders;nav.layerMask=1<<3;
        nav.overrideVoxelSize=true;nav.voxelSize=.12f;nav.BuildNavMesh();
        if(nav.navMeshData==null) throw new Exception("Lab NavMesh build failed.");
        string navPath=Folder+"/LaboratoryNavMesh.asset";
        var oldNav=AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
        if(oldNav!=null) { EditorUtility.CopySerialized(nav.navMeshData,oldNav);Object.DestroyImmediate(nav.navMeshData);nav.navMeshData=oldNav; }
        else AssetDatabase.CreateAsset(nav.navMeshData,navPath);
        nav.RemoveData();nav.AddData();
        var hud=CreateHUD(playerState);
        Set(managers,"scoreText",hud.scoreText);Set(managers,"roundText",null);Set(managers,"waveCompletePanel",null);
        ConfigureLighting();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        File.WriteAllText("Artifacts/lab-build.txt","Generated HELIX laboratory, player, robot, navigation and HUD.\n"+DateTime.UtcNow.ToString("O"));
        Debug.Log("LAB BUILD COMPLETE");
    }
    static void Materials()
    {
        white=Mat("Ceramic",new Color(.59f,.68f,.73f),.2f);
        dark=Mat("Graphite",new Color(.025f,.049f,.075f),.35f);
        steel=Mat("Titanium",new Color(.12f,.18f,.23f),.6f);
        cyan=Mat("Ion cyan",new Color(.04f,.7f,.9f),.2f,new Color(.03f,1.5f,2.1f));
        amber=Mat("Safety amber",new Color(.9f,.37f,.07f),.25f,new Color(1.5f,.4f,.02f));
        glass=Mat("Containment glass",new Color(.03f,.22f,.27f,.18f),.2f);
        glass.SetFloat("_Surface",1);glass.SetFloat("_Blend",0);glass.SetFloat("_ZWrite",0);
        glass.SetFloat("_SrcBlend",5);glass.SetFloat("_DstBlend",10);glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.renderQueue=3000;
        unlit=Asset<Material>("Trace",()=>new Material(Shader.Find("Universal Render Pipeline/Unlit")));
        unlit.SetColor("_BaseColor",new Color(2,.8f,.12f));unlit.enableInstancing=true;
    }
    static Material Mat(string name,Color color,float metallic,Color? emission=null)
    {
        var m=Asset<Material>(name,()=>new Material(Shader.Find("Universal Render Pipeline/Lit")));
        m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",.42f);
        if(emission.HasValue) { m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",emission.Value); }
        m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
    }
    static T Asset<T>(string name,Func<T> create) where T:Object
    {
        string p=Folder+"/"+name+".asset";
        var asset=AssetDatabase.LoadAssetAtPath<T>(p);
        if(asset==null) { asset=create();asset.name=name;AssetDatabase.CreateAsset(asset,p); }
        EditorUtility.SetDirty(asset);return asset;
    }
    static GameObject CreateRobot()
    {
        var go=new GameObject("Laboratory sentinel");go.layer=6;
        var agent=go.AddComponent<NavMeshAgent>();agent.radius=.48f;agent.height=1.65f;agent.baseOffset=0;agent.stoppingDistance=2;agent.speed=3.4f;agent.acceleration=12;agent.angularSpeed=360;
        var collider=go.AddComponent<CapsuleCollider>();collider.radius=.5f;collider.height=1.6f;collider.center=new Vector3(0,.8f,0);
        var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/EnemyRobots/Prefabs/MachineGunRobot.prefab"),go.transform);
        model.name="Sentinel visual";model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;model.transform.localScale=Vector3.one*.62f;
        foreach(var light in model.GetComponentsInChildren<Light>()) Object.DestroyImmediate(light.gameObject);
        foreach(var line in model.GetComponentsInChildren<LineRenderer>()) Object.DestroyImmediate(line);
        foreach(var particles in model.GetComponentsInChildren<ParticleSystem>()) if(particles!=null) Object.DestroyImmediate(particles.gameObject);
        foreach(var r in model.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial=r.name.Contains("Eye")||r.name.Contains("Glass")?amber:steel;
        var presentation=go.AddComponent<RobotPresentation>();presentation.visual=model.transform;
        presentation.turret=model.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="MachineGunRobot_GunJoint");
        presentation.muzzle=new GameObject("Muzzle").transform;presentation.muzzle.SetParent(model.transform,false);presentation.muzzle.localPosition=new Vector3(0,1.7f,.9f);
        presentation.sparks=Object.Instantiate(EffectAsset("Impact",false),go.transform).GetComponent<ParticleSystem>();presentation.sparks.transform.localPosition=Vector3.up*.7f;
        presentation.muzzleFlash=Object.Instantiate(EffectAsset("Muzzle",true),presentation.muzzle).GetComponent<ParticleSystem>();
        var enemy=go.AddComponent<NormalEnemyStateMachine>();enemy.agent=agent;enemy.layerMask=~(1<<6|1<<8|1<<2);enemy.maxHealth=100;
        var lr=go.AddComponent<LineRenderer>();ConfigureTracer(lr,.025f);
        Layers(go,6);var result=PrefabUtility.SaveAsPrefabAsset(go,Folder+"/LaboratorySentinel.prefab");Object.DestroyImmediate(go);return result;
    }
    static void ConfigureTracer(LineRenderer lr,float width)
    {
        lr.sharedMaterial=unlit;lr.positionCount=2;lr.useWorldSpace=true;lr.startWidth=width;lr.endWidth=width*.3f;lr.enabled=false;
        lr.shadowCastingMode=ShadowCastingMode.Off;lr.receiveShadows=false;
    }
    static GameObject EffectAsset(string name,bool muzzle)
    {
        string assetPath=Folder+"/"+name+".prefab";
        var exists=AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);if(exists!=null)return exists;
        var go=new GameObject(name);var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main;main.playOnAwake=false;main.loop=false;main.duration=.3f;main.startLifetime=muzzle?.035f:.22f;main.startSpeed=muzzle?.2f:3;main.startSize=muzzle?.10f:.035f;main.startColor=new Color(1,.7f,.25f);main.maxParticles=32;main.simulationSpace=ParticleSystemSimulationSpace.World;main.gravityModifier=muzzle?0:.35f;
        var emission=ps.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)(muzzle?3:12))});
        var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=35;shape.radius=.015f;
        var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=unlit;renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=muzzle?1:2;
        var saved=PrefabUtility.SaveAsPrefabAsset(go,assetPath);Object.DestroyImmediate(go);return saved;
    }
    static void Layers(GameObject go,int layer) { foreach(var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=layer; }
    static object Get(Object obj,string field) { return obj.GetType().GetField(field,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public)?.GetValue(obj); }
    public static void Set(Object target,string field,object value)
    {
        if(target==null)return;
        var so=new SerializedObject(target);var p=so.FindProperty(field);if(p==null) throw new Exception(target.name+" missing "+field);
        if(value is Array array) { p.arraySize=array.Length;for(int i=0;i<array.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=(Object)array.GetValue(i); }
        else if(value is int n) p.intValue=n;else if(value is float f)p.floatValue=f;else if(value is bool b)p.boolValue=b;
        else if(value is Vector3 v)p.vector3Value=v;else p.objectReferenceValue=value as Object;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    static GameObject Box(string name,Transform parent,Vector3 pos,Vector3 size,Material mat,bool collision=true)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=size;
        go.GetComponent<Renderer>().sharedMaterial=mat;go.layer=collision?3:2;
        if(!collision)Object.DestroyImmediate(go.GetComponent<Collider>());else go.isStatic=true;
        return go;
    }
    static GameObject Cylinder(string name,Vector3 pos,Vector3 scale,Material mat,bool collision=true)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(world,false);go.transform.localPosition=pos;go.transform.localScale=scale;go.layer=collision?3:2;go.GetComponent<Renderer>().sharedMaterial=mat;
        if(!collision)Object.DestroyImmediate(go.GetComponent<Collider>());return go;
    }
    static void Sign(string text,Vector3 pos,Quaternion rot,float size,Color color)
    {
        var go=new GameObject(text);go.transform.SetParent(world,false);go.transform.localPosition=pos;go.transform.localRotation=rot;
        var label=go.AddComponent<TextMeshPro>();label.text=text;label.fontSize=size;label.alignment=TextAlignmentOptions.Center;label.color=color;label.rectTransform.sizeDelta=new Vector2(12,3);
    }
}
