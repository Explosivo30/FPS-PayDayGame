using UnityEngine;

public static class MutationVisual
{
    static Material material;
    public static Material Material
    {
        get
        {
            if(material==null)material=Resources.Load<Material>("Mutations/Effect");
            if(material==null)material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            return material;
        }
    }
    public static void Tint(Renderer renderer,Color color)
    {
        if(renderer==null)return;var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
        block.SetColor("_BaseColor",color);block.SetColor("_Color",color);renderer.SetPropertyBlock(block);
    }
    static Transform Part(Transform root,string name,PrimitiveType shape,Vector3 position,Vector3 scale,Color color)
    {
        var go=GameObject.CreatePrimitive(shape);go.name=name;go.layer=2;
        go.transform.SetParent(root,false);go.transform.localPosition=position;go.transform.localScale=scale;
        var collider=go.GetComponent<Collider>();collider.enabled=false;Object.Destroy(collider);
        go.GetComponent<Renderer>().sharedMaterial=Material;Tint(go.GetComponent<Renderer>(),color);return go.transform;
    }
    public static Transform Model(MutationWorldKind kind,Transform root)
    {
        var model=new GameObject("Device model").transform;model.SetParent(root,false);
        var steel=new Color(.16f,.2f,.24f);var orange=new Color(1,.4f,.04f);
        if(kind==MutationWorldKind.Decoy)
        {
            Part(model,"Beacon",PrimitiveType.Cylinder,Vector3.up*.08f,new Vector3(.5f,.08f,.5f),steel);
            Part(model,"Hologram torso",PrimitiveType.Cube,Vector3.up*.75f,new Vector3(.45f,.45f,.18f),Color.cyan);
            Part(model,"Hologram visor",PrimitiveType.Cube,Vector3.up*1.15f,new Vector3(.23f,.16f,.16f),orange);
            for(int side=-1;side<=1;side+=2)Part(model,"Hologram leg",PrimitiveType.Cube,new Vector3(side*.13f,.35f,0),new Vector3(.1f,.4f,.1f),Color.cyan);
        }
        else if(kind==MutationWorldKind.Hammer)
        {
            Part(model,"Hammer head",PrimitiveType.Cube,Vector3.zero,new Vector3(.9f,.4f,.6f),steel);
            Part(model,"Impact plate",PrimitiveType.Cube,Vector3.down*.22f,new Vector3(.85f,.08f,.55f),orange);
            Part(model,"Actuator",PrimitiveType.Cylinder,Vector3.up*.5f,new Vector3(.13f,.5f,.13f),Color.gray);
        }
        else if(kind==MutationWorldKind.Coil)
        {
            Part(model,"Base",PrimitiveType.Cylinder,Vector3.up*.07f,new Vector3(.55f,.07f,.55f),steel);
            Part(model,"Capacitor",PrimitiveType.Cylinder,Vector3.up*.7f,new Vector3(.22f,.65f,.22f),Color.cyan);
            for(int i=0;i<3;i++)Part(model,"Charge "+i,PrimitiveType.Cube,new Vector3((i-1)*.12f,1.45f,0),new Vector3(.08f,.1f,.12f),Color.gray);
        }
        else if(kind==MutationWorldKind.Saw)
        {
            Part(model,"Saw disk",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.65f,.025f,.65f),Color.yellow);
            for(int i=0;i<8;i++)
            {float a=i*Mathf.PI/4;var tooth=Part(model,"Tooth",PrimitiveType.Cube,new Vector3(Mathf.Cos(a)*.3f,0,Mathf.Sin(a)*.3f),new Vector3(.18f,.07f,.12f),steel);tooth.localRotation=Quaternion.Euler(0,-i*45,0);}
        }
        else if(kind==MutationWorldKind.ReturningBlade)
        {
            Part(model,"Blade",PrimitiveType.Cube,Vector3.forward*.15f,new Vector3(.09f,.035f,.5f),Color.white);
            Part(model,"Grip",PrimitiveType.Cube,Vector3.back*.2f,new Vector3(.1f,.07f,.22f),steel);
        }
        else if(kind==MutationWorldKind.FuseMark||kind==MutationWorldKind.CutMark||kind==MutationWorldKind.Carrier)
        {
            Part(model,"Charge",PrimitiveType.Cube,Vector3.zero,new Vector3(.18f,.18f,.18f),kind==MutationWorldKind.CutMark?Color.cyan:orange);
        }
        else if(kind==MutationWorldKind.HammerMark)
        {
            for(int i=0;i<3;i++)Part(model,"Hammer mark "+i,PrimitiveType.Cube,new Vector3((i-1)*.13f,.2f,0),new Vector3(.09f,.09f,.09f),Color.gray);
        }
        else if(kind==MutationWorldKind.MagazineBomb||kind==MutationWorldKind.TimedMagazine)
        {
            Part(model,"Magazine",PrimitiveType.Cube,Vector3.up*.15f,new Vector3(.18f,.3f,.12f),steel);
            Part(model,"Fuse",PrimitiveType.Cube,new Vector3(0,.31f,0),new Vector3(.16f,.04f,.14f),kind==MutationWorldKind.MagazineBomb?Color.yellow:Color.red);
        }
        else if(kind==MutationWorldKind.Anchor||kind==MutationWorldKind.ScrapMagnet)
        {
            Part(model,"Magnetic hub",PrimitiveType.Cylinder,Vector3.up*.06f,new Vector3(.4f,.05f,.4f),Color.cyan);
            for(int i=0;i<4;i++)Part(model,"Metal fragment",PrimitiveType.Cube,new Vector3((i%2-.5f)*.4f,.15f,(i/2-.5f)*.4f),new Vector3(.12f,.08f,.08f),steel);
        }
        return model;
    }
    public static LineRenderer MakeLine(Transform root,Color color,float width)
    {
        var go=new GameObject("Ability signal",typeof(LineRenderer));go.layer=2;go.transform.SetParent(root,false);
        var line=go.GetComponent<LineRenderer>();line.sharedMaterial=Material;line.useWorldSpace=true;line.positionCount=0;
        line.startWidth=line.endWidth=width;line.startColor=line.endColor=color;line.numCapVertices=3;Tint(line,color);return line;
    }
    public static void Line(Vector3 start,Vector3 end,Color color,float width,float duration,MutationRun owner)
    {
        if(owner==null)return;
        if(owner.GetComponentsInChildren<MutationSignal>().Length>=96)return;
        var line=MakeLine(owner.transform,color,width);line.positionCount=2;line.SetPosition(0,start);line.SetPosition(1,end);
        var signal=line.gameObject.AddComponent<MutationSignal>();signal.Run=owner;signal.Until=owner.Clock+duration;
    }
    public static void Burst(Vector3 point,Color color,float radius,MutationRun owner)
    {
        if(owner==null||owner.GetComponentsInChildren<MutationSignal>().Length>=96)return;
        var line=MakeLine(owner.transform,color,.055f);line.positionCount=25;
        for(int i=0;i<25;i++){float a=i*Mathf.PI/12;line.SetPosition(i,point+new Vector3(Mathf.Cos(a),.03f,Mathf.Sin(a))*radius);}
        var signal=line.gameObject.AddComponent<MutationSignal>();signal.Run=owner;signal.Until=owner.Clock+.22f;
    }
    public static void Cone(Vector3 point,Vector3 forward,float range,Color color,MutationRun owner)
    {
        for(int i=-2;i<=2;i++)Line(point,point+Quaternion.AngleAxis(i*22,Vector3.up)*forward*range,color,.055f,.22f,owner);
        Sound(point,180,owner);
    }
    public static void Sound(Vector3 point,float hz,MutationRun owner)
    {
        if(owner==null||owner.GetComponentsInChildren<MutationSignal>().Length>=96)return;
        var go=new GameObject("Ability audio");go.transform.SetParent(owner.transform);go.transform.position=point;
        var audio=go.AddComponent<AudioSource>();audio.spatialBlend=.65f;audio.minDistance=2;audio.maxDistance=20;audio.volume=.24f;audio.dopplerLevel=0;
        var signal=go.AddComponent<MutationSignal>();signal.Run=owner;signal.Until=owner.Clock+.3f;
        signal.Clip=EnemySignals.Tone(hz,.18f,false);audio.PlayOneShot(signal.Clip);
    }
}
public sealed class MutationSignal : MonoBehaviour
{
    public MutationRun Run;public float Until;public AudioClip Clip;
    bool paused;
    void Update()
    {
        if(Run==null||Run.Player==null||Run.Player.IsDead||Run.Clock>=Until){Destroy(gameObject);return;}
        var audio=GetComponent<AudioSource>();
        if(audio!=null&&paused!=WeaponAction.CombatPaused){paused=WeaponAction.CombatPaused;if(paused)audio.Pause();else audio.UnPause();}
    }
    void OnDestroy(){if(Clip!=null)Destroy(Clip);}
}
