using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class TowerBuild
{
    static TowerFloor NewFloor(string name,int number,string next)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        world=new GameObject(name+" / Environment").transform;
        var floor=new GameObject("Floor configuration").AddComponent<TowerFloor>();
        floor.displayName=name;floor.floorNumber=number;floor.nextScene=next;
        return floor;
    }
    static void Perimeter(float x,float z, bool garden)
    {
        Box("Arena foundation",new Vector3(0,-.35f,0),new Vector3(x*2,.7f,z*2),soil);
        Box("West retaining wall",new Vector3(-x,3,0),new Vector3(.65f,6,z*2),ink);
        Box("East retaining wall",new Vector3(x,3,0),new Vector3(.65f,6,z*2),ink);
        Box("North retaining wall",new Vector3(0,3,z),new Vector3(x*2,6,.65f),ink);
        Box("South west wall",new Vector3(-(x+2.4f)/2,3,-z),new Vector3(x-2.4f,6,.65f),ink);
        Box("South east wall",new Vector3((x+2.4f)/2,3,-z),new Vector3(x-2.4f,6,.65f),ink);
        for(int side=-1;side<=1;side+=2)
        {
            for(float i=-z+3;i<z;i+=6)
            {
                Box("Wall buttress",new Vector3(side*(x-.4f),4,i),new Vector3(.9f,8,.4f),copper);
                Box("Cladding",new Vector3(side*(x-.36f),3.3f,i+2.4f),new Vector3(.16f,5.5f,4.6f),garden?violet:jade,false);
                Box("Living wall stripe",new Vector3(side*(x-.55f),2,i+2.4f),new Vector3(.07f,.07f,3.8f),glow,false);
            }
            for(float i=-x+4;i<x;i+=7)
            {
                Box("Distant structural mast",new Vector3(i,8,side*z),new Vector3(.5f,16,.5f),copper,false);
                Beam("Roof rib",new Vector3(i,13,-z),new Vector3(i,16,z),.3f,ink);
            }
        }
        for(int side=-1;side<=1;side+=2)
        {
            Box("Upper arboretum wall",new Vector3(side*x,12,0),new Vector3(.4f,14,z*2),garden?violet:jade,false);
            Box("Upper end wall",new Vector3(0,12,side*z),new Vector3(x*2,14,.4f),garden?violet:jade,false);
        }
        Box("High canopy sky",new Vector3(0,20,0),new Vector3(x*2,.4f,z*2),garden?violet:jade,false);
        for(int i=-2;i<=2;i++)Box("Roof light strip",new Vector3(i*9,19.7f,0),new Vector3(2,.12f,z*1.9f),ivory,false);
    }
    static void BuildForest()
    {
        var floor=NewFloor("BOSQUE ROBÓTICO",2,"CrystalGarden");
        floor.enemyHealth=100;floor.enemyDamage=9;
        floor.briefing="BOSQUE ROBÓTICO · Las raíces energizadas avisan en ámbar antes de emitir un pulso. Usa los caminos y la pasarela.";
        Perimeter(28,25,false);
        // Wide, connected lanes run around the tree islands and underneath the bridge.
        Path(new Vector3(0,.012f,-13),new Vector3(6,.045f,24));
        Path(new Vector3(-13,.012f,0),new Vector3(5,.045f,45));
        Path(new Vector3(13,.012f,0),new Vector3(5,.045f,45));
        Path(new Vector3(0,.012f,-10),new Vector3(48,.045f,5));
        Path(new Vector3(0,.012f,9),new Vector3(48,.045f,5));
        Path(new Vector3(0,.012f,21),new Vector3(48,.045f,4));
        // Hero tree: copper trunk, layered mechanical fronds and roots used as cover.
        Tree(new Vector3(0,0,2),1.65f,15,true);
        Vector3[] trees={new Vector3(-7,0,-2),new Vector3(8,0,1),new Vector3(-7,0,-17),new Vector3(8,0,-17),
            new Vector3(-21,0,-18),new Vector3(21,0,-18),new Vector3(-21,0,-4),new Vector3(21,0,-3),
            new Vector3(-21,0,17),new Vector3(21,0,18),new Vector3(-8,0,18),new Vector3(8,0,18)};
        for(int i=0;i<trees.Length;i++) Tree(trees[i],.65f+(i%3)*.12f,i*39,false);
        Bridge(18,3,13,42);
        for(int side=-1;side<=1;side+=2)
        {
            Cover(new Vector3(side*6,0,-7),side*12,false);
            Cover(new Vector3(side*20,0,3),0,false);
            Cover(new Vector3(side*6,0,8),90,false);
            PlantBed(new Vector3(side*23,0,-10),false);
            PlantBed(new Vector3(side*23,0,9),false);
            Hazard(new Vector3(side*8,0,1),side*3);
            for(int z=-20;z<=20;z+=10)
                Sign(z<0?"ARBORETUM / 02":"BIO-MECHANICAL SYSTEMS",new Vector3(side*27.35f,4.8f,z),2.7f,new Color(.5f,1,.75f),Quaternion.Euler(0,side*90,0));
        }
        Sign("B O S Q U E   R O B Ó T I C O",new Vector3(0,7,24.45f),8,Color.white);
        Sign("02   /   COPPER GROVE",new Vector3(0,5.5f,24.4f),3.6f,new Color(.4f,1,.65f));
        floor.elevator=Elevator(new Vector3(0,0,-24.7f),2);floor.arrival=floor.elevator.transform.Find("Arrival");
        Supplies(new Vector3(-8,0,-22),true);
        Entries(floor,25,18);
        Lighting(false);
        Bake(floor.gameObject.scene,"RoboticForest");
    }
    static void BuildGarden()
    {
        var floor=NewFloor("JARDÍN DE CRISTAL",3,"LaboratoryFloor");
        floor.enemyHealth=115;floor.enemyDamage=10;
        floor.briefing="JARDÍN DE CRISTAL · Usa los maceteros como cobertura y rodea las agujas. El ascensor conecta de nuevo con el laboratorio.";
        Perimeter(25,23,true);
        Path(new Vector3(0,.01f,-12),new Vector3(6,.045f,22));
        Path(new Vector3(0,.01f,0),new Vector3(46,.045f,5));
        Path(new Vector3(-12,.01f,0),new Vector3(5,.045f,42));
        Path(new Vector3(12,.01f,0),new Vector3(5,.045f,42));
        Path(new Vector3(0,.01f,17),new Vector3(45,.045f,5));
        for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
        {
            Vector3 p=new Vector3(x*17,0,z*12+3);
            if(x==0&&z==-1) continue;
            float size=x==0&&z==0?1.6f:1;
            Shape("Hexagonal botanical island",hex,p,new Vector3(6*size,.6f,6*size),ink,true);
            Shape("Growing medium",hex,p+Vector3.up*.6f,new Vector3(5.6f*size,.08f,5.6f*size),violet);
            for(int i=0;i<7;i++)
            {
                float a=i*2.4f;
                var point=p+new Vector3(Mathf.Cos(a)*1.5f*size,.7f,Mathf.Sin(a)*1.5f*size);
                CrystalPlant(point,size*(.8f+(i%3)*.4f),i*51);
            }
        }
        for(int side=-1;side<=1;side+=2)
        {
            Cover(new Vector3(side*6,0,-7),side*18,true);
            Cover(new Vector3(side*6,0,11),side*-18,true);
            PlantBed(new Vector3(side*21,0,-17),true);
            Beam("Arboretum arch",new Vector3(side*23,0,-4),new Vector3(side*14,10,-4),.65f,ivory);
            Beam("Arboretum arch",new Vector3(side*14,10,-4),new Vector3(0,13,-4),.5f,ivory);
            Beam("Arch light",new Vector3(side*22.6f,1,-4),new Vector3(side*13.7f,10,-4),.09f,pink);
        }
        Sign("J A R D Í N   D E   C R I S T A L",new Vector3(0,7,22.45f),7.3f,Color.white);
        Sign("03   /   LUMINESCENT CONSERVATORY",new Vector3(0,5.5f,22.4f),3.1f,new Color(1,.6f,.8f));
        floor.elevator=Elevator(new Vector3(0,0,-22.7f),3);floor.arrival=floor.elevator.transform.Find("Arrival");
        Supplies(new Vector3(-8,0,-20),true);Entries(floor,22,18);
        Lighting(true);Bake(floor.gameObject.scene,"CrystalGarden");
    }
    static void Path(Vector3 p,Vector3 size)
    {
        Box("Service walkway",p,size,ink,false);
        if(size.x>size.z)
            for(int side=-1;side<=1;side+=2)Box("Route edge",p+new Vector3(0,.027f,side*(size.z*.5f-.1f)),new Vector3(size.x,.02f,.08f),gold,false);
        else for(int side=-1;side<=1;side+=2)Box("Route edge",p+new Vector3(side*(size.x*.5f-.1f),.027f,0),new Vector3(.08f,.02f,size.z),gold,false);
        int count=(int)(Mathf.Max(size.x,size.z)/3);
        for(int i=0;i<count;i++)
        {
            var offset=size.x>size.z?new Vector3(-size.x*.5f+i*3,0,0):new Vector3(0,0,-size.z*.5f+i*3);
            Box("Walkway joint",p+offset+Vector3.up*.026f,size.x>size.z?new Vector3(.035f,.01f,size.z-.3f):new Vector3(size.x-.3f,.01f,.035f),jade,false);
        }
    }
    static void Tree(Vector3 p,float scale,float angle,bool hero)
    {
        var parent=new GameObject(hero?"Copper mother tree":"Mechanical tree");parent.transform.SetParent(world);
        parent.transform.position=p;parent.transform.localScale=Vector3.one*scale;parent.transform.localRotation=Quaternion.Euler(0,angle,0);
        var root=parent.transform;
        Shape("Hexagonal root collar",hex,Vector3.zero,new Vector3(3,.45f,3),ink,true,root);
        Shape("Copper bole",hex,new Vector3(0,.45f,0),new Vector3(1.25f,5.8f,1.25f),copper,true,root);
        for(int i=0;i<5;i++)
        {
            Shape("Insulator ring",hex,new Vector3(0,1+i,0),new Vector3(1.45f,.14f,1.45f),ink,false,root);
            Shape("Power band",hex,new Vector3(0,1.16f+i,0),new Vector3(1.3f,.08f,1.3f),glow,false,root);
        }
        for(int i=0;i<6;i++)
        {
            float a=i*Mathf.PI/3;Vector3 outward=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
            Beam("Copper root",outward*.45f+Vector3.up*.8f,outward*2.5f+Vector3.up*.12f,.3f,copper,root);
            Beam("Branch",Vector3.up*3.7f,outward*2.4f+Vector3.up*6,.42f,copper,root);
            Beam("Branch elbow",outward*2.4f+Vector3.up*6,outward*3.4f+Vector3.up*6.2f,.25f,ink,root);
            for(int tier=0;tier<2;tier++)
            {
                Vector3 pos=Vector3.up*(5.2f+tier*1.25f)+outward*(tier==0?.5f:0);
                float length=tier==0?4.7f:3.2f;
                var frond=Shape("Folded steel frond",leaf,pos,new Vector3(2.5f,1.3f,length),i%2==0?jade:mint,false,root);
                frond.transform.localRotation=Quaternion.Euler(-12,i*60,0);
                var vein=Shape("Copper leaf vein",leaf,pos+Vector3.up*.035f,new Vector3(.16f,1.3f,length*.95f),gold,false,root);
                vein.transform.localRotation=frond.transform.localRotation;
            }
        }
        if(hero)Sign("ENERGY ROOT / 02",new Vector3(0,2,-1.45f),1.4f,Color.white,Quaternion.identity,root);
    }
    static void CrystalPlant(Vector3 p,float scale,float angle)
    {
        Shape("Plant socket",hex,p,new Vector3(1,.22f,1),copper);
        for(int i=0;i<3;i++)
        {
            var spear=Shape("Bioluminescent crystal",crystal,p+new Vector3((i-1)*.35f,.4f,0)*scale,new Vector3(.8f,2.2f+(i==1?1.4f:0),.8f)*scale,i==1?pink:violet,true);
            spear.transform.localRotation=Quaternion.Euler(0,angle,15*(i-1));
            var core=Shape("Crystal luminous facet",crystal,p+new Vector3((i-1)*.35f,.4f,-.06f)*scale,new Vector3(.12f,2.3f,.12f)*scale,glow);
            core.transform.localRotation=spear.transform.localRotation;
        }
        for(int i=0;i<5;i++)
        {
            var blade=Shape("Crystal fern",leaf,p+Vector3.up*.22f,new Vector3(.9f,.7f,1.8f)*scale,i%2==0?mint:jade);
            blade.transform.localRotation=Quaternion.Euler(-20,i*72+angle,0);
        }
    }
    static void PlantBed(Vector3 p,bool garden)
    {
        Shape("Planter rim",hex,p,new Vector3(3.5f,.75f,3.5f),copper,true);
        Shape("Planter soil",hex,p+Vector3.up*.76f,new Vector3(3.1f,.06f,3.1f),ink);
        for(int i=0;i<4;i++)
        {
            float a=i*1.57f;
            Vector3 pos=p+new Vector3(Mathf.Cos(a)*.7f,.82f,Mathf.Sin(a)*.7f);
            if(garden)CrystalPlant(pos,.45f,i*90);
            else for(int j=0;j<4;j++)
            {
                var blade=Shape("Copper fern",leaf,pos,new Vector3(.6f,.65f,1.2f),j%2==0?mint:jade);
                blade.transform.localRotation=Quaternion.Euler(-35,i*30+j*90,0);
            }
        }
    }
    static void Cover(Vector3 p,float angle,bool garden)
    {
        var parent=new GameObject("Service cover");parent.transform.SetParent(world);parent.transform.position=p;parent.transform.rotation=Quaternion.Euler(0,angle,0);
        Box("Dark border",new Vector3(0,.6f,0),new Vector3(3.4f,1.2f,1.3f),ink,true,parent.transform);
        Box("Enamel cover",new Vector3(0,.68f,0),new Vector3(3.15f,1.05f,1.35f),garden?ivory:jade,false,parent.transform);
        Box("Copper cap",new Vector3(0,1.27f,0),new Vector3(3.5f,.12f,1.5f),copper,false,parent.transform);
        for(int i=-1;i<=1;i++)Box("Safety vent",new Vector3(i*.8f,.65f,-.69f),new Vector3(.38f,.12f,.04f),gold,false,parent.transform);
    }
    static void Bridge(float rampX,float height,float z,float width)
    {
        Box("Canopy observation deck",new Vector3(0,height-.18f,z),new Vector3(width,.36f,4),ink);
        Box("Copper deck surface",new Vector3(0,height+.015f,z),new Vector3(width,.03f,3.8f),copper,false);
        for(int side=-1;side<=1;side+=2)
        {
            var ramp=Box("Access ramp "+side,new Vector3(side*rampX,1.42f,z-7),new Vector3(4,.25f,10.5f),ink);
            ramp.transform.rotation=Quaternion.Euler(-Mathf.Atan2(height,10)*Mathf.Rad2Deg,0,0);
            for(int i=0;i<10;i++)Box("Amber ramp tread",new Vector3(side*rampX,.07f+i*.3f,z-11.7f+i),new Vector3(3.8f,.03f,.10f),gold,false);
            for(int i=-2;i<=2;i++)
            {
                if(side==-1&&Mathf.Abs(i*9)>=17)continue;
                Box("Guard panel",new Vector3(i*9,height+.45f,z+side*1.95f),new Vector3(7,.9f,.12f),jade);
                Box("Guard top",new Vector3(i*9,height+.96f,z+side*1.95f),new Vector3(7,.07f,.16f),glow,false);
            }
        }
        for(int i=-2;i<=2;i++)Box("Deck support",new Vector3(i*9,1.4f,z),new Vector3(.35f,2.8f,.35f),copper);
    }
    static void Hazard(Vector3 p,float phase)
    {
        var node=new GameObject("Energized root warning");node.transform.SetParent(world);node.transform.position=p;
        var pulse=node.AddComponent<RootPulse>();pulse.phase=phase;
        pulse.emitter=Shape("Pulse coil",hex,Vector3.up*.5f,new Vector3(1,.3f,1),glow,false,node.transform).GetComponent<Renderer>();
        // Hex outline uses six bars, leaving the floor and player feet visible.
        var ring=new GameObject("Warning perimeter").transform;ring.SetParent(node.transform,false);ring.localPosition=Vector3.up*.065f;
        for(int i=0;i<6;i++)
        {
            float a=(i+.5f)*Mathf.PI/3;
            var bar=Box("Warning arc",new Vector3(Mathf.Cos(a)*.43f,0,Mathf.Sin(a)*.43f),new Vector3(.5f,1,.025f),gold,false,ring);
            bar.transform.localRotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg+90,0);
        }
        pulse.ring=ring;ring.gameObject.SetActive(false);
    }
    static void Entries(TowerFloor floor,float x,float z)
    {
        var parent=new GameObject("Hidden wave entries").transform;parent.SetParent(world);
        floor.spawnPoints=new Transform[4];int index=0;
        for(int side=-1;side<=1;side+=2)for(int end=-1;end<=1;end+=2)
        {
            Vector3 p=new Vector3(side*x,0,end*z);
            var entry=new GameObject("Entry "+(++index)).transform;entry.SetParent(parent);entry.position=p;floor.spawnPoints[index-1]=entry;
            Box("Spawn screen",p+new Vector3(-side*1.8f,1.65f,0),new Vector3(.35f,3.3f,4.8f),ink);
            Box("Entry signal",p+Vector3.up*3.4f,new Vector3(2,.08f,.1f),gold,false);
        }
    }
    static void Supplies(Vector3 p,bool shop)
    {
        for(int i=0;i<(shop?3:2);i++)
        {
            var basePos=p+new Vector3(i*3,0,0);
            var terminal=Box(i==2?"Upgrade station":i==0?"Ammo station":"Medical station",basePos+Vector3.up*.7f,new Vector3(1.7f,1.4f,1.1f),ink);
            var screen=Box("Terminal screen",basePos+new Vector3(0,1.35f,.57f),new Vector3(1.4f,.5f,.06f),i==1?pink:glow,false);
            string text=i==2?"MEJORAS":i==0?"MUNICIÓN / 40":"SALUD / 60";
            Sign(text,basePos+new Vector3(0,1.55f,.62f),1.5f,Color.white,Quaternion.Euler(0,180,0));
            if(i==2)terminal.AddComponent<ShopOpener>();
            else { var supply=terminal.AddComponent<TowerSupply>();supply.medical=i==1;supply.price=i==0?40:60; }
        }
    }
    static TowerElevator Elevator(Vector3 p,int number)
    {
        var cabin=new GameObject("Ascensor "+number);cabin.transform.SetParent(world);cabin.transform.position=p;
        var root=cabin.transform;
        Box("Cabin floor",new Vector3(0,-.12f,-2.2f),new Vector3(4.8f,.24f,4.8f),ink,true,root);
        Box("Cabin back",new Vector3(0,1.8f,-4.4f),new Vector3(4.8f,3.6f,.2f),ink,true,root);
        Box("Cabin roof",new Vector3(0,3.7f,-2.2f),new Vector3(4.8f,.2f,4.8f),ink,true,root);
        for(int side=-1;side<=1;side+=2)
        {
            Box("Cabin side",new Vector3(side*2.35f,1.8f,-2.2f),new Vector3(.2f,3.6f,4.6f),jade,true,root);
            Box("Door pocket",new Vector3(side*2.05f,1.8f,.07f),new Vector3(1.1f,3.6f,.3f),copper,true,root);
            Box("Door jamb light",new Vector3(side*1.57f,1.7f,.25f),new Vector3(.055f,3.3f,.06f),gold,false,root);
            Box("Cabin light",new Vector3(side*2.20f,2.8f,-2),new Vector3(.08f,.08f,3.3f),glow,false,root);
        }
        Box("Header",new Vector3(0,3.65f,.02f),new Vector3(5,.6f,.5f),ink,true,root);
        var elevator=cabin.AddComponent<TowerElevator>();
        elevator.leftDoor=Box("Left sliding door",new Vector3(-.76f,1.65f,0),new Vector3(1.5f,3.3f,.14f),ivory,true,root).transform;
        elevator.rightDoor=Box("Right sliding door",new Vector3(.76f,1.65f,0),new Vector3(1.5f,3.3f,.14f),ivory,true,root).transform;
        elevator.leftDoor.gameObject.isStatic=false;elevator.rightDoor.gameObject.isStatic=false;
        // Dynamic doors must not erase the cabin entrance from the baked navigation.
        elevator.leftDoor.gameObject.layer=0;elevator.rightDoor.gameObject.layer=0;
        var arrival=new GameObject("Arrival").transform;arrival.SetParent(root,false);arrival.localPosition=new Vector3(0,1.05f,-2.4f);
        var panel=Box("SUBIR / E",new Vector3(1.8f,1.3f,-2.8f),new Vector3(.45f,.7f,.12f),gold,true,root);
        panel.AddComponent<ElevatorPanel>().elevator=elevator;
        Sign("E\nSUBIR",new Vector3(1.8f,1.3f,-2.72f),1.7f,Color.black,Quaternion.Euler(0,180,0),root);
        elevator.statusText=Sign("ASCENSOR",new Vector3(0,4.3f,.3f),2.5f,Color.white,Quaternion.Euler(0,180,0),root);
        return elevator;
    }
}
