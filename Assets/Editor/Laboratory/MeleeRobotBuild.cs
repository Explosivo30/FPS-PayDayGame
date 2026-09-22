using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

public static class MeleeRobotBuild
{
    const string Folder="Assets/Art/MeleeRobot";
    public const string PrefabPath=Folder+"/SecurityBrawler.prefab";
    [MenuItem("Tools/HELIX/Create melee security robot")]
    public static void Configure()
    {
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var importer=(ModelImporter)AssetImporter.GetAtPath(Folder+"/MeleeRobot.fbx");
        importer.importAnimation=false;importer.bakeAxisConversion=true;importer.materialImportMode=ModelImporterMaterialImportMode.None;
        importer.SaveAndReimport();
        var materials=new Dictionary<string,Material>();
        materials["Ivory"]=Mat("Ivory",new Color(.65f,.76f,.75f),.15f);
        materials["Orange"]=Mat("Orange",new Color(.94f,.22f,.035f),.18f);
        materials["Graphite"]=Mat("Graphite",new Color(.018f,.03f,.047f),.25f);
        materials["Steel"]=Mat("Steel",new Color(.12f,.19f,.23f),.5f);
        materials["Optic"]=Mat("Optic",new Color(.03f,.85f,.77f),.1f,new Color(.04f,1.4f,1.25f));
        materials["Warning"]=Mat("Warning",new Color(1,.55f,.02f),.1f,new Color(1.6f,.48f,.015f));
        // FBX contains named rigid parts. Reparenting with world transforms intact gives clean Unity joint axes.
        var root=new GameObject("Security brawler");
        var visual=new GameObject("Brawler visual").transform;visual.SetParent(root.transform,false);
        var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/MeleeRobot.fbx"),visual);
        var joints=new Dictionary<string,Transform>();
        joints["Pelvis"]=Joint("Pelvis",visual,new Vector3(0,.98f,0));
        joints["Torso"]=Joint("Torso",visual,new Vector3(0,1.15f,0));
        joints["Head"]=Joint("Head",joints["Torso"],new Vector3(0,1.72f,0));
        foreach(var side in new[]{"L","R"})
        {
            float s=side=="L"?-1:1;
            joints["UpperArm_"+side]=Joint("UpperArm_"+side,joints["Torso"],new Vector3(s*.49f,1.58f,0));
            joints["Forearm_"+side]=Joint("Forearm_"+side,joints["UpperArm_"+side],new Vector3(s*.59f,1.23f,0));
            joints["Thigh_"+side]=Joint("Thigh_"+side,visual,new Vector3(s*.2f,.93f,0));
            joints["Shin_"+side]=Joint("Shin_"+side,joints["Thigh_"+side],new Vector3(s*.2f,.57f,0));
        }
        // Material slots retain names even with external remapping; use the source part's material identifier.
        foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>())
        {
            var key=joints.Keys.OrderByDescending(k=>k.Length).First(k=>renderer.name.StartsWith(k+"_"));
            renderer.transform.SetParent(joints[key],true);
            string n=renderer.name;
            string mat=n.Contains("Optic")?"Optic":n.Contains("Core")||n.Contains("Signal")?"Warning":
                n.Contains("Shoulder")||n.Contains("Gauntlet")||n.Contains("Hazard")||n.Contains("Battery")||n.Contains("Belt")||n.Contains("Chin")||n.Contains("Guard")||n.Contains("Toe")?"Orange":
                n.Contains("Frame")||n.Contains("Bib")||n.Contains("BackPack")||n.Contains("Mask")||n.Contains("Fist")||n.Contains("Panel")||n.Contains("Knee")||n.Contains("Boot")?"Graphite":
                n.Contains("Bearing")||n.Contains("Elbow")||n.Contains("Spine")||n.Contains("Piston")||n.Contains("Hip")||n.Contains("Neck")||n.Contains("Thumb")||n.Contains("Vent")?"Steel":"Ivory";
            renderer.sharedMaterial=materials[mat];
        }
        Object.DestroyImmediate(model);
        var nav=root.AddComponent<NavMeshAgent>();nav.radius=.44f;nav.height=2.05f;nav.stoppingDistance=1.53f;
        nav.speed=3.8f;nav.acceleration=18;nav.angularSpeed=420;nav.obstacleAvoidanceType=ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        var capsule=root.AddComponent<CapsuleCollider>();capsule.height=1.98f;capsule.radius=.42f;capsule.center=Vector3.up*.99f;
        var reactions=root.AddComponent<RobotPresentation>();reactions.visual=visual;reactions.telegraphStrength=.22f;
        reactions.hitEffectMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/CombatFeel/Impact particles.mat");
        var enemy=root.AddComponent<NormalEnemyStateMachine>();enemy.agent=nav;enemy.maxHealth=85;enemy.attackRange=1.85f;
        enemy.layerMask=~(1<<6|1<<8|1<<2);
        root.AddComponent<MeleeRobotCombat>();
        var pose=root.AddComponent<MeleeRobotPresentation>();pose.torso=joints["Torso"];pose.head=joints["Head"];
        pose.leftArm=joints["UpperArm_L"];pose.rightArm=joints["UpperArm_R"];
        pose.leftForearm=joints["Forearm_L"];pose.rightForearm=joints["Forearm_R"];
        pose.leftThigh=joints["Thigh_L"];pose.rightThigh=joints["Thigh_R"];
        pose.leftShin=joints["Shin_L"];pose.rightShin=joints["Shin_R"];
        var trailPoint=Joint("Fist trail",pose.rightForearm,new Vector3(.6f,.86f,.29f));
        var trail=trailPoint.gameObject.AddComponent<TrailRenderer>();trail.time=.12f;trail.minVertexDistance=.025f;
        trail.widthMultiplier=.12f;trail.emitting=false;trail.sharedMaterial=reactions.hitEffectMaterial;
        trail.startColor=new Color(1,.55f,.03f,.8f);trail.endColor=new Color(1,.14f,.02f,0);
        pose.fistTrail=trail;
        pose.warning=Warning(joints["Head"],materials["Warning"]);
        pose.windupSound=AssetDatabase.LoadAssetAtPath<AudioClip>(Folder+"/Warning.wav");
        pose.swingSound=AssetDatabase.LoadAssetAtPath<AudioClip>(Folder+"/Swing.wav");
        pose.contactSound=AssetDatabase.LoadAssetAtPath<AudioClip>(Folder+"/Contact.wav");
        pose.stepSound=AssetDatabase.LoadAssetAtPath<AudioClip>(Folder+"/Step.wav");
        foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=6;
        pose.fistTrail.gameObject.layer=2;pose.warning.gameObject.layer=2;
        var bounds=new Bounds(new Vector3(0,1,0),Vector3.zero);
        foreach(var r in visual.GetComponentsInChildren<MeshRenderer>())if(r!=pose.warning)bounds.Encapsulate(r.bounds);
        if(bounds.size.y<1.8f||bounds.size.y>2.3f)throw new Exception("Unexpected imported robot dimensions: "+bounds);
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);Object.DestroyImmediate(root);
        var setup=EditorSceneManager.GetSceneManagerSetup();
        foreach(var path in new[]{"Assets/Scenes/TowerCore.unity","Assets/Scenes/SampleScene.unity"})
        {
            var scene=EditorSceneManager.OpenScene(path);
            foreach(var gm in Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None))
            { LabBuild.Set(gm,"meleeEnemyPrefab",prefab);LabBuild.Set(gm,"meleeShare",.4f); }
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        if(setup.Any(s=>s.isLoaded))EditorSceneManager.RestoreSceneManagerSetup(setup);
        AssetDatabase.SaveAssets();Debug.Log("MELEE ROBOT READY: "+bounds.size);
    }
    static Transform Joint(string name,Transform parent,Vector3 world)
    { var t=new GameObject(name).transform;t.SetParent(parent,false);t.position=world;return t; }
    static Material Mat(string name,Color color,float metal,Color? emission=null)
    {
        string path=Folder+"/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null) { m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path); }
        m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",.28f);m.enableInstancing=true;
        if(emission.HasValue) { m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",emission.Value); }
        EditorUtility.SetDirty(m);return m;
    }
    static Renderer Warning(Transform head,Material material)
    {
        var go=new GameObject("Attack warning");go.transform.SetParent(head,false);go.transform.position=new Vector3(0,2.23f,.04f);
        string path=Folder+"/Warning triangle.asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(mesh==null)
        {
            mesh=new Mesh();mesh.vertices=new[]{new Vector3(0,.10f,0),new Vector3(-.1f,-.07f,0),new Vector3(.1f,-.07f,0)};
            mesh.triangles=new[]{0,1,2,2,1,0};mesh.RecalculateNormals();AssetDatabase.CreateAsset(mesh,path);
        }
        string symbolPath=Folder+"/Warning symbol.mat";
        var symbol=AssetDatabase.LoadAssetAtPath<Material>(symbolPath);
        if(symbol==null) { symbol=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(symbol,symbolPath); }
        symbol.SetColor("_BaseColor",new Color(1.5f,.65f,.025f));EditorUtility.SetDirty(symbol);
        go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=symbol;renderer.enabled=false;return renderer;
    }
    public static void ConfigureAndQuit() { Configure();EditorApplication.Exit(0); }
    public static void Play() { TowerValidation.Play(); }
    public static void Build() { TowerValidation.Build(); }
    public static void ConfigureBuildAndPlay() { Configure();Build();Play(); }
}
