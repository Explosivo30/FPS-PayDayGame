using System;
using System.Collections.Generic;
using UnityEngine;

public enum MutationWorldKind
{
    TimedMagazine,MagazineBomb,Decoy,Anchor,Hammer,Saw,Coil,ScrapMagnet,SlagTrail,GroundArc,
    FuseMark,CutMark,Carrier,ReturningBlade,HammerMark
}

/// <summary>Physical and timed effects share the run clock, never independent realtime timers.</summary>
public sealed class MutationWorldEffect : MonoBehaviour
{
    public MutationWorldKind Kind { get; private set; }
    public MutationRun Run { get; private set; }
    public Component Source { get; private set; }
    public bool Live { get; private set; }
    public NormalEnemyStateMachine Target;
    public Vector3 Direction;
    public readonly List<Vector3> Path=new List<Vector3>();
    public int Transfers,Captured;
    public float Remaining=>Mathf.Max(0,expires-Run.Clock);
    public bool Returning { get; private set; }
    public bool Dropped { get; private set; }
    public float Travelled { get; private set; }
    float born,expires,damage;
    readonly HashSet<int> hit=new HashSet<int>();
    readonly Dictionary<int,float> pulled=new Dictionary<int,float>();
    Transform model;
    LineRenderer outline;
    MutationObject receiver;
    Renderer[] bladeRenderers;

