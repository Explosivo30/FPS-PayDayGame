using System.Collections.Generic;
using UnityEngine;

/// <summary>Coalesces pellet contacts into one readable sound per rendered frame.</summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public class ImpactFeedbackPlayer : MonoBehaviour
{
    [Range(0,1)] public float confirmationVolume=.22f;
    [Range(0,1)] public float metalVolume=.3f;
    readonly Dictionary<int,CombatImpact> pending=new Dictionary<int,CombatImpact>();
    AudioSource confirmation,impactSource;
    AudioClip hit,kill,metal,crush,blade;
    GameObject emitter;
    public int CuesPlayed { get; private set; }
    public CombatImpact LastImpact { get; private set; }
    void Awake()
    {
        confirmation=gameObject.AddComponent<AudioSource>();
        confirmation.playOnAwake=false;confirmation.spatialBlend=0;confirmation.priority=24;
        emitter=new GameObject("Contact sound");emitter.transform.SetParent(transform,false);
        impactSource=emitter.AddComponent<AudioSource>();impactSource.playOnAwake=false;
        impactSource.spatialBlend=.8f;impactSource.minDistance=3;impactSource.maxDistance=35;
        impactSource.rolloffMode=AudioRolloffMode.Logarithmic;impactSource.dopplerLevel=0;impactSource.priority=48;
        hit=MakeSound("Hit confirmation",0);kill=MakeSound("Elimination confirmation",1);
        metal=MakeSound("Metal impact",2);crush=MakeSound("Robot collapse",3);blade=MakeSound("Blade contact",4);
    }
    void OnEnable() { CombatFeedback.Impact+=Queue; }
    void OnDisable()
    {
        CombatFeedback.Impact-=Queue;pending.Clear();confirmation?.Stop();impactSource?.Stop();
    }
    void OnDestroy()
    {
        foreach(var clip in new[]{hit,kill,metal,crush,blade}) if(clip!=null) Destroy(clip);
    }
    void Queue(CombatImpact impact)
    {
        if(WeaponAction.CombatPaused)return;
        pending[impact.TargetId]=pending.TryGetValue(impact.TargetId,out var previous)?previous.Merge(impact):impact;
    }
    void LateUpdate()
    {
        if(WeaponAction.CombatPaused) { pending.Clear();return; }
        if(pending.Count==0)return;
        CombatImpact strongest=default;bool found=false;
        foreach(var contact in pending.Values)
        {
            if(!found || contact.Fatal&&!strongest.Fatal || contact.Fatal==strongest.Fatal&&contact.Damage>strongest.Damage)
            { strongest=contact;found=true; }
        }
        pending.Clear();LastImpact=strongest;CuesPlayed++;
        confirmation.pitch=strongest.Fatal?1:Mathf.Lerp(1.12f,.93f,Mathf.Clamp01(strongest.Damage/80));
        confirmation.PlayOneShot(strongest.Fatal?kill:hit,confirmationVolume);
        emitter.transform.position=strongest.Point;
        impactSource.pitch=strongest.Melee?.9f:Mathf.Lerp(1.12f,.82f,Mathf.Clamp01(strongest.Damage/90));
        impactSource.PlayOneShot(strongest.Fatal?crush:strongest.Melee?blade:metal,metalVolume);
    }
    public static AudioClip MakeSound(string name,int kind)
    {
        const int rate=44100;
        float length=kind==3?.42f:kind==1?.22f:kind==4?.19f:.12f;
        var data=new float[Mathf.CeilToInt(rate*length)];
        var random=new System.Random(735+kind);float low=0,peak=0;
        for(int i=0;i<data.Length;i++)
        {
            float t=i/(float)rate;float noise=(float)random.NextDouble()*2-1;low+=(noise-low)*.12f;
            float sample;
            if(kind==0)
                sample=(Mathf.Sin(t*2*Mathf.PI*1750)*.46f+noise*.25f)*Mathf.Exp(-t*85);
            else if(kind==1)
            {
                float second=Mathf.Max(0,t-.042f);
                sample=Mathf.Sin(t*2*Mathf.PI*1200)*Mathf.Exp(-t*70)*.34f;
                if(t>.042f)sample+=Mathf.Sin(second*2*Mathf.PI*1850)*Mathf.Exp(-second*35)*.42f;
            }
            else
            {
                float body=kind==3?105:kind==4?640:310;
                sample=noise*Mathf.Exp(-t*(kind==3?32:100))*.55f+
                    Mathf.Sin(t*2*Mathf.PI*body)*Mathf.Exp(-t*24)*.45f+
                    Mathf.Sin(t*2*Mathf.PI*body*2.76f)*Mathf.Exp(-t*43)*.2f+
                    low*Mathf.Exp(-t*18)*.22f;
                if(kind==3&&t>.07f)sample+=noise*Mathf.Exp(-(t-.07f)*35)*.18f;
            }
            sample*=Mathf.Min(1,t*1600)*Mathf.Clamp01((length-t)*200);
            data[i]=sample;peak=Mathf.Max(peak,Mathf.Abs(sample));
        }
        if(peak>0)for(int i=0;i<data.Length;i++)data[i]*=.78f/peak;
        var clip=AudioClip.Create(name,data.Length,1,rate,false);clip.SetData(data,0);return clip;
    }
}
