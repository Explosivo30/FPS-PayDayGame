using System;
using System.Collections.Generic;
using System.IO;
using Project.Scripts.Fractures;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Composed combat room. Permanent routes work with the membrane intact or open.</summary>
public static class FoundryArenaBuild
{
    public const string ScenePath = "Assets/Scenes/FundicionReactiva.unity";
    public const string ArtPath = "Assets/Art/FoundryArena";
    public const string BuildPath = "Builds/FoundryRebuilt/Trasvase.exe";
    const string CorePath = "Assets/Scenes/TowerCore.unity";
    const float Height = 2.8f, Thickness = .32f, MembraneYaw = 47.7263f;
    static readonly Vector3 South = new Vector3(9, Height, -4), North = new Vector3(-1, Height, 7);
    static readonly Vector3 SouthRampBase = new Vector3(9, 0, -12), SouthRampTop = new Vector3(9, Height, -5.6f);
    static readonly Vector3 NorthRampTop = new Vector3(-1, Height, 9.2f), NorthRampBase = new Vector3(-1, 0, 14);
    static readonly Vector3 Membrane = new Vector3(3.1f, 1.125f, 2.5f);
    sealed class Palette { public Material deck, worn, ceramic, jade, dark, copper, root, leaf, coral, interior, mint, warm, glass; }