    public void Initialize(MutationRun owner,MutationWorldKind kind,float lifetime,Component source,float amount)
    {
        Run=owner;Kind=kind;Source=source;damage=amount;Live=true;born=owner.Clock;expires=born+lifetime;
        gameObject.layer=2;
        model=MutationVisual.Model(kind,transform);
        if(kind==MutationWorldKind.MagazineBomb||kind==MutationWorldKind.Coil||kind==MutationWorldKind.Decoy)
        {
            var target=new GameObject("Shootable device");target.layer=3;target.transform.SetParent(transform,false);
            var box=target.AddComponent<BoxCollider>();bool tall=kind==MutationWorldKind.Decoy||kind==MutationWorldKind.Coil;
            box.size=tall?new Vector3(.5f,kind==MutationWorldKind.Coil?1.85f:1.2f,.35f):new Vector3(.4f,.5f,.4f);
            box.center=Vector3.up*(kind==MutationWorldKind.Coil?.95f:tall?.65f:.23f);
            receiver=target.AddComponent<MutationObject>();receiver.Effect=this;
        }
        outline=MutationVisual.MakeLine(transform,kind==MutationWorldKind.GroundArc?Color.cyan:new Color(1,.5f,.08f),.045f);
        if(kind==MutationWorldKind.ReturningBlade&&source!=null)
        {
            bladeRenderers=source.GetComponentsInChildren<Renderer>(true);
            foreach(var renderer in bladeRenderers)if(!(renderer is LineRenderer)&&!(renderer is ParticleSystemRenderer))renderer.forceRenderingOff=true;
        }
        MutationVisual.Sound(transform.position,kind==MutationWorldKind.Coil?340:kind==MutationWorldKind.Hammer?160:700,owner);
    }
    public void Tick(float dt)
    {
        if(!Live||Run==null)return;
        if(Target!=null)
        {
            transform.position=Target.AimPoint;
            if(Target.IsDead)
            {
                if(Kind==MutationWorldKind.FuseMark||Kind==MutationWorldKind.CutMark||Kind==MutationWorldKind.HammerMark){Finish();return;}
                Target=null;
            }
        }
        float age=Run.Clock-born;
        if(Kind==MutationWorldKind.Saw||Kind==MutationWorldKind.ReturningBlade)Projectile(Mathf.Max(0,dt));
        if(!Live)return;
        if(Kind==MutationWorldKind.ScrapMagnet||Kind==MutationWorldKind.Anchor)Magnet(dt);
        if(Kind==MutationWorldKind.SlagTrail||Kind==MutationWorldKind.GroundArc)GroundDamage();
        if(Kind==MutationWorldKind.Hammer&&model!=null)
            model.localPosition=Vector3.up*Mathf.Lerp(3.2f,.3f,Mathf.Pow(Mathf.Clamp01(age/.8f),3));
        if(model!=null&&(Kind==MutationWorldKind.Saw||Kind==MutationWorldKind.ReturningBlade))model.Rotate(0,dt*900,0,Space.Self);
        DrawOutline(age);
        if(Run.Clock>=expires)
        {
            if(Kind==MutationWorldKind.TimedMagazine||Kind==MutationWorldKind.Carrier||Kind==MutationWorldKind.Hammer)Detonate();
            else Finish();
        }
    }
    void DrawOutline(float age)
    {
        if(outline==null)return;
        if(Kind==MutationWorldKind.SlagTrail||Kind==MutationWorldKind.GroundArc)
        {
            outline.startWidth=outline.endWidth=Kind==MutationWorldKind.SlagTrail?.55f:.28f;
            outline.positionCount=Path.Count;if(Path.Count>0)outline.SetPositions(Path.ToArray());return;
        }
        if(Kind==MutationWorldKind.Hammer||Kind==MutationWorldKind.HammerMark)
        {
            float size=Kind==MutationWorldKind.Hammer?1.35f:.28f;
            outline.positionCount=5;
            Vector3[] square={new Vector3(-size,.05f,-size),new Vector3(size,.05f,-size),new Vector3(size,.05f,size),new Vector3(-size,.05f,size),new Vector3(-size,.05f,-size)};
            for(int i=0;i<5;i++)outline.SetPosition(i,transform.position+square[i]);
            if(Kind==MutationWorldKind.HammerMark&&model!=null)
                for(int i=0;i<model.childCount;i++)MutationVisual.Tint(model.GetChild(i).GetComponent<Renderer>(),i<Transfers?Color.yellow:Color.gray);
            return;
        }
        float radius=Kind==MutationWorldKind.Anchor||Kind==MutationWorldKind.ScrapMagnet?3:Kind==MutationWorldKind.Hammer?1.4f:
            Kind==MutationWorldKind.TimedMagazine||Kind==MutationWorldKind.MagazineBomb?2.5f:.28f;
        outline.positionCount=33;
        float fraction=Kind==MutationWorldKind.Carrier||Kind==MutationWorldKind.TimedMagazine||Kind==MutationWorldKind.FuseMark||Kind==MutationWorldKind.CutMark?Mathf.Clamp01(Remaining/(expires-born)):1;
        for(int i=0;i<33;i++)
        {float a=i*Mathf.PI/16*fraction;outline.SetPosition(i,transform.position+new Vector3(Mathf.Cos(a),.05f,Mathf.Sin(a))*radius);}
        if(Kind==MutationWorldKind.Coil&&model!=null)
        {
            for(int i=0;i<model.childCount;i++)
            {var part=model.GetChild(i);if(part.name.StartsWith("Charge"))MutationVisual.Tint(part.GetComponent<Renderer>(),i-2<Captured?Color.yellow:Color.gray);}
        }
    }
    void Magnet(float dt)
    {
        int count=0;
        foreach(var enemy in Run.Enemies(transform.position+Vector3.up*.7f,3))
        {
            if(Mathf.Abs(enemy.transform.position.y-transform.position.y)>.65f||!Run.Visible(transform.position+Vector3.up*.2f,enemy.AimPoint))continue;
            var control=MutationEnemyControl.For(enemy,Run);
            if(enemy.IsVeteran&&Kind==MutationWorldKind.Anchor)control.Slow(.15f);
            else
            {
                int id=enemy.GetInstanceID();float prior=pulled.TryGetValue(id,out float used)?used:0;
                float maximum=enemy.IsVeteran?.15f:1.5f;
                float move=control.MoveToward(transform.position,Mathf.Min(Mathf.Max(0,dt)*(enemy.IsVeteran?.15f:2),maximum-prior));pulled[id]=prior+move;
            }
            if(++count>=4)break;
        }
    }
    void GroundDamage()
    {
        if(Path.Count<2)return;
        foreach(var enemy in Run.Enemies(transform.position+Vector3.up,16))
        {
            int id=enemy.GetInstanceID();if(hit.Contains(id)||(Kind==MutationWorldKind.GroundArc&&hit.Count>=4))continue;
            Vector3 feet=enemy.transform.position;
            for(int i=1;i<Path.Count;i++)
            {
                Vector3 segment=Path[i]-Path[i-1];float t=segment.sqrMagnitude>.001f?Mathf.Clamp01(Vector3.Dot(feet-Path[i-1],segment)/segment.sqrMagnitude):0;
                Vector3 closest=Path[i-1]+segment*t;
                if(Vector3.Distance(feet,closest)>.7f||!Run.Visible(closest+Vector3.up*.15f,enemy.AimPoint))continue;
                hit.Add(id);Run.Deal(enemy,damage,Source);
                if(Kind==MutationWorldKind.GroundArc&&!enemy.IsDead)MutationEnemyControl.For(enemy,Run).Slow(.8f);
                break;
            }
        }
    }
    void Projectile(float dt)
    {
        if(Dropped)
        {
            if(Vector3.Distance(Run.Player.transform.position,transform.position)<1.3f)
            {Run.SawRecovered();Finish();}return;
        }
        bool blade=Kind==MutationWorldKind.ReturningBlade;
        if(blade&&Returning)
        {
            Vector3 to=Run.Player.transform.position+Vector3.up*.7f-transform.position;
            if(to.magnitude<.6f){Finish();return;}Direction=to.normalized;
        }
        float distance=Mathf.Min(dt*(blade?25:18),blade&&!Returning?Mathf.Max(0,12-Travelled):blade?40:Mathf.Max(0,12-Travelled));
        var overlaps=Physics.OverlapSphere(transform.position,.13f,~(1<<7|1<<8|1<<2),QueryTriggerInteraction.Ignore);
        foreach(var collider in overlaps)
        {
            if(collider.GetComponentInParent<NormalEnemyStateMachine>()!=null||collider.GetComponentInParent<MutationObject>()!=null)continue;
            if(blade&&!Returning){Returning=true;hit.Clear();return;}
            if(blade){Finish();return;}DropSaw();return;
        }
        foreach(var collider in overlaps)
        {
            var enemy=collider.GetComponentInParent<NormalEnemyStateMachine>();
            if(enemy!=null)HitEnemy(enemy,default,blade);
        }
        var hits=Physics.SphereCastAll(transform.position,.13f,Direction,distance,~(1<<7|1<<8|1<<2),QueryTriggerInteraction.Ignore);
        Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
        foreach(var contact in hits)
        {
            var enemy=contact.collider.GetComponentInParent<NormalEnemyStateMachine>();
            if(enemy!=null)
            {
                HitEnemy(enemy,contact,blade);
            }
            else if(contact.collider.GetComponentInParent<MutationObject>()==null)
            {
                transform.position+=Direction*Mathf.Max(0,contact.distance-.03f);
                if(blade&&!Returning){Returning=true;hit.Clear();return;}
                if(blade){Finish();return;}
                DropSaw();return;
            }
        }
        transform.position+=Direction*distance;
        if(!Returning)Travelled+=distance;
        if(Travelled>=11.99f&&!Returning)
        {if(blade){Returning=true;hit.Clear();}else DropSaw();}
    }
    void HitEnemy(NormalEnemyStateMachine enemy,RaycastHit contact,bool blade)
    {
        if(enemy.IsDead||hit.Contains(enemy.GetInstanceID())||blade&&hit.Count>=2)return;
        hit.Add(enemy.GetInstanceID());
        if(blade)
        {
            if(contact.collider!=null)enemy.ReactToHit(contact,Direction,damage);
            else enemy.GetComponent<RobotPresentation>()?.Hit(enemy.AimPoint,-Direction,Direction,damage);
            enemy.TakeDamage(damage);
            if(enemy.IsDead)CombatFeedback.ReportPlayerKill(enemy,Source,true);
            CombatFeedback.ReportHit();Run.OnKnifeHit((Knife)Source,enemy);
        }
        else Run.Deal(enemy,damage,Source);
    }
    void DropSaw(){Dropped=true;transform.position=Run.Ground(transform.position);Direction=Vector3.zero;}
    public bool Capture()
    {
        if(!Live||Kind!=MutationWorldKind.Coil||Captured>=3)return false;
        Captured++;MutationVisual.Burst(transform.position+Vector3.up*.4f,Color.cyan,.45f,Run);return true;
    }
    public void ShotByPlayer()
    {
        if(!Live||WeaponAction.CombatPaused)return;
        if(Kind==MutationWorldKind.MagazineBomb||Kind==MutationWorldKind.Coil)Detonate();
        else if(Kind==MutationWorldKind.Decoy)Finish();
    }
    public void Detonate()
    {
        if(!Live||WeaponAction.CombatPaused)return;
        Vector3 point=transform.position;int energy=Captured;var kind=Kind;Finish();
        if(kind==MutationWorldKind.Coil)
        {
            if(energy>0)Run.Blast(point+Vector3.up*.45f,3,damage*energy,6,Source);
            foreach(var enemy in Run.Enemies(point+Vector3.up,3))
                if(Run.Visible(point+Vector3.up*.4f,enemy.AimPoint))MutationEnemyControl.For(enemy,Run).MoveToward(enemy.transform.position+(enemy.transform.position-point).normalized,enemy.IsVeteran?.2f:.8f);
        }
        else Run.Blast(point+Vector3.up*.5f,kind==MutationWorldKind.Hammer?1.4f:2.5f,damage,kind==MutationWorldKind.Carrier?6:4,Source);
        MutationVisual.Sound(point,120,Run);
    }
    public void Finish()
    {
        if(!Live)return;Live=false;
        if(receiver!=null)receiver.GetComponent<Collider>().enabled=false;
        if(Kind==MutationWorldKind.ReturningBlade)
        {
            if(Run!=null)Run.BladeReturned();
            if(bladeRenderers!=null)foreach(var renderer in bladeRenderers)if(renderer!=null)renderer.forceRenderingOff=false;
        }
        gameObject.SetActive(false);Destroy(gameObject);
    }
    void OnDestroy(){if(Live&&Kind==MutationWorldKind.ReturningBlade&&Run!=null)Run.BladeReturned();}
}

public sealed class MutationObject : MonoBehaviour,IRaycastHitHandler
{
    public MutationWorldEffect Effect;
    public bool Saturated=>Effect==null||!Effect.Live||Effect.Kind==MutationWorldKind.Coil&&Effect.Captured>=3;
    public void HandleRaycastHit(RaycastHit hit,float amount){if(amount>0&&Effect!=null)Effect.ShotByPlayer();}
    public bool CaptureEnemyShot()=>Effect!=null&&Effect.Capture();
    public static bool EnemyRay(Vector3 start,Vector3 direction,out RaycastHit result,float distance,int mask,bool melee=false)
    {
        if(!Physics.Raycast(start,direction,out result,distance,mask,QueryTriggerInteraction.Ignore))return false;
        var first=result.collider.GetComponentInParent<MutationObject>();
        if(first==null||!first.Saturated&&!melee)return true;
        var hits=Physics.RaycastAll(start,direction,distance,mask,QueryTriggerInteraction.Ignore);
        Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
        foreach(var hit in hits)
        {
            var device=hit.collider.GetComponentInParent<MutationObject>();
            if(device!=null&&(device.Saturated||melee))continue;
            result=hit;return true;
        }
        result=default;return false;
    }
}
