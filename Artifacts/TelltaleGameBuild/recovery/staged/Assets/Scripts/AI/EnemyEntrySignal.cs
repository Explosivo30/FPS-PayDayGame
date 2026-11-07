using UnityEngine;
public sealed class EnemyEntrySignal : MonoBehaviour
{
    Material material;AudioSource source;AudioClip clip;Light lamp;float age;bool paused;
    public static EnemyEntrySignal Create(Vector3 point)
    {
        var go=new GameObject("Enemy entry warning");go.transform.position=point+Vector3.up*.08f;
        return go.AddComponent<EnemyEntrySignal>();
    }
    void Awake()
    {
        material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.SetColor("_BaseColor",new Color(1,.45f,.04f));
        var line=gameObject.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=false;
        line.startWidth=line.endWidth=.06f;line.positionCount=33;
        for(int i=0;i<=32;i++)line.SetPosition(i,new Vector3(Mathf.Cos(i*Mathf.PI/16),0,Mathf.Sin(i*Mathf.PI/16))*.8f);
        lamp=gameObject.AddComponent<Light>();lamp.color=new Color(1,.35f,.02f);lamp.range=4;lamp.intensity=3;
        source=gameObject.AddComponent<AudioSource>();source.spatialBlend=1;source.minDistance=3;source.maxDistance=35;source.volume=.6f;
        clip=EnemySignals.Tone(320,.7f,true);source.clip=clip;source.Play();
    }
    void Update()
    {
        if(paused!=WeaponAction.CombatPaused){paused=WeaponAction.CombatPaused;if(paused)source.Pause();else source.UnPause();}
        if(paused)return;age+=Time.deltaTime;lamp.intensity=2+Mathf.Sin(age*18);
        transform.localScale=Vector3.one*(1+.12f*Mathf.Sin(age*12));
    }
    void OnDestroy(){Destroy(material);Destroy(clip);}
}
