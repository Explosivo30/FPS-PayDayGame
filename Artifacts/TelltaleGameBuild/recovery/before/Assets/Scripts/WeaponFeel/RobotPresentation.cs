using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(70)]
public class RobotPresentation : MonoBehaviour
{
    public Transform visual,muzzle,turret;
    public ParticleSystem sparks,muzzleFlash;
    public Material hitEffectMaterial;
    [Header("Impact feel")]
    [Range(0,2)] public float reactionStrength=1;
    public float flashDuration=.065f;
    [Range(0,1)] public float telegraphStrength=1;
    public float springFrequency=24;
    public int maximumImpactSparks=20;
    Vector3 home,target,hitOffset,hitVelocity,hitAngles,angleVelocity,lastDirection=Vector3.forward;
    Quaternion rotation,turretRotation,deathStart,moveRotation;
    float firePulse,telegraph,deathTime=-1,flash,health01=1,damageClock;
    NavMeshAgent agent;
    Renderer[] bodyRenderers;
    Color[] baseColors,baseEmission;
    MaterialPropertyBlock properties;
    readonly List<Material> ownedMaterials=new List<Material>();
    ParticleSystem contactSparks;
    Material effectMaterial;
    LineRenderer ring;
    float ringLife;
    CombatImpact pending;
    bool hasPending,landed;
    System.Random random;
    public float VisualDisplacement => hitOffset.magnitude;
    public float FlashStrength => flash;
    public Vector3 LastHitDirection => lastDirection;
    public int ImpactBursts { get; private set; }
    public Vector3 MuzzlePosition=>muzzle!=null?muzzle.position:transform.position+Vector3.up;
    void Awake()
    {
        agent=GetComponent<NavMeshAgent>();random=new System.Random(GetInstanceID());
        // Never animate the navigation/collision root as a fallback.
        if(visual==null||visual==transform) { enabled=false;return; }
        home=visual.localPosition;rotation=moveRotation=visual.localRotation;
        if(turret!=null)turretRotation=turret.localRotation;
        var body=new List<Renderer>();
        foreach(var renderer in visual.GetComponentsInChildren<Renderer>())
            if(!(renderer is ParticleSystemRenderer)&&!(renderer is LineRenderer))body.Add(renderer);
        bodyRenderers=body.ToArray();baseColors=new Color[body.Count];baseEmission=new Color[body.Count];
        properties=new MaterialPropertyBlock();
        var copies=new Dictionary<Material,Material>();
        for(int i=0;i<bodyRenderers.Length;i++)
        {
            var materials=bodyRenderers[i].sharedMaterials;
            for(int j=0;j<materials.Length;j++)
            {
                var original=materials[j];if(original==null)continue;
                if(!copies.TryGetValue(original,out var clone))
                {
                    clone=new Material(original);clone.name=original.name+" / robot feedback";
                    clone.EnableKeyword("_EMISSION");copies.Add(original,clone);ownedMaterials.Add(clone);
                }
                materials[j]=clone;
            }
            bodyRenderers[i].sharedMaterials=materials;
            var material=materials.Length>0?materials[0]:null;
            baseColors[i]=material!=null&&material.HasProperty("_BaseColor")?material.GetColor("_BaseColor"):Color.white;
            baseEmission[i]=material!=null&&material.HasProperty("_EmissionColor")?material.GetColor("_EmissionColor"):Color.black;
        }
        CreateEffects();
    }
    void CreateEffects()
    {
        effectMaterial=hitEffectMaterial!=null?new Material(hitEffectMaterial):new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        effectMaterial.SetColor("_BaseColor",Color.white);
        effectMaterial.SetFloat("_Surface",1);effectMaterial.SetFloat("_Blend",2);
        effectMaterial.SetFloat("_SrcBlend",(int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        effectMaterial.SetFloat("_DstBlend",(int)UnityEngine.Rendering.BlendMode.One);
        effectMaterial.SetFloat("_ZWrite",0);effectMaterial.SetFloat("_Cull",0);
        effectMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");effectMaterial.renderQueue=3000;
        var go=new GameObject("Robot contact sparks");go.transform.SetParent(transform,false);go.layer=2;
        contactSparks=go.AddComponent<ParticleSystem>();contactSparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=contactSparks.main;main.playOnAwake=false;main.loop=false;main.duration=.5f;main.startLifetime=.22f;
        main.startSize=.035f;main.startSpeed=0;main.maxParticles=160;main.simulationSpace=ParticleSystemSimulationSpace.World;main.gravityModifier=.7f;
        var emission=contactSparks.emission;emission.enabled=false;var shape=contactSparks.shape;shape.enabled=false;
        var color=contactSparks.colorOverLifetime;color.enabled=true;
        var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(1,.32f,.04f),1)},
            new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});color.color=gradient;
        var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=effectMaterial;
        renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=2.2f;renderer.velocityScale=.06f;
        var circle=new GameObject("Impact ring");circle.transform.SetParent(transform,false);circle.layer=2;
        ring=circle.AddComponent<LineRenderer>();ring.sharedMaterial=effectMaterial;ring.positionCount=13;
        ring.useWorldSpace=false;ring.startWidth=ring.endWidth=.012f;ring.enabled=false;
    }
    public void SetTarget(Vector3 position) { target=position; }
    public void SetHealth(float normalized) { health01=Mathf.Clamp01(normalized); }
    public void Telegraph(float duration) { telegraph=duration; }
    public void Fire() { firePulse=1;muzzleFlash?.Play(); }
    public void Hit(Vector3 direction) { Hit(transform.position+Vector3.up*.8f,-direction,direction,30); }
    public void Hit(Vector3 point,Vector3 normal,Vector3 direction,float damage)
    {
        if(!isActiveAndEnabled||visual==null||visual==transform||deathTime>=0||damage<=0)return;
        lastDirection=direction.normalized;
        var impact=new CombatImpact(GetInstanceID(),point,normal,direction,damage,false,false);
        pending=hasPending?pending.Merge(impact):impact;hasPending=true;
    }
    void FlushHit()
    {
        if(!hasPending)return;
        var impact=pending;hasPending=false;
        float strength=Mathf.Clamp(Mathf.Sqrt(impact.Damage/30f),.45f,1.9f)*reactionStrength;
        var local=transform.InverseTransformDirection(impact.Direction);
        hitOffset=Vector3.ClampMagnitude(hitOffset+local*.035f*strength,.13f);
        hitVelocity=Vector3.ClampMagnitude(hitVelocity+local*.95f*strength,2.2f);
        hitAngles=Vector3.ClampMagnitude(hitAngles+new Vector3(local.z*7,-local.x*3,-local.x*10)*strength,19);
        flash=1;
        Emit(impact.Point+impact.Normal*.03f,impact.Normal,Mathf.Clamp(Mathf.RoundToInt(9*strength),5,maximumImpactSparks),strength);
        ring.transform.SetPositionAndRotation(impact.Point+impact.Normal*.035f,Quaternion.LookRotation(impact.Normal));
        ringLife=.085f;ring.enabled=true;
        ImpactBursts++;
    }
    void Emit(Vector3 position,Vector3 normal,int count,float strength)
    {
        if(contactSparks==null)return;
        for(int i=0;i<count;i++)
        {
            var jitter=new Vector3(Next(),Next(),Next());
            var direction=(normal*.8f+jitter).normalized;
            var particle=new ParticleSystem.EmitParams {
                position=position,velocity=direction*(1.3f+(float)random.NextDouble()*2.8f)*strength,
                startLifetime=.1f+(float)random.NextDouble()*.23f,startSize=.017f+(float)random.NextDouble()*.022f,
                startColor=new Color(1,.72f,.26f)};
            contactSparks.Emit(particle,1);
        }
    }
    float Next() { return (float)random.NextDouble()*2-1; }
    public void Die()
    {
        if(deathTime>=0)return;
        FlushHit();deathTime=Time.time;deathStart=visual!=null?visual.localRotation:rotation;
        if(sparks!=null)sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        Emit(transform.position+Vector3.up*.8f,Vector3.up,30,1.3f);
    }
    static void Spring(ref Vector3 value,ref Vector3 velocity,float frequency,float dt)
    {
        float decay=Mathf.Exp(-frequency*dt);
        Vector3 step=(velocity+frequency*value)*dt;
        velocity=(velocity-frequency*step)*decay;value=(value+step)*decay;
    }
    void LateUpdate()
    {
        if(visual==null||visual==transform||WeaponAction.CombatPaused)return;
        float dt=Time.deltaTime;
        FlushHit();
        if(deathTime>=0)
        {
            float t=Mathf.Clamp01((Time.time-deathTime-.045f)/.48f);
            float eased=1-Mathf.Pow(1-t,3);
            var local=transform.InverseTransformDirection(lastDirection);local.y=0;
            if(local.sqrMagnitude<.001f)local=Vector3.forward;
            local.Normalize();
            var fall=rotation*Quaternion.Euler(local.z*86,0,-local.x*86);
            visual.localRotation=Quaternion.Slerp(deathStart,fall,eased);
            visual.localPosition=home+local*.32f*eased+Vector3.up*(.20f*eased+Mathf.Sin(t*Mathf.PI)*.09f);
            if(t>=1&&!landed) { landed=true;Emit(transform.position+local*.3f+Vector3.up*.15f,Vector3.up,12,.65f); }
        }
        else
        {
            Vector3 velocity=agent!=null&&agent.enabled?transform.InverseTransformDirection(agent.velocity):Vector3.zero;
            visual.localPosition=home+Vector3.up*Mathf.Sin(Time.time*5+GetInstanceID())*.025f+hitOffset;
            moveRotation=Quaternion.Slerp(moveRotation,rotation*Quaternion.Euler(velocity.z*1.5f,0,-velocity.x*2),1-Mathf.Exp(-12*dt));
            visual.localRotation=moveRotation*Quaternion.Euler(hitAngles);
            if(turret!=null)
            {
                Vector3 direction=target-MuzzlePosition;
                float pitch=-Mathf.Atan2(direction.y,new Vector2(direction.x,direction.z).magnitude)*Mathf.Rad2Deg;
                turret.localRotation=turretRotation*Quaternion.Euler(Mathf.Clamp(pitch,-35,35)-firePulse*8,0,0);
            }
            Spring(ref hitOffset,ref hitVelocity,springFrequency,dt);
            Spring(ref hitAngles,ref angleVelocity,springFrequency,dt);
            damageClock-=dt;
            if(health01<.3f&&damageClock<=0) { Emit(transform.position+Vector3.up*.85f,Vector3.up,2,.35f);damageClock=.45f; }
        }
        if(ringLife>0)
        {
            ringLife=Mathf.Max(0,ringLife-dt);float t=1-ringLife/.085f;
            for(int i=0;i<13;i++) { float a=i*Mathf.PI/6;ring.SetPosition(i,new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*Mathf.Lerp(.025f,.16f,t)); }
            ring.startColor=ring.endColor=new Color(1,.85f,.45f,1-t);ring.enabled=ringLife>0;
        }
        for(int i=0;i<bodyRenderers.Length;i++)
        {
            bodyRenderers[i].GetPropertyBlock(properties);
            Color baseColor=deathTime>=0?baseColors[i]*.48f:baseColors[i];
            properties.SetColor("_BaseColor",Color.Lerp(baseColor,new Color(1,.88f,.64f),flash*.45f));
            properties.SetColor("_EmissionColor",baseEmission[i]*(deathTime>=0?.1f:1)+new Color(.65f,.35f,.1f)*flash+
                (telegraph>0&&deathTime<0?new Color(.9f,.16f,.01f)*telegraphStrength:Color.black));
            bodyRenderers[i].SetPropertyBlock(properties);
        }
        flash=Mathf.Max(0,flash-dt/Mathf.Max(.02f,flashDuration));
        firePulse*=Mathf.Exp(-18*dt);telegraph=Mathf.Max(0,telegraph-dt);
    }
    void OnDestroy()
    {
        foreach(var material in ownedMaterials)if(material!=null)Destroy(material);
        if(effectMaterial!=null)Destroy(effectMaterial);
    }
}
