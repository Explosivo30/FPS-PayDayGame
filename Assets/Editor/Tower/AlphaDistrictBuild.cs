using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

/// <summary>Six connected districts, two shared loops and two persistent topology changes.</summary>
public static class AlphaDistrictBuild
{
    public const string ScenePath = "Assets/Scenes/PuertoResonancia.unity";
    public const string ArtPath = "Assets/Art/AlphaDistrict";
    public const string BuildPath = "Builds/PuertoResonanciaAlpha/PuertoResonancia.exe";
    const string CorePath = "Assets/Scenes/TowerCore.unity";
    const float GalleryY = 4;
    static readonly Color SignColor = new Color(.91f,.88f,.73f);
    static readonly Vector3[] Centers = {
        new Vector3(0,0,-40), new Vector3(-42,0,-12), new Vector3(0,0,0),
        new Vector3(42,0,-6), new Vector3(-24,0,34), new Vector3(24,0,34)
    };
    static readonly Vector2[] Sizes = {
        new Vector2(32,20),new Vector2(30,30),new Vector2(32,32),
        new Vector2(30,36),new Vector2(32,24),new Vector2(32,24)
    };
    static readonly string[] Names = { "MUELLE", "BOBINAS", "REGULADOR", "INVERNADERO", "TURBINAS", "RAÍCES" };
    static readonly int[][] Neighbors = {
        new[]{1,2,3},new[]{0,2,4},new[]{0,1,3,4},new[]{0,2,5},new[]{1,2,5},new[]{3,4}
    };
    enum Side { North, South, East, West }
    struct Opening { public float at,width,floor; public Opening(float a,float w,float y=0){at=a;width=w;floor=y;} }
    static readonly Dictionary<string,List<Opening>> openings = new Dictionary<string,List<Opening>>();
    static readonly List<Tuple<Vector3,Vector3,string>> navLegs = new List<Tuple<Vector3,Vector3,string>>();
    static readonly Vector3[] Audit = {
        new Vector3(5,0,-38),new Vector3(-40,0,-16),new Vector3(8,0,-8),
        new Vector3(40,0,-10),new Vector3(-26,4,36),new Vector3(22,0,30)
    };
    static Transform world,solid,detail,mechanisms,markers;
    static AlphaDistrictArt.Palette p;
    static AlphaDistrictDirector director;

