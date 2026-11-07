using System;
using System.IO;
using Project.Scripts.Fractures;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class DestructionPlaygroundBuild
{
    public const string ScenePath="Assets/Scenes/DestructionPlayground.unity";
    const string Art="Assets/Art/DestructionPlayground";
    [MenuItem("Tools/HELIX/Destruction/Create playground")]
    public static void Create()
    {
        Directory.CreateDirectory(Art);AssetDatabase.Refresh();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var dark=Mat("Deck",new Color(.055f,.09f,.13f));var cream=Mat("Ceramic shell",new Color(.82f,.9f,.83f));
        var mint=Mat("Turquoise shell",new Color(.04f,.6f,.53f));var orange=Mat("Fresh orange fracture",new Color(1,.29f,.06f));
        var purple=Mat("Violet shell",new Color(.42f,.22f,.72f));var bright=Mat("Fresh pale fracture",new Color(.75f,1,.82f));
        Box("Ground",new Vector3(0,-.35f,3),new Vector3(42,.7f,44),dark).layer=3;
        Box("Back",new Vector3(0,3,24),new Vector3(42,6,.5f),dark);Box("Left boundary",new Vector3(-21,2,3),new Vector3(.5f,4,44),dark);
        Box("Right boundary",new Vector3(21,2,3),new Vector3(.5f,4,44),dark);Box("Front boundary",new Vector3(0,2,-19),new Vector3(42,4,.5f),dark);
        for(int x=-18;x<=18;x+=3)Box("Floor stripe",new Vector3(x,.008f,3),new Vector3(.035f,.015f,42),mint,false);
        Breakable("01 PERFORA",new Vector3(0,2,0),new Vector3(7,4,.65f),mint,orange,64,Anchor.Bottom);
        Breakable("02 ABRE PASO",new Vector3(-10,2,6),new Vector3(5,4,.6f),cream,purple,48,Anchor.Bottom);
        Breakable("03 DERRIBA",new Vector3(9,3,7),new Vector3(1.7f,6,1.7f),purple,bright,40,Anchor.Bottom);
        Breakable("04 DERRIBA",new Vector3(13,4,10),new Vector3(2,8,2),mint,orange,48,Anchor.Bottom);
        Breakable("05 BLOQUE",new Vector3(-5,2.5f,15),new Vector3(1.3f,5,1.3f),mint,orange,32,Anchor.Bottom);
        Breakable("06 BLOQUE",new Vector3(0,2.5f,15),new Vector3(1.3f,5,1.3f),mint,orange,32,Anchor.Bottom);
        Breakable("07 MONOLITO",new Vector3(-2.5f,2.5f,18),new Vector3(3,5,1.3f),cream,purple,40,Anchor.Bottom);
        for(int i=0;i<4;i++)Breakable("CRISTAL "+i,new Vector3(-15+i*2,1.2f,15+(i%2)*2),new Vector3(1.25f,2.4f,1.25f),purple,bright,18,Anchor.Bottom);
        Label("01 / PERFORA",new Vector3(0,4.8f,0));Label("02 / ABRE PASO",new Vector3(-10,4.8f,6));
        Label("03 / DERRIBA LAS TORRES",new Vector3(11,9,10));Label("04 / DEMOLICION",new Vector3(-3,7.2f,15));
        var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=2;sun.transform.rotation=Quaternion.Euler(48,-35,0);sun.shadows=LightShadows.Soft;
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.38f,.48f,.6f);RenderSettings.fog=true;RenderSettings.fogColor=new Color(.09f,.14f,.2f);RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=35;RenderSettings.fogEndDistance=90;
        var player=new GameObject("Sandbox player");player.layer=7;player.transform.position=new Vector3(0,1.1f,-12);
        var cc=player.AddComponent<CharacterController>();cc.height=1.8f;cc.radius=.35f;cc.stepOffset=.3f;
        var play=player.AddComponent<DestructionPlayground>();
        var camera=new GameObject("Sandbox camera").AddComponent<Camera>();camera.transform.SetParent(player.transform,false);camera.transform.localPosition=new Vector3(0,.65f,0);camera.tag="MainCamera";camera.fieldOfView=75;camera.nearClipPlane=.08f;camera.backgroundColor=RenderSettings.fogColor;camera.clearFlags=CameraClearFlags.SolidColor;camera.gameObject.AddComponent<AudioListener>();
        camera.GetUniversalAdditionalCameraData().SetRenderer(1);
        var cannon=Box("Demolition tool",Vector3.zero,new Vector3(.18f,.18f,.5f),cream,false);cannon.transform.SetParent(camera.transform,false);cannon.transform.localPosition=new Vector3(.3f,-.28f,.65f);cannon.layer=2;play.view=camera;play.cannon=cannon.transform;
        var pulse=Mat("Impact flash",Color.white);pulse.shader=Shader.Find("Universal Render Pipeline/Unlit");EditorUtility.SetDirty(pulse);play.pulseMaterial=pulse;
        EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();Debug.Log("DESTRUCTION PLAYGROUND CREATED");
    }
    static Material Mat(string name,Color color)
    {
        string path=Art+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.27f);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
    }
    static GameObject Box(string name,Vector3 position,Vector3 scale,Material material,bool solid=true)
    {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;if(!solid)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;}
    static void Breakable(string name,Vector3 pos,Vector3 size,Material outside,Material inside,int chunks,Anchor anchor)
    {
        var go=Box(name,pos,size,outside);var f=go.AddComponent<FractureThis>();var so=new SerializedObject(f);
        so.FindProperty("chunks").intValue=chunks;so.FindProperty("density").floatValue=4;so.FindProperty("internalStrength").floatValue=45;
        so.FindProperty("anchor").intValue=(int)anchor;so.FindProperty("insideMaterial").objectReferenceValue=inside;so.FindProperty("outsideMaterial").objectReferenceValue=outside;so.ApplyModifiedPropertiesWithoutUndo();
    }
    static void Label(string text,Vector3 position)
    {var go=new GameObject(text);go.transform.position=position;var mesh=go.AddComponent<TextMesh>();mesh.text=text;mesh.anchor=TextAnchor.MiddleCenter;mesh.characterSize=.15f;mesh.fontSize=60;mesh.color=Color.white;}
    [MenuItem("Tools/HELIX/Destruction/Build playground")]
    public static void Build()
    {
        Create();string path="Builds/DestructionPlayground/PatioVoronoi.exe";Directory.CreateDirectory(Path.GetDirectoryName(path));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=path,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Playground build: "+report.summary.result);
        Debug.Log("DESTRUCTION PLAYGROUND BUILD SUCCEEDED");
    }
}