    [MenuItem("Tools/HELIX/Foundry/Create reactive foundry")]
    public static void Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Rebuild outside Play Mode.");
        Directory.CreateDirectory(ArtPath + "/Geometry"); AssetDatabase.Refresh();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var world = new GameObject("TRASVASE - chamber, gallery and extraction").transform;
        var director = world.gameObject.AddComponent<FoundryArenaDirector>();
        var structure = Child("01 Permanent architecture - navigation layer 3", world);
        var detail = Child("02 Visual assemblies - no collision", world);
        var mechanisms = Child("03 Membrane and objectives - excluded from bake", world);
        var positions = Child("04 Encounter positions and sectors", world);
        var p = Materials();
        Chamber(structure, detail, p); TransferOrgan(structure, detail, p); Gallery(structure, detail, p);
        Interchange(structure, detail, mechanisms, director, p);
        ArrivalAndExit(structure, detail, mechanisms, positions, director, p);
        EncounterPositions(positions, director); AuditMarkers(world, director); Lighting(world);
        director.rangedPrefab = EnemyPrefab("Assets/Art/Laboratory/LaboratorySentinel.prefab");
        director.meleePrefab = EnemyPrefab("Assets/Art/MeleeRobot/SecurityBrawler.prefab");
        director.crane = null; director.petal = null;
        Physics.SyncTransforms();
        var surface = Child("06 Navigation - permanent layer 3", world).gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All; surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = 1 << 3; surface.overrideVoxelSize = true; surface.voxelSize = .08f;
        BakeNavigation(surface, world);
        if (surface.navMeshData == null) throw new InvalidOperationException("Transfer chamber navigation bake returned no data.");
        AssertNavigation(director.arrival.position - Vector3.up * 1.05f);
        SaveNavigation(surface);
        foreach (var obstacle in world.GetComponentsInChildren<NavMeshObstacle>(true)) obstacle.enabled = true;
        EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
        Debug.Log("TRASVASE CREATED: 28x30m room, internal diagonal gallery at 2.8m, reciprocal membrane crossing, recoverable carbine and extraction. No tower floor regenerated.");
    }

    static Palette Materials()
    {
        return new Palette
        {
            deck = Mat("20 Matte petrol deck", new Color(.095f,.17f,.18f), .18f,.15f),
            worn = Mat("21 Worn service deck", new Color(.12f,.205f,.21f), .22f,.18f),
            ceramic = Mat("22 Warm ceramic enamel", new Color(.78f,.80f,.68f), .44f,.12f),
            jade = Mat("23 Deep jade shell", new Color(.12f,.31f,.285f), .4f,.2f),
            dark = Mat("24 Graphite machinery", new Color(.038f,.065f,.075f), .24f,.35f),
            copper = Mat("25 Brushed copper", new Color(.53f,.255f,.115f), .42f,.65f),
            root = Mat("26 Oxidised root conduits", new Color(.24f,.31f,.22f), .26f,.36f),
            leaf = Mat("27 Porcelain leaves", new Color(.40f,.56f,.32f), .36f,.14f),
            coral = Mat("28 Frangible coral membrane", new Color(.69f,.29f,.225f), .28f,.05f),
            interior = Mat("29 Fresh membrane interior", new Color(.96f,.64f,.39f), .16f,0),
            mint = Mat("30 Transfer indicators", new Color(.32f,.83f,.64f), .3f,.15f,new Color(.16f,.58f,.37f)),
            warm = Mat("31 Chamber heat", new Color(.94f,.54f,.18f), .25f,.15f,new Color(1.35f,.47f,.08f)),
            glass = Mat("32 Roof diffusion", new Color(.38f,.51f,.46f), .32f,.08f,new Color(.10f,.17f,.13f))
        };
    }

    static void Chamber(Transform s, Transform d, Palette p)
    {
        Box("Main foundation - 28 by 30 metres",s,new Vector3(0,-.35f,0),new Vector3(28,.7f,30),p.deck);
        for (int x=0;x<7;x++) for(int z=0;z<10;z++)
            Box("Deck tile "+x+" "+z,d,new Vector3(-12+x*4,.014f,-13.5f+z*3),new Vector3(3.97f,.025f,2.97f),(x+z*2)%5==0?p.worn:p.deck,false);
        Box("West wall",s,new Vector3(-14.25f,4,0),new Vector3(.5f,8,30.5f),p.dark);
        Box("East wall",s,new Vector3(14.25f,4,0),new Vector3(.5f,8,30.5f),p.dark);
        Box("South wall west of arrival",s,new Vector3(-10.5f,4,-15.25f),new Vector3(7,8,.5f),p.jade);
        Box("South wall east of arrival",s,new Vector3(6.5f,4,-15.25f),new Vector3(15,8,.5f),p.jade);
        Box("South arrival lintel",s,new Vector3(-4,5.8f,-15.25f),new Vector3(6,4.4f,.5f),p.dark);
        Box("North objective backdrop",s,new Vector3(-5.5f,4,15.25f),new Vector3(17,8,.5f),p.jade);
        Box("North exit east jamb",s,new Vector3(11.5f,4,15.25f),new Vector3(5,8,.5f),p.jade);
        Box("North exit lintel",s,new Vector3(6,5.8f,15.25f),new Vector3(6,4.4f,.5f),p.dark);
        // These wall-attached masses interrupt the otherwise dominant perimeter orbit.
        Bevel("West manifold - joins organ to wall",s,new Vector3(-11.2f,2,1.6f),new Vector3(5.6f,4,5.2f),.25f,p.jade,true);
        Bevel("East exchanger housing",s,new Vector3(12.2f,2.7f,3),new Vector3(3.6f,5.4f,5.2f),.3f,p.jade,true);
        Bevel("North west intake housing",s,new Vector3(-12.2f,1.4f,13.2f),new Vector3(3.6f,2.8f,3.6f),.22f,p.dark,true);
        Bevel("South east ceramic abutment",s,new Vector3(12.5f,1.1f,-12.8f),new Vector3(3,2.2f,4.4f),.25f,p.dark,true);
        foreach(float z in new[]{-12f,-5f,7f,13f}) foreach(int side in new[]{-1,1})
        {
            Box("Wall pier "+side+" "+z,d,new Vector3(side*13.78f,4,z),new Vector3(.34f,8,.75f),p.copper,false);
            Box("Wall ceramic bay "+side+" "+z,d,new Vector3(side*13.85f,4.9f,z+1.5f),new Vector3(.16f,3.2f,2.5f),p.ceramic,false);
        }
        for(int i=0;i<5;i++)
        {
            float x=-11+i*5.5f;
            Box("North enamel bay "+i,d,new Vector3(x,5.5f,14.93f),new Vector3(3.8f,3.7f,.12f),p.ceramic,false);
            Box("South enamel bay "+i,d,new Vector3(x,5.5f,-14.93f),new Vector3(3.8f,3.7f,.12f),p.ceramic,false);
        }
        var roof=Box("Continuous roof - enclosed chamber",d,new Vector3(0,9.3f,0),new Vector3(28.5f,.3f,30.5f),p.dark,false);
        roof.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        foreach(float z in new[]{-11f,-2f,8f,14f})
        {
            Box("Roof truss "+z,d,new Vector3(0,8.6f,z),new Vector3(28.1f,.55f,.65f),p.copper,false);
            var diffusion=Box("Roof diffusion panel "+z,d,new Vector3(3.5f,9.08f,z-2.1f),new Vector3(13,.08f,3.4f),p.glass,false);
            diffusion.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            Box("Roof light strip "+z,d,new Vector3(3.5f,8.98f,z-3.8f),new Vector3(11,.09f,.12f),p.ceramic,false);
        }
        Portal(d,new Vector3(13.77f,0,9.4f),-90,"02",p);
        Portal(d,new Vector3(-9.5f,0,14.94f),0,"03",p);
    }

    static void TransferOrgan(Transform s, Transform d, Palette p)
    {
        var organ=Child("Transfer organ - environmental protagonist",d);
        Bevel("Organ permanent foot",s,new Vector3(-6.5f,.55f,1),new Vector3(6,1.1f,8),.35f,p.dark,true);
        Bevel("Organ permanent body",s,new Vector3(-6.5f,2.5f,1),new Vector3(5.8f,3.9f,7.8f),.5f,p.jade,true);
        Bevel("Organ ceramic front",organ,new Vector3(-6.5f,2.45f,-3.02f),new Vector3(4.6f,2.85f,.28f),.1f,p.ceramic,false);
        Box("Warm transfer throat",organ,new Vector3(-6.5f,2.3f,-3.18f),new Vector3(1.9f,1.9f,.08f),p.warm,false);
        foreach(int side in new[]{-1,1})
        {
            Box("Organ ceramic cheek "+side,organ,new Vector3(-6.5f+side*2.87f,2.6f,.6f),new Vector3(.12f,2.4f,5.8f),p.ceramic,false);
            Cylinder("Pressure column "+side,organ,new Vector3(-6.5f+side*1.8f,4.6f,.9f),.65f,2.5f,p.copper);
            Cylinder("Column enamel sleeve "+side,organ,new Vector3(-6.5f+side*1.8f,4.5f,.9f),.70f,1.1f,p.ceramic);
        }
        Bevel("Organ upper collar",organ,new Vector3(-6.5f,4.6f,.8f),new Vector3(5.5f,.5f,5.9f),.4f,p.copper,false);
        Cylinder("Living transfer trunk",organ,new Vector3(-6.6f,5.6f,1.3f),.64f,3,p.root);
        var crown=new Vector3(-6.6f,6,1.3f);
        var tips=new[]{new Vector3(-10.6f,7.6f,-1),new Vector3(-2.4f,7.4f,-.4f),new Vector3(-10.5f,7.2f,5),new Vector3(-2.1f,7.8f,4.9f),new Vector3(-6.3f,8.2f,-3.2f)};
        for(int i=0;i<tips.Length;i++)
        {
            var bend=Vector3.Lerp(crown,tips[i],.55f)+Vector3.up*.5f;
            Pipe("Collector stem "+i,organ,new[]{crown,bend,tips[i]},.14f,p.copper);
            Leaf("Collector leaf "+i,organ,Vector3.Lerp(bend,tips[i],.15f),tips[i],1.5f,p.leaf);
        }
        Pipe("West root feed",organ,new[]{new Vector3(-13.9f,4.6f,1.5f),new Vector3(-10.6f,5.1f,1.5f),new Vector3(-8.6f,5.8f,1.3f)},.3f,p.root);
        Pipe("Overhead transfer main",organ,new[]{new Vector3(-4.9f,5.6f,1.3f),new Vector3(-3.6f,7.9f,2.4f),new Vector3(8.5f,7.9f,2.4f),new Vector3(12.3f,5.6f,3)},.32f,p.copper);
        foreach(float z in new[]{1.5f,3f,4.5f})
        {
            Box("Exchanger ceramic fin "+z,d,new Vector3(10.31f,3.2f,z),new Vector3(.18f,3.8f,.48f),p.ceramic,false);
            Box("Exchanger seam "+z,d,new Vector3(10.2f,3.2f,z),new Vector3(.07f,1.5f,.10f),p.mint,false);
        }
        Box("Organ foot contact band",organ,new Vector3(-3.52f,.65f,1),new Vector3(.08f,.18f,7.2f),p.copper,false);
        Sign("TRASVASE",organ,new Vector3(-6.5f,3.35f,-3.23f),0,1.25f,new Vector2(4,.5f),p.dark.color);
        Sign("07",organ,new Vector3(-6.5f,1.15f,-3.23f),0,1.6f,new Vector2(2,.5f),p.dark.color);
    }

    static void Gallery(Transform s, Transform d, Palette p)
    {
        var gallery=Child("Gallery - crossed room and two grounded ends",d);
        // Slopes meet the outer edge of each landing. A ramp aimed at a landing's
        // centre leaves an unwalkable riser where the horizontal deck starts.
        Ramp("South gallery approach",s,SouthRampBase,SouthRampTop,4.4f,p.worn);
        Deck("South gallery landing",s,South,new Vector2(4.4f,3.2f),p.ceramic);
        var delta=North-South;
        var bridge=Box("Diagonal gallery - clearance 2.48m",s,(South+North)*.5f-Vector3.up*Thickness*.5f,new Vector3(4.2f,Thickness,delta.magnitude+.15f),p.ceramic);
        bridge.transform.rotation=Quaternion.LookRotation(delta.normalized);
        Deck("North gallery landing and objective",s,North,new Vector2(4.4f,4.4f),p.ceramic);
        Ramp("North gallery descent",s,NorthRampTop,NorthRampBase,4.4f,p.worn);
        Vector3 side=Vector3.Cross(Vector3.up,delta.normalized);
        foreach(int sign in new[]{-1,1})
        {
            var a=South+side*(sign*2.04f)+Vector3.up*.06f;var b=North+side*(sign*2.04f)+Vector3.up*.06f;
            Beam("Gallery copper edge "+sign,gallery,a,b,.085f,.07f,p.copper);
            var railA=Vector3.Lerp(a,b,sign==-1?.03f:.55f);var railB=Vector3.Lerp(a,b,sign==-1?.25f:.70f);
            Beam("Limited gallery rail "+sign,gallery,railA+Vector3.up*.8f,railB+Vector3.up*.8f,.10f,.12f,p.copper);
            Tube("Rail post A "+sign,gallery,railA,railA+Vector3.up*.8f,.055f,p.copper);
            Tube("Rail post B "+sign,gallery,railB,railB+Vector3.up*.8f,.055f,p.copper);
        }
        // Supports at ends leave the central ground crossing free; no invisible floor blockers.
        foreach(var foot in new[]{new Vector3(9,0,-4.8f),new Vector3(-1.2f,0,8.6f)})
        {
            Box("Gallery support pier",s,foot+Vector3.up*1.2f,new Vector3(.75f,2.4f,.75f),p.copper);
            Box("Gallery support shoe",s,foot+Vector3.up*.12f,new Vector3(1.2f,.24f,1.2f),p.dark);
        }
        var baffle=Box("South landing angle baffle",s,new Vector3(10.95f,4.05f,-4.15f),new Vector3(.28f,2.5f,2.7f),p.jade);
        baffle.transform.rotation=Quaternion.Euler(0,-16,0);
        // A service berth anchors the later gallery deployment to the installation.
        // These shallow markings and the overhead fixture never obstruct the walking strip.
        Cylinder("Gallery service berth rim",gallery,new Vector3(8.8f,Height+.025f,-3.8f),.86f,.035f,p.copper);
        Cylinder("Gallery service berth inset",gallery,new Vector3(8.8f,Height+.048f,-3.8f),.70f,.024f,p.jade);
        foreach(int sign in new[]{-1,1})
            Box("Service berth ceramic index "+sign,gallery,new Vector3(8.8f+sign*.71f,Height+.064f,-3.8f),new Vector3(.17f,.018f,.34f),p.ceramic,false);
        Tube("Maintenance rail upright",gallery,new Vector3(10.7f,2.9f,-4.15f),new Vector3(10.7f,5.3f,-4.15f),.09f,p.copper);
        Tube("Maintenance rail arm",gallery,new Vector3(10.7f,5.3f,-4.15f),new Vector3(9.5f,5.3f,-4.15f),.09f,p.copper);
        Bevel("Maintenance deployment head",gallery,new Vector3(9.5f,5.15f,-4.15f),new Vector3(.65f,.28f,.50f),.08f,p.ceramic,false);
    }

    static void Interchange(Transform s, Transform d, Transform m, FoundryArenaDirector director, Palette p)
    {
        // Disposable membrane follows the gallery underneath, rather than carrying its weight.
        director.breach=Frangible("Reciprocal under-gallery membrane",m,Membrane,new Vector3(5.8f,2.25f,.32f),MembraneYaw,p.coral,p.interior,32,Anchor.Top|Anchor.Left|Anchor.Right);
        director.breach.opensPassage=true;director.breach.structuralHealth=72;
        director.breach.passageCenter=new Vector3(Membrane.x,1.05f,Membrane.z);director.breach.passageHalfExtents=new Vector3(.62f,.94f,.62f);
        var gate=director.breach.gameObject.AddComponent<NavMeshObstacle>();gate.shape=NavMeshObstacleShape.Box;gate.center=Vector3.zero;
        gate.size=new Vector3(5.8f,2.25f,.4f);gate.carving=true;gate.carveOnlyStationary=false;gate.enabled=false;director.breach.obstacle=gate;
        Vector3 tangent=Quaternion.Euler(0,MembraneYaw,0)*Vector3.right;
        foreach(int sign in new[]{-1,1})
        {
            var end=Membrane+tangent*(sign*3.0f);
            Box("Membrane fixed jamb "+sign,s,new Vector3(end.x,1.22f,end.z),new Vector3(.22f,2.44f,.22f),p.dark);
            Box("Membrane ceramic jamb "+sign,d,new Vector3(end.x,1.25f,end.z),new Vector3(.26f,1.85f,.26f),p.ceramic,false);
        }
        var top=new Vector3(Membrane.x,2.37f,Membrane.z);
        Beam("Membrane hanging copper rail",d,top-tangent*3,top+tangent*3,.20f,.18f,p.copper);
        for(int i=0;i<5;i++)Cylinder("Membrane tension fastener "+i,d,Membrane+tangent*(-2.4f+i*1.2f)+Vector3.up*.96f,.085f,.12f,p.copper);
        // The secondary shield lives on an edge, not across the gallery travel strip.
        Vector3 shield=new Vector3(5.40f,Height+1.05f,2.78f);
        var sill=Box("Gallery shield fixed sill",s,new Vector3(shield.x,Height+.30f,shield.z),new Vector3(3,.6f,.42f),p.dark);
        sill.transform.rotation=Quaternion.Euler(0,MembraneYaw,0);
        Frangible("Gallery perforable ceramic shield",m,shield+Vector3.up*.12f,new Vector3(2.8f,1.65f,.24f),MembraneYaw,p.ceramic,p.interior,18,Anchor.Bottom);
        var inset=Box("Ground crossing inset",d,new Vector3(3.1f,.034f,2.5f),new Vector3(7.4f,.035f,3),p.worn,false);inset.transform.rotation=Quaternion.Euler(0,MembraneYaw,0);
        foreach(int sign in new[]{-1,1})
        {
            var stripe=Box("Crossing edge "+sign,d,new Vector3(3.1f,.055f,2.5f)+tangent*(sign*3.6f),new Vector3(.12f,.025f,2.9f),p.copper,false);
            stripe.transform.rotation=Quaternion.Euler(0,MembraneYaw,0);
        }
    }

    static void ArrivalAndExit(Transform s, Transform d, Transform m, Transform positions, FoundryArenaDirector director, Palette p)
    {
        Box("Arrival foundation",s,new Vector3(-4,-.35f,-18.5f),new Vector3(6,.7f,7),p.worn);
        Box("Arrival west wall",s,new Vector3(-7.25f,2.4f,-18.5f),new Vector3(.5f,4.8f,7),p.dark);
        Box("Arrival east wall",s,new Vector3(-.75f,2.4f,-18.5f),new Vector3(.5f,4.8f,7),p.dark);
        Box("Arrival back wall",s,new Vector3(-4,2.4f,-22.25f),new Vector3(6.5f,4.8f,.5f),p.jade);
        Box("Arrival roof",d,new Vector3(-4,4.8f,-18.5f),new Vector3(6.5f,.3f,7.5f),p.dark,false);
        DoorFrame(d,new Vector3(-4,0,-15.08f),6,3.6f,p,false);
        Sign("CÁMARA 07",d,new Vector3(-4,3.9f,-15.45f),180,1.4f,new Vector2(5,.7f),p.ceramic.color);
        director.arrival=Marker("Player arrival - oblique composition",positions,new Vector3(-4,1.05f,-16.8f));
        director.arrival.rotation=Quaternion.LookRotation(new Vector3(7,0,19.8f));
        director.encounterGate=Marker("Interior crossing - reinforcement threshold",positions,new Vector3(-1,1.05f,-3));
        // Everything in the cradle is parented to the objective so collection clears it together.
        var cradle=Bevel("Carbine transfer cradle",m,new Vector3(-2.25f,3.4f,8.05f),new Vector3(.85f,1.2f,.8f),.1f,p.dark,true,0);
        director.corePickup=cradle.transform;cradle.AddComponent<FoundryObjective>();
        Box("Cradle indicator",cradle.transform,new Vector3(-2.25f,4.02f,8.05f),new Vector3(.72f,.12f,.65f),p.mint,false);
        Box("Cradle ceramic face",cradle.transform,new Vector3(-2.25f,3.5f,7.62f),new Vector3(.64f,.65f,.10f),p.ceramic,false);
        // A readable firearm silhouette communicates the reward without an explanatory poster.
        var gun=Child("Carbine in transfer cradle",cradle.transform);gun.position=new Vector3(-2.25f,4.17f,8.05f);
        Box("Carbine receiver",gun,gun.position,new Vector3(1.05f,.20f,.24f),p.ceramic,false);
        Box("Carbine barrel",gun,gun.position+Vector3.right*.68f,new Vector3(.55f,.085f,.085f),p.dark,false);
        Box("Carbine stock",gun,gun.position+Vector3.left*.65f-Vector3.up*.05f,new Vector3(.30f,.25f,.16f),p.dark,false);
        var grip=Box("Carbine grip",gun,gun.position+new Vector3(-.18f,-.23f,0),new Vector3(.16f,.28f,.15f),p.copper,false);grip.transform.rotation=Quaternion.Euler(0,0,-18);
        Box("Carbine magazine",gun,gun.position+new Vector3(.16f,-.23f,0),new Vector3(.17f,.30f,.16f),p.dark,false);
        Box("Extraction foundation",s,new Vector3(6,-.35f,18),new Vector3(6,.7f,6),p.worn);
        Box("Extraction west wall",s,new Vector3(2.75f,2.5f,18),new Vector3(.5f,5,6),p.dark);
        Box("Extraction east wall",s,new Vector3(9.25f,2.5f,18),new Vector3(.5f,5,6),p.dark);
        Box("Extraction back wall",s,new Vector3(6,2.5f,21.25f),new Vector3(6.5f,5,.5f),p.jade);
        Box("Extraction roof",d,new Vector3(6,5,18),new Vector3(6.5f,.3f,6.5f),p.dark,false);
        DoorFrame(d,new Vector3(6,0,14.96f),6,3.6f,p,true);
        director.exitDoorLeft=Door("Extraction door left",m,new Vector3(4.51f,1.65f,15.12f),p);
        director.exitDoorRight=Door("Extraction door right",m,new Vector3(7.49f,1.65f,15.12f),p);
        Sign("SALIDA  ↑",d,new Vector3(6,4.4f,14.75f),0,1.65f,new Vector2(5.3f,.7f),p.ceramic.color);
        var exit=Bevel("Extraction terminal",m,new Vector3(6,.85f,19.3f),new Vector3(1.3f,1.7f,.8f),.12f,p.dark,true,0);
        exit.AddComponent<FoundryObjective>().isExit=true;director.exitPoint=exit.transform;
        Box("Extraction terminal indicator",exit.transform,new Vector3(6,1.76f,19.3f),new Vector3(1.15f,.12f,.65f),p.mint,false);
        foreach(float x in new[]{3.6f,8.4f})Box("Extraction floor rail "+x,d,new Vector3(x,.04f,18),new Vector3(.13f,.06f,5),p.mint,false);
    }

    static Transform Door(string name, Transform parent, Vector3 position, Palette p)
    {
        var door=Box(name,parent,position,new Vector3(2.98f,3.3f,.3f),p.jade,true,0);
        var obstacle=door.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.size=new Vector3(1,1,1.333f);
        obstacle.carving=true;obstacle.carveOnlyStationary=false;obstacle.enabled=false;
        Box("Door ceramic panel",door.transform,position+new Vector3(0,.1f,-.18f),new Vector3(2,2.4f,.08f),p.ceramic,false);
        Box("Door status light",door.transform,position+new Vector3(0,1.2f,-.24f),new Vector3(1.7f,.09f,.06f),p.mint,false);
        return door.transform;
    }

    static void EncounterPositions(Transform root, FoundryArenaDirector director)
    {
        director.sentrySpawns=new[]{Marker("Sentry south exchange",root,new Vector3(-2,0,-5)),Marker("Sentry north east exchange",root,new Vector3(8,0,8))};
        director.meleeSpawns=new[]{Marker("Melee west organ approach",root,new Vector3(-11,0,-6)),Marker("Melee east below gallery",root,new Vector3(11,0,-1))};
        director.reinforcementSpawns=new[]{Marker("Reinforcement east portal",root,new Vector3(11.5f,0,9)),Marker("Reinforcement north portal",root,new Vector3(-10,0,11))};
        director.galleryPositions=new[]{Marker("Gallery south position",root,new Vector3(8.8f,Height,-3.8f)),Marker("Gallery middle position",root,new Vector3(4,Height,1.5f)),Marker("Gallery north position",root,new Vector3(-.5f,Height,6.8f))};
        director.sniperSpawn=Marker("Needle gallery entrance",root,director.galleryPositions[0].position);director.bruteSpawn=Marker("Ram north intake",root,new Vector3(-10,0,11));
        director.sentrySectors=new[]{
            Sector("South ranged sector",root,new Bounds(new Vector3(-3,1.3f,-7),new Vector3(11,3,8)),new Vector3(-7,0,-7),new Vector3(-2,0,-5),new Vector3(1.5f,0,-8)),
            Sector("North east ranged sector",root,new Bounds(new Vector3(6.5f,1.3f,9.3f),new Vector3(11,3,8)),new Vector3(8,0,8),new Vector3(10.5f,0,11.5f),new Vector3(3.5f,0,11))};
        director.sniperSector=Sector("Gallery ranged sector",root,new Bounds(new Vector3(4,4.1f,1.5f),new Vector3(15,3,17)),director.galleryPositions[0].position,director.galleryPositions[1].position,director.galleryPositions[2].position);
        foreach(var point in director.sentrySpawns)point.rotation=Quaternion.Euler(0,180,0);
        foreach(var point in director.meleeSpawns)point.rotation=Quaternion.Euler(0,180,0);
        foreach(var point in director.reinforcementSpawns)point.rotation=Quaternion.Euler(0,180,0);
        director.sniperSpawn.rotation=director.bruteSpawn.rotation=Quaternion.Euler(0,180,0);
    }

    static FoundryCombatSector Sector(string name, Transform parent, Bounds bounds, params Vector3[] points)
    {
        var sector=Child(name,parent).gameObject.AddComponent<FoundryCombatSector>();sector.bounds=bounds;sector.positions=new Transform[points.Length];
        for(int i=0;i<points.Length;i++)sector.positions[i]=Marker(name+" position "+i,sector.transform,points[i]);return sector;
    }

    static void AuditMarkers(Transform world, FoundryArenaDirector director)
    {
        var root=Child("05 Audit landmarks - not rendered",world);Vector3 normal=Quaternion.Euler(0,MembraneYaw,0)*Vector3.forward;
        Vector3 near=new Vector3(Membrane.x,1.05f,Membrane.z)-normal*2.4f,far=new Vector3(Membrane.x,1.05f,Membrane.z)+normal*2.4f;
        Marker("QA.Start",root,director.arrival.position).rotation=director.arrival.rotation;
        Marker("QA.SouthRampFoot",root,SouthRampBase + Vector3.back * .8f + Vector3.up * 1.05f);
        Marker("QA.NorthRampFoot",root,NorthRampBase + Vector3.up * 1.05f);
        Marker("QA.Low",root,new Vector3(5.2f,1.05f,-4.5f));Marker("QA.High",root,new Vector3(4,Height+1.05f,1.5f));
        Marker("QA.BreachNear",root,near);Marker("QA.BreachFar",root,far);Marker("QA.Core",root,new Vector3(-.7f,Height+1.05f,7.5f));
        Marker("QA.ExitApproach",root,new Vector3(6,1.05f,12.5f));
        Marker("QA.Exit",root,new Vector3(6,1.05f,18.1f));Marker("QA.Extraction",root,new Vector3(6,1.05f,18.1f));
        Marker("QA.Arrival",root,director.arrival.position).rotation=director.arrival.rotation;
        Marker("QA.CombatCenter",root,new Vector3(5.2f,1.05f,-.5f));Marker("QA.BreachSouth",root,near);Marker("QA.BreachNorth",root,far);
        Marker("QA.ExposureSouth",root,near+Vector3.up*.35f);Marker("QA.ExposureNorth",root,far+Vector3.up*.35f);
        director.auditLoopPoints=new[]{Marker("QA.Orbit.0",root,new Vector3(-1.3f,1.05f,-5.5f)),Marker("QA.Orbit.1",root,new Vector3(8.6f,1.05f,5.5f)),Marker("QA.Orbit.2",root,new Vector3(2,1.05f,11.5f))};
        Marker("QA.Orbit.3",root,new Vector3(-.6f,1.05f,6.1f));Marker("QA.FirstDecision",root,new Vector3(-4.5f,1.05f,-10));
    }

    static void Lighting(Transform world)
    {
        var sun=Child("Chamber key light",world).gameObject.AddComponent<Light>();sun.type=LightType.Directional;
        sun.transform.rotation=Quaternion.Euler(62,-34,0);sun.color=new Color(.91f,.98f,.93f);sun.intensity=1.65f;sun.shadows=LightShadows.Soft;
        LocalLight("Organ warm spill",world,new Vector3(-6.5f,3,-4),new Color(1,.59f,.26f),8,2.4f);
        LocalLight("Gallery soft fill",world,new Vector3(5,6.8f,1),new Color(.65f,.87f,.79f),13,3.5f);
        LocalLight("Extraction fill",world,new Vector3(6,5.8f,12),new Color(.69f,.90f,.77f),10,3);
        LocalLight("Arrival fill",world,new Vector3(-4,3.8f,-18),new Color(.72f,.82f,.79f),7,2);
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.32f,.40f,.36f);
        RenderSettings.ambientEquatorColor=new Color(.20f,.25f,.24f);RenderSettings.ambientGroundColor=new Color(.12f,.16f,.17f);RenderSettings.reflectionIntensity=.6f;
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=new Color(.11f,.18f,.17f);RenderSettings.fogStartDistance=30;RenderSettings.fogEndDistance=75;
    }

    static void LocalLight(string name, Transform parent, Vector3 at, Color color, float range, float intensity)
    {var l=Child(name,parent).gameObject.AddComponent<Light>();l.transform.position=at;l.type=LightType.Point;l.color=color;l.range=range;l.intensity=intensity;l.shadows=LightShadows.None;}

    static void DoorFrame(Transform parent, Vector3 center, float width, float height, Palette p, bool exit)
    {
        foreach(int sign in new[]{-1,1})
        {
            Bevel("Door jamb "+center+" "+sign,parent,center+new Vector3(sign*(width*.5f+.08f),height*.5f,0),new Vector3(.44f,height+.3f,.65f),.09f,p.ceramic,false);
            Box("Door status rail "+center+" "+sign,parent,center+new Vector3(sign*(width*.5f-.15f),height*.5f,-.35f),new Vector3(.10f,height-.35f,.08f),exit?p.mint:p.warm,false);
        }
        Bevel("Door frame head "+center,parent,center+Vector3.up*(height+.12f),new Vector3(width+.6f,.55f,.7f),.1f,p.ceramic,false);
    }

    static void Portal(Transform parent, Vector3 position, float yaw, string number, Palette p)
    {
        var portal=Child("Reinforcement intake "+number,parent);portal.position=position;portal.rotation=Quaternion.Euler(0,yaw,0);
        LocalBox("Intake recess",portal,new Vector3(0,1.8f,0),new Vector3(3.8f,3.6f,.13f),p.dark);
        foreach(int side in new[]{-1,1})
        {
            LocalBox("Intake ceramic side "+side,portal,new Vector3(side*1.95f,1.8f,-.08f),new Vector3(.28f,3.9f,.25f),p.ceramic);
            LocalBox("Intake status "+side,portal,new Vector3(side*1.66f,2.2f,-.13f),new Vector3(.10f,1.8f,.12f),p.warm);
        }
        LocalBox("Intake header",portal,new Vector3(0,3.7f,-.08f),new Vector3(4.15f,.3f,.28f),p.copper);
        for(int i=0;i<4;i++)LocalBox("Intake shutter "+i,portal,new Vector3(0,.45f+i*.78f,-.1f),new Vector3(3.25f,.65f,.12f),p.jade);
        Sign(number,portal,position-portal.forward*.23f+Vector3.up*4.2f,yaw,1.5f,new Vector2(2,.7f),p.ceramic.color);
    }

    static FoundryStructure Frangible(string name, Transform parent, Vector3 position, Vector3 size, float yaw, Material outside, Material inside, int chunks, Anchor anchors)
    {
        var holder=Child(name,parent);holder.position=position;holder.rotation=Quaternion.Euler(0,yaw,0);
        var state=holder.gameObject.AddComponent<FoundryStructure>();state.structuralHealth=72;
        var intact=Box("Prepared Voronoi source",holder,position,size,outside,true,0);intact.transform.rotation=holder.rotation;
        var source=intact.AddComponent<FractureThis>();var data=new SerializedObject(source);
        data.FindProperty("chunks").intValue=chunks;data.FindProperty("density").floatValue=4;data.FindProperty("internalStrength").floatValue=40;
        data.FindProperty("anchor").intValue=(int)anchors;data.FindProperty("insideMaterial").objectReferenceValue=inside;data.FindProperty("outsideMaterial").objectReferenceValue=outside;
        data.ApplyModifiedPropertiesWithoutUndo();state.source=source;return state;
    }

    static void Deck(string name, Transform parent, Vector3 top, Vector2 dimensions, Material material)
    {Box(name,parent,top-Vector3.up*Thickness*.5f,new Vector3(dimensions.x,Thickness,dimensions.y),material);}
    static void Ramp(string name, Transform parent, Vector3 from, Vector3 to, float width, Material material)
    {
        var delta=to-from;var rotation=Quaternion.LookRotation(delta.normalized,Vector3.up);
        var ramp=Box(name,parent,(from+to)*.5f-rotation*Vector3.up*Thickness*.5f,new Vector3(width,Thickness,delta.magnitude+.14f),material);ramp.transform.rotation=rotation;
    }
    static void Beam(string name, Transform parent, Vector3 from, Vector3 to, float width, float height, Material material)
    {var beam=Box(name,parent,(from+to)*.5f,new Vector3(width,height,Vector3.Distance(from,to)),material,false);beam.transform.rotation=Quaternion.LookRotation((to-from).normalized,Vector3.up);}
    static void Cylinder(string name, Transform parent, Vector3 center, float radius, float height, Material material)
    {Tube(name,parent,center-Vector3.up*height*.5f,center+Vector3.up*height*.5f,radius,material);}
    static void Tube(string name, Transform parent, Vector3 from, Vector3 to, float radius, Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.position=(from+to)*.5f;
        go.transform.rotation=Quaternion.FromToRotation(Vector3.up,(to-from).normalized);go.transform.localScale=new Vector3(radius*2,Vector3.Distance(from,to)*.5f,radius*2);
        go.transform.SetParent(parent,true);go.layer=2;go.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(go.GetComponent<Collider>());GameObjectUtility.SetStaticEditorFlags(go,0);
    }
    static void Pipe(string name, Transform parent, Vector3[] route, float radius, Material material)
    {for(int i=1;i<route.Length;i++)Tube(name+" segment "+i,parent,route[i-1],route[i],radius,material);}

    static void Leaf(string name, Transform parent, Vector3 start, Vector3 end, float width, Material material)
    {
        var side=Vector3.Cross((end-start).normalized,Vector3.up).normalized*width*.5f;
        var ridge=Vector3.Lerp(start,end,.48f)+Vector3.up*.22f;var left=Vector3.Lerp(start,end,.55f)-side;var right=Vector3.Lerp(start,end,.55f)+side;var bottom=ridge-Vector3.up*.28f;
        var v=new List<Vector3>();var t=new List<int>();
        Tri(v,t,start,left,ridge);Tri(v,t,left,end,ridge);Tri(v,t,end,right,ridge);Tri(v,t,right,start,ridge);
        Tri(v,t,left,start,bottom);Tri(v,t,end,left,bottom);Tri(v,t,right,end,bottom);Tri(v,t,start,right,bottom);
        MeshObject(name,parent,v,t,material);Tube(name+" midrib",parent,start+Vector3.up*.03f,end+Vector3.up*.03f,.035f,material);
    }
    static GameObject Bevel(string name, Transform parent, Vector3 center, Vector3 size, float bevel, Material material, bool solid, int layer=3)
    {
        float x=size.x*.5f,z=size.z*.5f,h=size.y*.5f,b=Mathf.Min(bevel,Mathf.Min(x,z)*.4f);
        var ring=new[]{new Vector3(-x+b,0,-z),new Vector3(x-b,0,-z),new Vector3(x,0,-z+b),new Vector3(x,0,z-b),new Vector3(x-b,0,z),new Vector3(-x+b,0,z),new Vector3(-x,0,z-b),new Vector3(-x,0,-z+b)};
        var v=new List<Vector3>();var t=new List<int>();
        for(int i=0;i<ring.Length;i++)
        {
            int j=(i+1)%ring.Length;var a=ring[i]-Vector3.up*h;var d=ring[i]+Vector3.up*h;var c=ring[j]+Vector3.up*h;var e=ring[j]-Vector3.up*h;
            Tri(v,t,a,d,c);Tri(v,t,a,c,e);Tri(v,t,Vector3.up*h,c,d);Tri(v,t,-Vector3.up*h,a,e);
        }
        var go=MeshObject(name,parent,v,t,material);go.transform.position=center;go.layer=solid?layer:2;if(solid)go.AddComponent<BoxCollider>().size=size;return go;
    }
    static void Tri(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c)
    {int i=v.Count;v.Add(a);v.Add(b);v.Add(c);t.Add(i);t.Add(i+1);t.Add(i+2);}
    static GameObject MeshObject(string name, Transform parent, List<Vector3> vertices, List<int> triangles, Material material)
    {
        var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        string file=name;foreach(char invalid in Path.GetInvalidFileNameChars())file=file.Replace(invalid,'_');string path=ArtPath+"/Geometry/"+file+".asset";
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing!=null){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;EditorUtility.SetDirty(mesh);}else AssetDatabase.CreateAsset(mesh,path);
        var go=Child(name,parent).gameObject;go.layer=2;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;return go;
    }
    static void LocalBox(string name, Transform parent, Vector3 local, Vector3 size, Material material)
    {var go=Box(name,parent,parent.TransformPoint(local),size,material,false);go.transform.rotation=parent.rotation;}
    static Material Mat(string name, Color color, float smoothness, float metallic, Color emission=default)
    {
        string path=ArtPath+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new InvalidOperationException("URP Lit unavailable.");mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}
        mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",smoothness);mat.SetFloat("_Metallic",metallic);mat.SetColor("_EmissionColor",emission);
        if(emission.maxColorComponent>0)mat.EnableKeyword("_EMISSION");else mat.DisableKeyword("_EMISSION");mat.enableInstancing=true;EditorUtility.SetDirty(mat);return mat;
    }
    static Transform Child(string name, Transform parent)
    {var child=new GameObject(name).transform;child.SetParent(parent,false);child.gameObject.layer=0;GameObjectUtility.SetStaticEditorFlags(child.gameObject,0);return child;}
    // Geometry takes world positions and physical dimensions, regardless of its parent.
    static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material, bool solid=true, int layer=3)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=position;go.transform.localScale=size;go.transform.SetParent(parent,true);
        go.layer=solid?layer:2;go.GetComponent<Renderer>().sharedMaterial=material;GameObjectUtility.SetStaticEditorFlags(go,0);if(!solid)Object.DestroyImmediate(go.GetComponent<Collider>());return go;
    }
    static TextMeshPro Sign(string text, Transform parent, Vector3 position, float yaw, float size, Vector2 bounds, Color color)
    {
        var label=Child("Sign - "+text,parent).gameObject.AddComponent<TextMeshPro>();label.transform.position=position;label.transform.rotation=Quaternion.Euler(0,yaw,0);label.gameObject.layer=2;
        label.text=text;label.fontSize=size;label.color=color;label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=bounds;return label;
    }
    static Transform Marker(string name, Transform parent, Vector3 position)
    {var point=Child(name,parent);point.position=position;point.rotation=Quaternion.identity;return point;}
    static NormalEnemyStateMachine EnemyPrefab(string path)
    {var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);var enemy=asset!=null?asset.GetComponent<NormalEnemyStateMachine>():null;if(enemy==null)throw new InvalidOperationException("Missing robot prefab: "+path);return enemy;}
    static void BakeNavigation(NavMeshSurface surface, Transform sceneRoot)
    {
        // GetBuildSettings returns a copy. Restrict this room's topology to a step the
        // actual controller can clear; the project's Humanoid settings remain untouched.
        var settings=surface.GetBuildSettings();
        settings.agentClimb=.18f; settings.agentHeight=2.05f; settings.agentRadius=.45f;
        settings.overrideVoxelSize=true; settings.voxelSize=.08f;
        var sources=new List<NavMeshBuildSource>();
        var markups=new List<NavMeshBuildMarkup>();
        UnityEngine.AI.NavMeshBuilder.CollectSources(sceneRoot,1<<3,NavMeshCollectGeometry.PhysicsColliders,
            0,false,markups,false,sources);
        if(sources.Count==0)throw new InvalidOperationException("No permanent geometry collected for transfer chamber navigation.");
        // Bounds include both vestibules. Root is identity; geometry is collected in world space.
        var bounds=new Bounds(new Vector3(0,4,-.5f),new Vector3(32,12,47));
        var data=UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(settings,sources,bounds,Vector3.zero,Quaternion.identity);
        if(data==null)throw new InvalidOperationException("Local transfer chamber navigation build returned no data.");
        data.name="Transfer chamber - local 0.18m climb";
        surface.RemoveData(); surface.navMeshData=data; surface.AddData();
    }
    static void AssertNavigation(Vector3 arrivalGround)
    {
        string[] labels={"arrival","south ramp foot","south landing","gallery centre","carbine approach","north ramp foot"};
        Vector3[] expected={arrivalGround,SouthRampBase+Vector3.back*.8f,South,new Vector3(4,Height,1.5f),new Vector3(-.7f,Height,7.5f),NorthRampBase};
        var sampled=new Vector3[expected.Length];
        for(int i=0;i<expected.Length;i++)
        {
            if(!NavMesh.SamplePosition(expected[i],out var hit,.35f,NavMesh.AllAreas))
                throw new InvalidOperationException("TRASVASE NAVIGATION REJECTED: "+labels[i]+" has no surface within 0.35m of "+expected[i]+".");
            sampled[i]=hit.position;
            // Sampling must not quietly choose the ground under an elevated waypoint.
            if(Mathf.Abs(sampled[i].y-expected[i].y)>.18f)
                throw new InvalidOperationException("TRASVASE NAVIGATION REJECTED: "+labels[i]+" sampled wrong elevation "+sampled[i]+" instead of "+expected[i]+".");
        }
        for(int i=1;i<sampled.Length;i++)
        {
            var path=new NavMeshPath();
            if(!NavMesh.CalculatePath(sampled[i-1],sampled[i],NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)
                throw new InvalidOperationException("TRASVASE NAVIGATION REJECTED: "+labels[i-1]+" -> "+labels[i]+" is "+path.status+". Build stopped before BuildPlayer.");
        }
        Debug.Log("TRASVASE NAVIGATION: arrival -> ramp foot -> south landing -> gallery -> carbine -> north ramp foot complete with local 0.18m climb. Physical controller verification remains required.");
    }
    static void SaveNavigation(NavMeshSurface surface)
    {
        string path=ArtPath+"/Transfer Chamber Navigation.asset";var existing=AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
        if(existing!=null){var generated=surface.navMeshData;surface.RemoveData();EditorUtility.CopySerialized(generated,existing);Object.DestroyImmediate(generated);surface.navMeshData=existing;EditorUtility.SetDirty(existing);}
        else AssetDatabase.CreateAsset(surface.navMeshData,path);surface.RemoveData();surface.AddData();
    }
    [MenuItem("Tools/HELIX/Foundry/Build Windows")]
    public static void Build()
    {
        Create();Directory.CreateDirectory(Path.GetDirectoryName(BuildPath));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath,CorePath},locationPathName=BuildPath,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Transfer chamber build: "+report.summary.result);Debug.Log("TRASVASE BUILD SUCCEEDED: "+BuildPath);
    }
}