    [MenuItem("Tools/HELIX/Alpha/Create Puerto Resonancia")]
    public static void Create()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Create the district outside Play Mode.");
        Directory.CreateDirectory(ArtPath+"/Geometry");AssetDatabase.Refresh();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        world=new GameObject("PUERTO DE RESONANCIA - six connected districts").transform;
        solid=Child("01 Permanent walkable architecture",world);
        detail=Child("02 Silhouettes and visual assemblies",world);
        mechanisms=Child("03 Persistent topology mechanisms",world);
        markers=Child("04 Objectives, entries and audit landmarks",world);
        director=world.gameObject.AddComponent<AlphaDistrictDirector>();
        p=AlphaDistrictArt.CreateMaterials();AlphaDistrictArt.CreateSetup(world);
        openings.Clear();navLegs.Clear();
        ConfigureOpenings();CreateDistricts();ConnectDistricts();
        Dock();Workshop();Regulator();Greenhouse();Turbines();RootDepot();
        CreateCollapse();CreateContainment();Objectives();AuditMarkers();
        director.rangedPrefab=Enemy("Assets/Art/Laboratory/LaboratorySentinel.prefab");
        director.meleePrefab=Enemy("Assets/Art/MeleeRobot/SecurityBrawler.prefab");
        Physics.SyncTransforms();
        var surface=Child("05 District navigation - local controller dimensions",world).gameObject.AddComponent<NavMeshSurface>();
        surface.layerMask=1<<3;surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;
        surface.overrideVoxelSize=true;surface.voxelSize=.08f;
        Bake(surface);AssertNavigation();SaveNavigation(surface);
        // Prepared collision states are baked together only where their walkable surfaces
        // are spatially disjoint. Runtime carving controls which connection is available.
        foreach(var c in director.collapse.finalRamp.GetComponentsInChildren<Collider>())c.enabled=false;
        director.collapse.rampGate.enabled=true;
        director.collapse.oldNestGate.enabled=false;
        director.containment.obstacle.enabled=true;
        EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
        Debug.Log("PUERTO RESONANCIA CREATED: A-B-E-C-A and A-C-E-F-D-A; B/F objectives, C radio, A extraction; prepared gantry and C-F breach. Previous maps untouched.");
    }

    static void ConfigureOpenings()
    {
        Door(0,Side.North,0,9);Door(0,Side.West,-38,8);Door(0,Side.East,-38,8);
        Door(1,Side.South,-38,8);Door(1,Side.East,-10,8);Door(1,Side.North,-35,8);
        Door(2,Side.South,0,9);Door(2,Side.West,-10,8);Door(2,Side.West,12,7);
        Door(2,Side.East,-6,8);Door(2,Side.North,-12,7);Door(2,Side.North,12,7);
        Door(3,Side.South,38,8);Door(3,Side.West,-6,8);Door(3,Side.North,34,8);
        Door(4,Side.South,-35,8,4);Door(4,Side.South,-24,7);Door(4,Side.South,-12,7,4);Door(4,Side.East,36,8,4);
        Door(5,Side.South,34,8);Door(5,Side.South,12,7);Door(5,Side.West,36,8);
    }
    static string Key(int index,Side side)=>index+"/"+side;
    static void Door(int index,Side side,float at,float width,float floor=0)
    {
        string key=Key(index,side);if(!openings.TryGetValue(key,out var list)){list=new List<Opening>();openings[key]=list;}
        list.Add(new Opening(at,width,floor));
    }

    static void CreateDistricts()
    {
        director.zones=new AlphaDistrictZone[6];
        for(int i=0;i<6;i++)
        {
            var center=Centers[i];var size=Sizes[i];float upper=i==4?4:0;
            var zone=Child(((char)('A'+i))+" - "+Names[i],markers).gameObject.AddComponent<AlphaDistrictZone>();
            zone.index=i;zone.label=Names[i];zone.neighbors=Neighbors[i];
            zone.bounds=new Bounds(center+Vector3.up*6,new Vector3(size.x+16,22,size.y+16));
            director.zones[i]=zone;
            Box(Names[i]+" ground foundation",solid,center-Vector3.up*.35f,new Vector3(size.x,.7f,size.y),p.floor);
            if(i==4)
            {
                Deck("Turbine north gallery",new Vector3(-24,4,38),new Vector2(32,16));
                Deck("Turbine west gallery wing",new Vector3(-35,4,26),new Vector2(10,8));
                Deck("Turbine east gallery wing",new Vector3(-13,4,26),new Vector2(10,8));
                Box("Turbine north gallery substructure",solid,new Vector3(-24,1.8f,38),new Vector3(32,3.6f,16),p.jade);
                Box("Turbine west gallery substructure",solid,new Vector3(-35,1.8f,26),new Vector3(10,3.6f,8),p.jade);
                Box("Turbine east gallery substructure",solid,new Vector3(-13,1.8f,26),new Vector3(10,3.6f,8),p.jade);
                Staircase("CE permanent staircase",new Vector3(-24,0,20),new Vector3(-24,4,30),6,26);
                navLegs.Add(Tuple.Create(new Vector3(-24,0,19.3f),new Vector3(-24,4,30.5f),"CE actual stair ascent"));
            }
            // These are operational entry bays, with a dogleg and a solid baffle. Their
            // origins are connected to the floor but hidden from the central room view.
            var entries=new List<Transform>();
            if(i==0){entries.Add(Intake(i,Side.South,-10,0));entries.Add(Intake(i,Side.South,10,0));}
            if(i==1){entries.Add(Intake(i,Side.West,-16,0));entries.Add(Intake(i,Side.North,-50,0));entries.Add(Intake(i,Side.South,-50,0));}
            if(i==2){entries.Add(Intake(i,Side.East,10,0));entries.Add(Intake(i,Side.North,0,0));}
            if(i==3){entries.Add(Intake(i,Side.East,-14,0));entries.Add(Intake(i,Side.East,5,0));entries.Add(Intake(i,Side.North,48,0));}
            if(i==4){entries.Add(Intake(i,Side.West,27,4));entries.Add(Intake(i,Side.North,-32,4));entries.Add(Intake(i,Side.North,-16,4));}
            if(i==5){entries.Add(Intake(i,Side.North,14,0));entries.Add(Intake(i,Side.North,34,0));entries.Add(Intake(i,Side.East,31,0));}
            zone.entries=entries.ToArray();
            var positions=new[]{Audit[i],center+new Vector3(-7,upper,5),center+new Vector3(7,upper,5)};
            if(i==4)positions=new[]{new Vector3(-34,4,34),new Vector3(-24,4,39),new Vector3(-14,4,34)};
            if(i==5)positions=new[]{Audit[i],new Vector3(17,0,37),new Vector3(34,0,28)};
            zone.rangedSector=Sector(Names[i]+" defender sector",zone.bounds,positions);
            // The dock is a quay with low retaining limits, rather than another closed
            // factory room. The regulator's skyline remains visible above its colonnade.
            float wallHeight=i==0?3.2f:i==4?12:8;
            foreach(Side side in Enum.GetValues(typeof(Side)))Wall(i,side,wallHeight);
            LargeSign(((char)('A'+i))+"  "+Names[i],detail,center+new Vector3(0,upper+6,-size.y*.5f+.42f),180,3.3f,new Vector2(size.x-5,1.3f),SignColor);
            // Broad paving bands connect actual openings; no painted route solves navigation.
            for(int stripe=0;stripe<3;stripe++)
                Box(Names[i]+" deck expansion joint "+stripe,detail,center+new Vector3(-size.x*.5f+(stripe+1)*size.x/4,.025f,0),new Vector3(.07f,.03f,size.y),p.ink,false);
        }
    }

    static void Wall(int index,Side side,float height)
    {
        var c=Centers[index];var size=Sizes[index];bool horizontal=side==Side.North||side==Side.South;
        float min=horizontal?c.x-size.x*.5f:c.z-size.y*.5f;
        float max=horizontal?c.x+size.x*.5f:c.z+size.y*.5f;
        float fixedAxis=horizontal?c.z+(side==Side.North?1:-1)*size.y*.5f:c.x+(side==Side.East?1:-1)*size.x*.5f;
        var list=openings.TryGetValue(Key(index,side),out var found)?found.OrderBy(x=>x.at).ToArray():new Opening[0];
        float cursor=min;
        foreach(var opening in list)
        {
            float a=Mathf.Clamp(opening.at-opening.width*.5f,min,max),b=Mathf.Clamp(opening.at+opening.width*.5f,min,max);
            if(a>cursor)WallPiece(index,side,cursor,a,fixedAxis,0,height);
            if(opening.floor>0)WallPiece(index,side,a,b,fixedAxis,0,opening.floor-.03f);
            float head=opening.floor+5.4f;if(head<height)WallPiece(index,side,a,b,fixedAxis,head,height);
            Vector3 mouth=horizontal?new Vector3(opening.at,opening.floor,fixedAxis):new Vector3(fixedAxis,opening.floor,opening.at);
            PortalFrame(Names[index]+" "+side+" "+opening.at,mouth,horizontal?0:90,opening.width,index==0?3.6f:5.4f);
            cursor=Mathf.Max(cursor,b);
        }
        if(cursor<max)WallPiece(index,side,cursor,max,fixedAxis,0,height);
    }
    static void WallPiece(int index,Side side,float from,float to,float fixedAxis,float bottom,float top)
    {
        bool horizontal=side==Side.North||side==Side.South;
        Vector3 at=horizontal?new Vector3((from+to)*.5f,(top+bottom)*.5f,fixedAxis):new Vector3(fixedAxis,(top+bottom)*.5f,(from+to)*.5f);
        Vector3 size=horizontal?new Vector3(to-from,top-bottom,.6f):new Vector3(.6f,top-bottom,to-from);
        var material=index==1?p.ivory:index==3?p.jade:p.wall;
        Box(Names[index]+" structural wall "+side+" "+from,solid,at,size,material);
        if(to-from>3)
        {
            Vector3 ribSize=horizontal?new Vector3(.36f,top-bottom,.82f):new Vector3(.82f,top-bottom,.36f);
            Box(Names[index]+" wall rib "+side+" "+from,detail,at,ribSize,p.copper,false);
        }
    }

    static Transform Intake(int index,Side side,float along,float floor)
    {
        Door(index,side,along,4.6f,floor);
        var c=Centers[index];var size=Sizes[index];Vector3 n=side==Side.North?Vector3.forward:side==Side.South?Vector3.back:side==Side.East?Vector3.right:Vector3.left;
        Vector3 t=Vector3.Cross(Vector3.up,n);
        Vector3 wall=(side==Side.North||side==Side.South)?new Vector3(along,floor,c.z+n.z*size.y*.5f):new Vector3(c.x+n.x*size.x*.5f,floor,along);
        var bay=Child(Names[index]+" hidden service intake "+side+along,solid);
        var rotation=Quaternion.LookRotation(n);
        OrientedBox("Intake floor",bay,wall+n*3.5f-Vector3.up*.25f,new Vector3(8,.5f,7.1f),rotation,p.floor);
        foreach(int sign in new[]{-1,1})OrientedBox("Intake side wall "+sign,bay,wall+n*3.5f+t*(sign*4)+Vector3.up*2.5f,new Vector3(.5f,5,7.5f),rotation,p.ink);
        OrientedBox("Intake back wall",bay,wall+n*7.1f+Vector3.up*2.5f,new Vector3(8.5f,5,.5f),rotation,p.ink);
        OrientedBox("Intake line-of-sight baffle",bay,wall+n*2.7f-t*.5f+Vector3.up*1.5f,new Vector3(3.0f,3,.55f),rotation,p.jade);
        OrientedBox("Intake header signal",detail,wall+n*.08f+Vector3.up*4.7f,new Vector3(3.8f,.16f,.2f),rotation,p.hazard,false);
        var entry=Marker(Names[index]+" entry "+side+along,wall+n*5.5f+t*1.4f);
        entry.rotation=Quaternion.LookRotation(-n);
        navLegs.Add(Tuple.Create(wall-n*1.2f,entry.position,Names[index]+" hidden intake "+side+along));
        return entry;
    }

    static void ConnectDistricts()
    {
        Passage("AB service street west",new Vector3(-16,0,-38),new Vector3(-38,0,-38),7);
        Passage("AB service street north",new Vector3(-38,0,-38),new Vector3(-38,0,-27),7);
        Passage("AC arrival boulevard",new Vector3(0,0,-30),new Vector3(0,0,-16),8);
        Passage("AD greenhouse approach east",new Vector3(16,0,-38),new Vector3(38,0,-38),7);
        Passage("AD greenhouse approach north",new Vector3(38,0,-38),new Vector3(38,0,-24),7);
        Passage("BC workshop cross street",new Vector3(-27,0,-10),new Vector3(-16,0,-10),7);
        Passage("BE service bend",new Vector3(-35,0,3),new Vector3(-35,0,10),6);
        Passage("BE upper approach",new Vector3(-35,0,10),new Vector3(-35,4,22),6);
        Passage("CD coolant street",new Vector3(16,0,-6),new Vector3(27,0,-6),7);
        Passage("CE lower west turn",new Vector3(-16,0,12),new Vector3(-24,0,12),6);
        Passage("CE lower stair approach",new Vector3(-24,0,12),new Vector3(-24,0,20),6);
        Passage("DF root service bend",new Vector3(34,0,12),new Vector3(34,0,22),7);
        Passage("EF turbine descent",new Vector3(-8,4,36),new Vector3(8,0,36),6);
        // This sixth-metre connector is closed by a real frangible diaphragm, not an
        // invisible objective flag. Both C and F retain their original routes around it.
        Passage("CF containment connector",new Vector3(12,0,16),new Vector3(12,0,22),6);
    }
    static void Passage(string name,Vector3 from,Vector3 to,float width)
    {
        Ramp(name+" walking slab",solid,from,to,width,p.floor,true);
        Vector3 flat=Vector3.ProjectOnPlane(to-from,Vector3.up).normalized;
        Vector3 side=Vector3.Cross(Vector3.up,flat);
        for(int sign=-1;sign<=1;sign+=2)
        {
            float trim=Mathf.Min(width*.56f,Vector3.Distance(from,to)*.22f);
            Vector3 along=(to-from).normalized;
            Beam(name+" retaining shell "+sign,solid,from+along*trim+side*(sign*(width*.5f+.12f))+Vector3.up*1.65f,
                to-along*trim+side*(sign*(width*.5f+.12f))+Vector3.up*1.65f,.35f,3.3f,p.ink,true);
            Beam(name+" upper enamel edge "+sign,detail,from+along*trim+side*(sign*(width*.5f+.15f))+Vector3.up*3.35f,
                to-along*trim+side*(sign*(width*.5f+.15f))+Vector3.up*3.35f,.42f,.25f,p.ivory,false);
        }
        navLegs.Add(Tuple.Create(from+(to-from).normalized*.8f,to-(to-from).normalized*.8f,name));
    }

    static void Dock()
    {
        var dock=Child("A mooring cranes and evacuation tram",detail);
        foreach(float x in new[]{-12f,12f})
        {
            Cylinder("Mooring pylon "+x,dock,new Vector3(x,3,-45),1.2f,6,p.copper);
            Box("Mooring enamel cap "+x,dock,new Vector3(x,6.2f,-45),new Vector3(3,.5f,3),p.ivory,false);
            Beam("Dock overhead boom "+x,dock,new Vector3(x,6.2f,-45),new Vector3(x*.35f,9,-43),.55f,.65f,p.ink,false);
        }
        // A walk-in gangway is part of the permanent floor. The vehicle can arrive later
        // without leaving an unbaked wall across the player's extraction route.
        var tram=Child("Extraction tram - walk-in north entrance",detail);
        director.extractionVehicle=tram;
        Box("Tram rear cab",tram,new Vector3(0,1.8f,-48),new Vector3(7,3.6f,3),p.copper,false);
        foreach(int side in new[]{-1,1})Box("Tram side shell "+side,tram,new Vector3(side*3.5f,1.6f,-45.7f),new Vector3(.5f,3.2f,5),p.ivory,false);
        Box("Tram roof visor",tram,new Vector3(0,3.5f,-46),new Vector3(7.6f,.45f,7),p.jade,false);
        Box("Tram cabin backing",tram,new Vector3(0,1.7f,-46.35f),new Vector3(5.8f,2.7f,.12f),p.ink,false);
        foreach(float x in new[]{-2.4f,2.4f})Box("Tram boarding strip "+x,tram,new Vector3(x,.035f,-44.7f),new Vector3(.16f,.05f,3.6f),p.signal,false);
        // All three destinations are readable before opening the map. This is wayfinding
        // mounted on the existing quay gateway; neither the ground nor the graph changes.
        DockDirection("←  BOBINAS",new Vector3(-5.7f,3.45f,-30.9f),new Vector2(6.4f,1.6f),9.0f);
        DockDirection("INVERNADERO  →",new Vector3(5.7f,3.45f,-30.9f),new Vector2(6.4f,1.6f),8.5f);
        DockDirection("PLAZA  ↑",new Vector3(0,4.6f,-30.9f),new Vector2(5.0f,1.6f),10.0f);
        director.arrival=Marker("Operator arrival",new Vector3(0,1.05f,-43));
        director.arrival.rotation=Quaternion.identity;
        director.exitPoint=Marker("Extraction boarding position",new Vector3(0,1.05f,-45));
        LargeSign("RESONANCIA / MUELLE 01",dock,new Vector3(0,7,-49.6f),180,3,new Vector2(24,1.2f),SignColor);
    }
    static void DockDirection(string text,Vector3 center,Vector2 size,float fontSize)
    {
        Box("Quay direction board "+text,detail,center,new Vector3(size.x,size.y,.20f),p.ink,false);
        var label=LargeSign(text,detail,center+Vector3.back*.13f,0,fontSize,new Vector2(size.x-.15f,size.y-.12f),SignColor);
        label.enableAutoSizing=true;
        label.fontSizeMin=fontSize*.35f;label.fontSizeMax=fontSize;
        label.enableWordWrapping=false;
        label.overflowMode=TextOverflowModes.Truncate;
    }
    static void Workshop()
    {
        var shop=Child("B winding workshop - crossed production aisles",detail);
        foreach(var at in new[]{new Vector3(-50,0,-10),new Vector3(-32,0,-2),new Vector3(-48,0,-22)})
        {
            Box("Winding machine fixed foot "+at,solid,at+Vector3.up*.6f,new Vector3(5,1.2f,4),p.ink);
            Cylinder("Copper coil drum "+at,shop,at+Vector3.up*2.3f,1.9f,2.3f,p.copper);
            Cylinder("Coil enamel crown "+at,shop,at+Vector3.up*3.5f,2.15f,.3f,p.ivory);
            Box("Winding control flank "+at,shop,at+new Vector3(2.2f,1.8f,0),new Vector3(.4f,2.4f,2),p.jade,false);
        }
        foreach(float z in new[]{-23f,-12f,0f})
        {
            Beam("Workshop roof frame "+z,shop,new Vector3(-56,8,z),new Vector3(-28,8,z),.55f,.6f,p.copper,false);
            Box("Workshop roof sail "+z,shop,new Vector3(-49,8.4f,z),new Vector3(9,.18f,4.5f),p.ivory,false);
        }
        Frangible("Workshop light shield",new Vector3(-34,1.4f,-15),new Vector3(3.5f,2.8f,.32f),25,8,48,false);
    }
    static void Regulator()
    {
        var machine=Child("C regulator - skyline landmark visible from all approaches",detail);
        Cylinder("Regulator permanent pedestal",solid,new Vector3(-2,.65f,2),4.6f,1.3f,p.ink,true);
        Cylinder("Regulator teal trunk",machine,new Vector3(-2,6,2),2.7f,10,p.jade);
        foreach(float h in new[]{2f,5f,8f,11f})Cylinder("Regulator collar "+h,machine,new Vector3(-2,h,2),3.1f,.45f,p.copper);
        foreach(int side in new[]{-1,1})
        {
            Beam("Regulator upward fork "+side,machine,new Vector3(-2,10,2),new Vector3(-2+side*5,16,2),1.3f,1.0f,p.ivory,false);
            Beam("Regulator live rail "+side,machine,new Vector3(-2+side*.4f,11,1.4f),new Vector3(-2+side*4.6f,15.5f,1.4f),.18f,.14f,p.signal,false);
        }
        for(int i=0;i<4;i++)
        {
            float a=i*Mathf.PI*.5f;
            Pipe("Regulator radial feed "+i,machine,new[]{new Vector3(-2,2,2),new Vector3(-2+Mathf.Cos(a)*6,3,2+Mathf.Sin(a)*6),new Vector3(-2+Mathf.Cos(a)*10,6,2+Mathf.Sin(a)*10)},.22f,p.copper);
        }
        Box("Radio plinth",solid,new Vector3(7,.45f,7),new Vector3(2.5f,.9f,2),p.ink);
        LargeSign("R",machine,new Vector3(-2,9.2f,-.76f),0,8,new Vector2(5,5),SignColor);
    }
    static void Greenhouse()
    {
        var greenhouse=Child("D greenhouse - glass fins and living production beds",detail);
        foreach(var at in new[]{new Vector3(48,0,3),new Vector3(33,0,-16),new Vector3(51,0,-17)})
        {
            Box("Cultivation bed "+at,solid,at+Vector3.up*.45f,new Vector3(5,.9f,5),p.jade);
            Plant(greenhouse,at+Vector3.up*.9f,5.6f,3.2f,p.foliage);
            foreach(int side in new[]{-1,1})
                Beam("Glass collector fin "+at+side,greenhouse,at+new Vector3(side*2.2f,1,0),at+new Vector3(side*1.3f,6.5f,.9f),.45f,1.1f,p.ivory,false);
        }
        foreach(float z in new[]{-21f,-5f,10f})
        {
            Beam("Greenhouse arch west "+z,greenhouse,new Vector3(28,0,z),new Vector3(35,10,z),.45f,.6f,p.copper,false);
            Beam("Greenhouse arch east "+z,greenhouse,new Vector3(56,0,z),new Vector3(49,10,z),.45f,.6f,p.copper,false);
            Beam("Greenhouse ridge "+z,greenhouse,new Vector3(35,10,z),new Vector3(49,10,z),.45f,.6f,p.ivory,false);
        }
        Frangible("Greenhouse perforable service shield",new Vector3(44,1.3f,-1),new Vector3(3.8f,2.6f,.3f),-30,8,48,false);
    }
    static void Turbines()
    {
        var gallery=Child("E turbine gallery - raised production floor",detail);
        foreach(float x in new[]{-35f,-13f})
        {
            Cylinder("Turbine structural shoe "+x,solid,new Vector3(x,4.5f,40),2.6f,1,p.ink,true);
            Cylinder("Turbine vertical casing "+x,gallery,new Vector3(x,7.0f,40),1.9f,4,p.jade);
            Cylinder("Turbine copper rim "+x,gallery,new Vector3(x,9.0f,40),2.4f,.45f,p.copper);
            Pipe("Upper steam main "+x,gallery,new[]{new Vector3(x,9,40),new Vector3(x,12,37),new Vector3(-24,12,37)},.35f,p.ivory);
        }
        foreach(float x in new[]{-39f,-9f})
            Beam("Upper gallery guard "+x,gallery,new Vector3(x,5,31),new Vector3(x,5,44),.14f,.18f,p.copper,false);
        // The open stair slot is framed by rails, never by a slab covering the stair head.
        foreach(float x in new[]{-28f,-20f})Beam("Stair well edge "+x,gallery,new Vector3(x,4.9f,23),new Vector3(x,4.9f,29),.14f,.16f,p.ivory,false);
    }
    static void RootDepot()
    {
        var depot=Child("F root heart - open crown and split low routes",detail);
        Cylinder("Root heart permanent collar",solid,new Vector3(29,.65f,35),4,1.3f,p.ink,true);
        Cylinder("Root heart porcelain bole",depot,new Vector3(29,5,35),1.5f,8.7f,p.ivory);
        for(int i=0;i<6;i++)
        {
            float a=i*Mathf.PI/3;var outward=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
            Pipe("Root heart branch "+i,depot,new[]{new Vector3(29,4,35),new Vector3(29,8,35)+outward*3,new Vector3(29,10,35)+outward*7},.32f,p.copper);
            Leaf("Root heart crown "+i,depot,new Vector3(29,8.4f,35)+outward*2.5f,new Vector3(29,10,35)+outward*8,3.4f,p.foliage);
            // Root conduits stay on their durable collar, outside all navigation strips.
            Tube("Root heart collar feed "+i,depot,new Vector3(29,1.4f,35)+outward*1.5f,new Vector3(29,.9f,35)+outward*3.8f,.24f,p.jade);
        }
        Box("North process bench",solid,new Vector3(17,.5f,41),new Vector3(3,1,2),p.ink);
    }

    static void CreateCollapse()
    {
        var root=Child("Gantry collapse - exchanges lower and upper C-E connections",mechanisms);
        var collapse=root.gameObject.AddComponent<AlphaDistrictCollapse>();director.collapse=collapse;
        Vector3 from=new Vector3(-12,0,16),to=new Vector3(-12,4,22);
        collapse.finalRamp=Ramp("Prepared final gantry collision",root,from,to,6,p.copper,true);
        collapse.finalRamp.GetComponent<Renderer>().enabled=false;
        collapse.movingLoad=Ramp("Falling gantry - final landing pose",root,from,to,6,p.ivory,false).transform;
        // Renderer-only ribs travel with the same object; its walking collision remains in
        // finalRamp and is enabled exactly once after settling.
        foreach(float x in new[]{-14.8f,-9.2f})Beam("Gantry edge spine "+x,collapse.movingLoad,new Vector3(x,.15f,16),new Vector3(x,4.15f,22),.22f,.28f,p.copper,false);
        collapse.initialLoadOffset=Vector3.up*8;
        collapse.oldNest=Ramp("Original elevated nest collision",root,from+Vector3.up*8,to+Vector3.up*8,6,p.ink,true);
        collapse.oldNest.GetComponent<Renderer>().enabled=false;
        collapse.supports=new[] {
            Frangible("Gantry west tension leg",new Vector3(-14.3f,5.1f,19.5f),new Vector3(.95f,10.2f,.95f),0,16,48,false),
            Frangible("Gantry east tension leg",new Vector3(-9.7f,5.1f,19.5f),new Vector3(.95f,10.2f,.95f),0,16,48,false)
        };
        foreach(float x in new[]{-14.3f,-9.7f})
        {
            Box("Gantry anchor footing "+x,solid,new Vector3(x,.3f,19.5f),new Vector3(1.7f,.6f,1.7f),p.ink);
            Box("Gantry tension cap "+x,detail,new Vector3(x,10.2f,19.5f),new Vector3(1.4f,.24f,1.4f),p.hazard,false);
        }
        var gate=Child("Closed gantry maintenance barrier",root);
        Box("Maintenance barrier ceramic shutter",gate,new Vector3(-12,1.8f,16.4f),new Vector3(6.4f,3.6f,.4f),p.destructible,true,0);
        Box("Maintenance barrier warning band",gate,new Vector3(-12,2.8f,16.14f),new Vector3(5.6f,.28f,.08f),p.hazard,false);
        collapse.rampGate=Obstacle(gate,new Vector3(-12,3,19),new Vector3(7,6,8),false);
        var oldGate=Child("Old nest navigation removal",root);
        collapse.oldNestGate=Obstacle(oldGate,new Vector3(-12,11,19),new Vector3(8,8,10),false);
        var blocker=Child("Fallen counterweight - blocks old CE low passage",root);
        Box("Counterweight walking obstruction",blocker,new Vector3(-24,1.5f,15),new Vector3(6.4f,3,2.2f),p.jade,true,0);
        Box("Counterweight copper reinforcement",blocker,new Vector3(-24,3.1f,15),new Vector3(6.8f,.25f,2.5f),p.copper,false);
        Obstacle(blocker,new Vector3(-24,1.6f,15),new Vector3(6.6f,3.2f,2.5f),true);
        collapse.lowBlocker=blocker.gameObject;blocker.gameObject.SetActive(false);
        collapse.impactBounds=new Bounds(new Vector3(-18,7.5f,18.5f),new Vector3(22,15,11));
        collapse.nestBounds=new Bounds(new Vector3(-12,11,19),new Vector3(8,10,10));
        var warning=Child("Gantry sweep warning footprint",root);
        foreach(float x in new[]{-28f,-8f})Box("Sweep warning long edge "+x,warning,new Vector3(x,.055f,18.5f),new Vector3(.16f,.09f,11),p.hazard,false);
        foreach(float z in new[]{13f,24f})Box("Sweep warning cross edge "+z,warning,new Vector3(-18,.055f,z),new Vector3(20,.09f,.16f),p.hazard,false);
        collapse.warningZone=warning;warning.gameObject.SetActive(false);
        director.nestSpawn=Marker("Aguja elevated gantry nest",new Vector3(-12,9.333f,18));
        director.nestSector=Sector("Gantry lookout sector",collapse.nestBounds,new[]{new Vector3(-13,8.67f,17),new Vector3(-11,10,19),new Vector3(-12,11.33f,21)});
        navLegs.Add(Tuple.Create(from+Vector3.back*.8f,to+Vector3.forward*.8f,"Prepared collapsed upper C-E connection"));
    }
    static void CreateContainment()
    {
        director.containment=Frangible("C-F containment diaphragm",new Vector3(12,1.9f,19),new Vector3(6.0f,3.8f,.45f),0,32,120,true);
        director.containment.passageCenter=new Vector3(12,1.05f,19);director.containment.passageHalfExtents=new Vector3(.7f,.94f,.8f);
        director.containment.obstacle=Obstacle(director.containment.transform,Vector3.zero,new Vector3(6.3f,3.8f,.65f),false);
        director.containmentEntry=Marker("New containment-side enemy access",new Vector3(18,0,25));
        foreach(float x in new[]{9f,15f})Box("Containment ceramic rail "+x,detail,new Vector3(x,2.1f,19),new Vector3(.25f,4.2f,.7f),p.ivory,false);
        Box("Containment pressure header",detail,new Vector3(12,4.1f,19),new Vector3(6.6f,.4f,.8f),p.copper,false);
    }

    static void Objectives()
    {
        director.terminals=new[] {
            Terminal("B induction circuit",new Vector3(-48,1.15f,-3),0,1),
            Terminal("F coolant circuit",new Vector3(17,1.5f,41),1,5),
            Terminal("C extraction radio",new Vector3(7,1.4f,7),2,2),
            Terminal("A boarding terminal",new Vector3(0,1.05f,-45),3,0),
            Terminal("D emergency medical station",new Vector3(53,1.2f,-6),4,3)
        };
    }
    static AlphaDistrictTerminal Terminal(string name,Vector3 at,int kind,int index)
    {
        var go=Box(name,mechanisms,at,new Vector3(1.4f,1.6f,.8f),kind==4?p.ivory:p.ink,true,0);
        var terminal=go.AddComponent<AlphaDistrictTerminal>();terminal.director=director;terminal.kind=kind;terminal.zoneIndex=index;
        Box(name+" signal face",go.transform,at+new Vector3(0,.35f,-.45f),new Vector3(1.05f,.65f,.08f),kind==4?p.hazard:p.signal,false);
        LargeSign(kind==2?"RADIO":kind==3?"EMBARQUE":kind==4?"+":kind==0?"I":"II",go.transform,at+new Vector3(0,1.35f,-.05f),0,1.2f,new Vector2(3,.7f),SignColor);
        return terminal;
    }

    static void AuditMarkers()
    {
        for(int i=0;i<6;i++)Marker("QA."+(char)('A'+i),Audit[i]+Vector3.up*1.05f);
        // These are look targets, not teleports or replacement traversal evidence. Their
        // corresponding QA.A..F positions stay unchanged and lie on the real floor.
        Vector3[] views={
            new Vector3(-2,7,2),new Vector3(-46,2.5f,-8),new Vector3(-2,7,2),
            new Vector3(48,3,3),new Vector3(-13,7,40),new Vector3(29,5.5f,35)
        };
        for(int i=0;i<views.Length;i++)Marker("QA.View."+(char)('A'+i),views[i]);
        Marker("QA.Start",director.arrival.position).rotation=director.arrival.rotation;
        Marker("QA.Exit",new Vector3(0,1.05f,-43.3f));
        Marker("QA.CollapseNear",new Vector3(-12,1.05f,14.8f));
        Marker("QA.CollapseRamp",new Vector3(-12,3.05f,19));
        Marker("QA.CollapseFar",new Vector3(-12,5.05f,23.2f));
        Marker("QA.ContainmentNear",new Vector3(12,1.05f,16.6f));
        Marker("QA.ContainmentFar",new Vector3(12,1.05f,23.4f));
        Marker("QA.CELow",new Vector3(-24,1.05f,17.5f));
        Marker("QA.StairFoot",new Vector3(-24,1.05f,19.3f));
        Marker("QA.StairTop",new Vector3(-24,5.05f,30.5f));
        Marker("QA.Nest",director.nestSpawn.position+Vector3.up*1.05f);
    }

    static void Bake(NavMeshSurface surface)
    {
        var settings=surface.GetBuildSettings();settings.agentClimb=.18f;settings.agentHeight=2.05f;settings.agentRadius=.45f;
        settings.overrideVoxelSize=true;settings.voxelSize=.08f;
        var sources=new List<NavMeshBuildSource>();
        UnityEngine.AI.NavMeshBuilder.CollectSources(world,1<<3,NavMeshCollectGeometry.PhysicsColliders,0,false,new List<NavMeshBuildMarkup>(),false,sources);
        var bounds=new Bounds(new Vector3(0,6,-2),new Vector3(140,28,120));
        var data=UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(settings,sources,bounds,Vector3.zero,Quaternion.identity);
        if(!data)throw new InvalidOperationException("Alpha district navigation build failed.");
        data.name="Puerto Resonancia local 0.18m climb";surface.RemoveData();surface.navMeshData=data;surface.AddData();
    }
    static void AssertNavigation()
    {
        foreach(var leg in navLegs)AssertLeg(leg.Item1,leg.Item2,leg.Item3);
        foreach(var sequence in new[]{new[]{0,1,4,2,0},new[]{0,2,4,5,3,0}})
            for(int i=1;i<sequence.Length;i++)AssertLeg(Audit[sequence[i-1]],Audit[sequence[i]],"Shared loop "+Names[sequence[i-1]]+" -> "+Names[sequence[i]],4);
        foreach(var terminal in director.terminals)
        {
            Vector3 approach=terminal.transform.position+Vector3.back*1.9f;approach.y=0;
            AssertLeg(Audit[terminal.zoneIndex],approach,"Terminal access "+terminal.name,4);
        }
        Debug.Log("ALPHA NAVIGATION PASSED: intake bays, corridor joints, upper/lower E, both shared loops, terminals and prepared collapse route. Physical motor tests remain mandatory.");
    }
    static void AssertLeg(Vector3 from,Vector3 to,string label,float detourFactor=1.9f)
    {
        if(!NavMesh.SamplePosition(from,out var a,.38f,NavMesh.AllAreas)||Mathf.Abs(a.position.y-from.y)>.2f ||
           !NavMesh.SamplePosition(to,out var b,.38f,NavMesh.AllAreas)||Mathf.Abs(b.position.y-to.y)>.2f)
            throw new InvalidOperationException("ALPHA NAVIGATION REJECTED: wrong/missing floor for "+label+" ("+from+" -> "+to+").");
        var path=new NavMeshPath();
        if(!NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)
            throw new InvalidOperationException("ALPHA NAVIGATION REJECTED: disconnected "+label+".");
        float length=0;for(int i=1;i<path.corners.Length;i++)length+=Vector3.Distance(path.corners[i-1],path.corners[i]);
        if(length>Vector3.Distance(from,to)*detourFactor+8)
            throw new InvalidOperationException("ALPHA NAVIGATION REJECTED: "+label+" detours "+length.ToString("F1")+"m; a local join may be broken.");
    }
    static void SaveNavigation(NavMeshSurface surface)
    {
        string path=ArtPath+"/Puerto Resonancia Navigation.asset";var existing=AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
        if(existing){var generated=surface.navMeshData;surface.RemoveData();EditorUtility.CopySerialized(generated,existing);Object.DestroyImmediate(generated);surface.navMeshData=existing;EditorUtility.SetDirty(existing);}
        else AssetDatabase.CreateAsset(surface.navMeshData,path);surface.RemoveData();surface.AddData();
    }

    static FoundryStructure Frangible(string name,Vector3 at,Vector3 size,float yaw,int chunks,float health,bool passage)
    {
        var holder=Child(name,mechanisms);holder.position=at;holder.rotation=Quaternion.Euler(0,yaw,0);
        var state=holder.gameObject.AddComponent<FoundryStructure>();state.structuralHealth=health;state.opensPassage=passage;
        var intact=Box(name+" prepared fracture source",holder,at,size,p.destructible,true,0);intact.transform.rotation=holder.rotation;
        var fracture=intact.AddComponent<FractureThis>();var serialized=new SerializedObject(fracture);
        serialized.FindProperty("chunks").intValue=chunks;serialized.FindProperty("density").floatValue=3;
        serialized.FindProperty("internalStrength").floatValue=40;serialized.FindProperty("anchor").intValue=(int)(Anchor.Bottom|Anchor.Top);
        serialized.FindProperty("insideMaterial").objectReferenceValue=p.fractureInside;serialized.FindProperty("outsideMaterial").objectReferenceValue=p.destructible;
        serialized.ApplyModifiedPropertiesWithoutUndo();state.source=fracture;return state;
    }
    static NavMeshObstacle Obstacle(Transform root,Vector3 center,Vector3 size,bool enabled)
    {var o=root.gameObject.AddComponent<NavMeshObstacle>();o.shape=NavMeshObstacleShape.Box;o.center=center;o.size=size;o.carving=true;o.carveOnlyStationary=false;o.enabled=enabled;return o;}
    static FoundryCombatSector Sector(string name,Bounds bounds,Vector3[] positions)
    {
        var sector=Child(name,markers).gameObject.AddComponent<FoundryCombatSector>();sector.bounds=bounds;
        sector.positions=positions.Select((v,i)=>Marker(name+" position "+i,v)).ToArray();return sector;
    }
    static void Deck(string name,Vector3 top,Vector2 size)=>Box(name,solid,top-Vector3.up*.2f,new Vector3(size.x,.4f,size.y),p.floor);
    static void Staircase(string name,Vector3 from,Vector3 to,float width,int count)
    {
        Vector3 delta=to-from;Vector3 flat=Vector3.ProjectOnPlane(delta,Vector3.up);float run=flat.magnitude/count;
        var rotation=Quaternion.LookRotation(flat.normalized);
        for(int i=0;i<count;i++)
        {
            float h=(to.y-from.y)*(i+1)/count;
            Vector3 at=from+flat*((i+.5f)/count)+Vector3.up*(h*.5f-.04f);
            OrientedBox(name+" tread "+i,solid,at,new Vector3(width,h+.08f,run+.015f),rotation,p.ivory);
        }
    }
    static GameObject Ramp(string name,Transform parent,Vector3 from,Vector3 to,float width,Material material,bool collision)
    {
        Vector3 forward=Vector3.ProjectOnPlane(to-from,Vector3.up).normalized,side=Vector3.Cross(Vector3.up,forward)*width*.5f;
        // Top endpoints coincide with the edges of adjacent slabs. There is no oversized
        // landing projecting over the slope and creating the previous pilot's riser.
        Vector3[] v={from-side,from+side,to-side,to+side,from-side-Vector3.up*.35f,from+side-Vector3.up*.35f,to-side-Vector3.up*.35f,to+side-Vector3.up*.35f};
        int[] t={0,2,1,1,2,3,4,5,6,5,7,6,0,1,4,1,5,4,2,6,3,3,6,7,0,4,2,2,4,6,1,3,5,3,7,5};
        return MeshObject(name,parent,v,t,material,collision);
    }
    static void PortalFrame(string name,Vector3 bottom,float yaw,float width,float height)
    {
        var rotation=Quaternion.Euler(0,yaw,0);Vector3 side=rotation*Vector3.right;
        foreach(int sign in new[]{-1,1})OrientedBox(name+" enamel jamb "+sign,detail,bottom+side*(sign*(width*.5f+.12f))+Vector3.up*height*.5f,new Vector3(.35f,height,.8f),rotation,p.ivory,false);
        OrientedBox(name+" copper header",detail,bottom+Vector3.up*(height+.15f),new Vector3(width+.9f,.5f,.85f),rotation,p.copper,false);
    }
    static void Plant(Transform parent,Vector3 origin,float height,float reach,Material material)
    {
        Cylinder("Cultivated trunk "+origin,parent,origin+Vector3.up*height*.45f,.35f,height*.9f,p.copper);
        for(int i=0;i<5;i++){float a=i*Mathf.PI*2/5;Vector3 end=origin+new Vector3(Mathf.Cos(a)*reach,height,Mathf.Sin(a)*reach);Leaf("Cultivated frond "+origin+" "+i,parent,origin+Vector3.up*height*.65f,end,reach*.85f,material);}
    }
    static void Leaf(string name,Transform parent,Vector3 start,Vector3 end,float width,Material material)
    {
        Vector3 side=Vector3.Cross((end-start).normalized,Vector3.up).normalized*width*.5f;
        Vector3 mid=Vector3.Lerp(start,end,.55f),ridge=mid+Vector3.up*.45f,bottom=mid-Vector3.up*.18f;
        Vector3[] v={start,mid-side,end,mid+side,ridge,bottom};
        MeshObject(name,parent,v,new[]{0,1,4,1,2,4,2,3,4,3,0,4,1,0,5,2,1,5,3,2,5,0,3,5},material,false);
        Tube(name+" midrib",parent,start+Vector3.up*.08f,end+Vector3.up*.08f,.055f,p.copper);
    }
    static void Cylinder(string name,Transform parent,Vector3 center,float radius,float height,Material material,bool collision=false)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.position=center;go.transform.localScale=new Vector3(radius*2,height*.5f,radius*2);go.transform.SetParent(parent,true);
        go.layer=collision?3:2;go.GetComponent<Renderer>().sharedMaterial=material;if(!collision)Object.DestroyImmediate(go.GetComponent<Collider>());GameObjectUtility.SetStaticEditorFlags(go,0);
    }
    static void Tube(string name,Transform parent,Vector3 from,Vector3 to,float radius,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.position=(from+to)*.5f;go.transform.rotation=Quaternion.FromToRotation(Vector3.up,(to-from).normalized);
        go.transform.localScale=new Vector3(radius*2,Vector3.Distance(from,to)*.5f,radius*2);go.transform.SetParent(parent,true);go.layer=2;go.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(go.GetComponent<Collider>());GameObjectUtility.SetStaticEditorFlags(go,0);
    }
    static void Pipe(string name,Transform parent,Vector3[] points,float radius,Material material)
    {for(int i=1;i<points.Length;i++)Tube(name+" segment "+i,parent,points[i-1],points[i],radius,material);}
    static GameObject Beam(string name,Transform parent,Vector3 from,Vector3 to,float width,float height,Material material,bool collision)
    {return OrientedBox(name,parent,(from+to)*.5f,new Vector3(width,height,Vector3.Distance(from,to)),Quaternion.LookRotation((to-from).normalized),material,collision);}
    static GameObject OrientedBox(string name,Transform parent,Vector3 center,Vector3 size,Quaternion rotation,Material material,bool collision=true)
    {var go=Box(name,parent,center,size,material,collision);go.transform.rotation=rotation;return go;}
    static GameObject Box(string name,Transform parent,Vector3 center,Vector3 size,Material material,bool collision=true,int layer=3)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=center;go.transform.localScale=size;go.transform.SetParent(parent,true);go.layer=collision?layer:2;
        go.GetComponent<Renderer>().sharedMaterial=material;if(!collision)Object.DestroyImmediate(go.GetComponent<Collider>());GameObjectUtility.SetStaticEditorFlags(go,0);return go;
    }
    static GameObject MeshObject(string name,Transform parent,Vector3[] vertices,int[] triangles,Material material,bool collision)
    {
        // Split faces for deliberately sharp, graphic normals matching the user's toon renderer.
        var mesh=new Mesh{name=name};mesh.vertices=triangles.Select(i=>vertices[i]).ToArray();mesh.triangles=Enumerable.Range(0,triangles.Length).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
        string file=name;foreach(char invalid in Path.GetInvalidFileNameChars())file=file.Replace(invalid,'_');string path=ArtPath+"/Geometry/"+file+".asset";
        var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved){EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);mesh=saved;EditorUtility.SetDirty(mesh);}else AssetDatabase.CreateAsset(mesh,path);
        var go=Child(name,parent).gameObject;go.layer=collision?3:2;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
        if(collision)go.AddComponent<MeshCollider>().sharedMesh=mesh;return go;
    }
    static Transform Child(string name,Transform parent)
    {var go=new GameObject(name);go.transform.SetParent(parent,false);GameObjectUtility.SetStaticEditorFlags(go,0);return go.transform;}
    static Transform Marker(string name,Vector3 at)
    {var marker=Child(name,markers);marker.position=at;return marker;}
    static TextMeshPro LargeSign(string text,Transform parent,Vector3 at,float yaw,float size,Vector2 bounds,Color color)
    {
        var label=Child("Sign "+text,parent).gameObject.AddComponent<TextMeshPro>();label.transform.position=at;label.transform.rotation=Quaternion.Euler(0,yaw,0);label.gameObject.layer=2;
        label.text=text;label.fontSize=size;label.color=color;label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=bounds;
        return label;
    }
    static NormalEnemyStateMachine Enemy(string path)
    {var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);var enemy=prefab?prefab.GetComponent<NormalEnemyStateMachine>():null;if(!enemy)throw new InvalidOperationException("Missing real enemy prefab: "+path);return enemy;}

    [MenuItem("Tools/HELIX/Alpha/Build Puerto Resonancia Windows")]
    public static void Build()
    {
        Create();Directory.CreateDirectory(Path.GetDirectoryName(BuildPath));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath,CorePath},locationPathName=BuildPath,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Puerto Resonancia build failed: "+report.summary.result);
        Debug.Log("PUERTO RESONANCIA BUILD SUCCEEDED: "+BuildPath);
    }
}
