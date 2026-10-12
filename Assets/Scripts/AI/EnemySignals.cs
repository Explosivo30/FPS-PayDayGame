using UnityEngine;

/// <summary>Owns cosmetic veteran parts and spatial warning audio, never navigation or damage.</summary>
public sealed class EnemySignals : MonoBehaviour
{
    AudioSource source;AudioClip normalTone,veteranTone;Material plate,lightMaterial;
    NormalEnemyStateMachine owner;bool paused,veteran;Light beacon;
    void Awake()
    {
        owner=GetComponent<NormalEnemyStateMachine>();
        source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1;
        source.minDistance=3;source.maxDistance=28;source.rolloffMode=AudioRolloffMode.Linear;source.volume=.3f;
        normalTone=Tone(480,.18f,false);veteranTone=Tone(owner.IsMelee?180:780,owner.IsMelee?.65f:.4f,true);
    }
    public static AudioClip Tone(float hz,float duration,bool doubleTone)
    {
        const int rate=22050;var data=new float[Mathf.CeilToInt(rate*duration)];
        for(int i=0;i<data.Length;i++)
        {
            float t=(float)i/rate,envelope=Mathf.Sin(Mathf.PI*i/data.Length);
            float gate=doubleTone?(.45f+.55f*Mathf.Sin(t*45)*Mathf.Sin(t*45)):1;
            data[i]=Mathf.Sin(2*Mathf.PI*(hz*t+160*t*t))*envelope*gate*.3f;
        }
        var clip=AudioClip.Create("Robot warning",data.Length,1,rate,false);clip.SetData(data,0);return clip;
    }
    public void Attack(bool special){if(!WeaponAction.CombatPaused)source.PlayOneShot(special?veteranTone:normalTone);}
    public void SetVeteran()
    {
        if(veteran)return;veteran=true;
        var visual=GetComponent<RobotPresentation>()?.visual;if(visual==null)return;
        var template=visual.GetComponentInChildren<MeshRenderer>()?.sharedMaterial;
        plate=template!=null?new Material(template):new Material(Shader.Find("Universal Render Pipeline/Lit"));
        plate.SetColor("_BaseColor",new Color(.14f,.17f,.2f));
        lightMaterial=new Material(plate);lightMaterial.SetColor("_BaseColor",new Color(1,.57f,.06f));
        lightMaterial.EnableKeyword("_EMISSION");lightMaterial.SetColor("_EmissionColor",new Color(1,.24f,.015f)*2);
        // Distinct silhouette, two illuminated rank bars and a crown survive material flashes.
        float shoulderHeight=owner.IsMelee?1.6f:1.25f,crownHeight=owner.IsMelee?2.05f:1.9f;
        float rankHeight=owner.IsMelee?1.35f:1.1f,front=owner.IsMelee?.35f:.64f;
        Part(visual,"Veteran shoulder L",new Vector3(-.55f,shoulderHeight,0),new Vector3(.38f,.2f,.48f),plate);
        Part(visual,"Veteran shoulder R",new Vector3(.55f,shoulderHeight,0),new Vector3(.38f,.2f,.48f),plate);
        for(int i=0;i<2;i++)Part(visual,"Veteran rank",new Vector3(0,rankHeight+i*.12f,front),new Vector3(.34f,.055f,.05f),lightMaterial);
        Part(visual,"Veteran crown",new Vector3(0,crownHeight,0),new Vector3(.22f,.18f,.22f),lightMaterial);
        var lamp=new GameObject("Veteran beacon");lamp.transform.SetParent(visual,false);lamp.transform.localPosition=LocalMetres(visual,new Vector3(0,crownHeight,0));
        beacon=lamp.AddComponent<Light>();beacon.color=new Color(1,.35f,.04f);beacon.range=2;beacon.intensity=1;
    }
    static Vector3 LocalMetres(Transform parent,Vector3 value)=>new Vector3(value.x/parent.lossyScale.x,value.y/parent.lossyScale.y,value.z/parent.lossyScale.z);
    static void Part(Transform parent,string name,Vector3 position,Vector3 size,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.layer=2;
        go.transform.SetParent(parent,false);go.transform.localPosition=LocalMetres(parent,position);go.transform.localScale=LocalMetres(parent,size);
        go.GetComponent<Collider>().enabled=false;Destroy(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;
    }
    void Update()
    {
        if(owner.IsDead){source.Stop();if(beacon!=null)beacon.enabled=false;return;}
        if(paused!=WeaponAction.CombatPaused){paused=WeaponAction.CombatPaused;if(paused)source.Pause();else source.UnPause();}
    }
    void OnDestroy(){if(normalTone!=null)Destroy(normalTone);if(veteranTone!=null)Destroy(veteranTone);if(plate!=null)Destroy(plate);if(lightMaterial!=null)Destroy(lightMaterial);}
}
