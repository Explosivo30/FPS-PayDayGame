using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class MutationRun
{
    sealed class Mark { public float Until;public MutationWorldEffect Visual; }
    readonly Dictionary<NormalEnemyStateMachine,Mark> fuses=new Dictionary<NormalEnemyStateMachine,Mark>();
    readonly Dictionary<NormalEnemyStateMachine,Mark> cuts=new Dictionary<NormalEnemyStateMachine,Mark>();
    readonly Dictionary<int,long> hitStamps=new Dictionary<int,long>();
    readonly Dictionary<BaseGun,int> forkHits=new Dictionary<BaseGun,int>();
    NormalEnemyStateMachine hammerTarget;
    int hammerHits,hammerLastShot;
    BaseGun hammerGun;
    bool carrierArmed;
    public int HammerHits=>hammerHits;

    public NormalEnemyStateMachine[] Enemies(Vector3 point,float radius)
    {
        var scene=session.CurrentFloor!=null?session.CurrentFloor.gameObject.scene:Player.gameObject.scene;
        return UnityEngine.Object.FindObjectsByType<NormalEnemyStateMachine>(FindObjectsSortMode.None)
            .Where(e=>!e.IsDead&&e.gameObject.scene==scene)
            .Where(e=>Vector3.Distance(e.AimPoint,point)<=radius).OrderBy(e=>(e.AimPoint-point).sqrMagnitude).ToArray();
    }
    public bool Visible(Vector3 origin,Vector3 destination)
    {
        Vector3 delta=destination-origin;
        if(delta.sqrMagnitude<.001f)return true;
        foreach(var hit in Physics.RaycastAll(origin,delta.normalized,delta.magnitude,1<<3,QueryTriggerInteraction.Ignore))
            if(hit.collider.GetComponentInParent<MutationObject>()==null)return false;
        return true;
    }
    public void OnDirectHit(BaseGun gun,NormalEnemyStateMachine enemy,RaycastHit hit)
    {
        if(enemy==null||Player==null||Player.IsDead||WeaponAction.CombatPaused)return;
        long stamp=((long)gun.GetInstanceID()<<32)^(uint)gun.ShotSequence;
        int id=enemy.GetInstanceID();
        if(hitStamps.TryGetValue(id,out long previous)&&previous==stamp)return;
        hitStamps[id]=stamp;DirectHits++;
        MutationEnemyControl.For(enemy,this).IgnoreDecoy();
        if(hammerTarget!=enemy){hammerTarget=enemy;hammerHits=0;if(hammerMark!=null)hammerMark.Finish();hammerMark=null;}
        hammerHits=Mathf.Min(3,hammerHits+1);hammerGun=gun;hammerLastShot=gun.ShotSequence;
        if(Has(MutationKind.WorkHammer)&&!enemy.IsDead)
        {
            if(hammerMark==null){hammerMark=Spawn(MutationWorldKind.HammerMark,enemy.AimPoint,60,gun,0);if(hammerMark!=null)hammerMark.Target=enemy;}
            if(hammerMark!=null)hammerMark.Transfers=hammerHits;
        }
        if(Has(MutationKind.DoubleFuse))
        {
            if(fuses.TryGetValue(enemy,out var fuse)&&fuse.Until>=Clock)
            {RemoveMark(fuses,enemy);Blast(enemy.AimPoint,2.6f,Damage(MutationKind.DoubleFuse,40),4,gun);}
            else if(!enemy.IsDead)SetMark(fuses,enemy,3,MutationWorldKind.FuseMark,gun);
        }
        if(cuts.TryGetValue(enemy,out var cut)&&cut.Until>=Clock)
        {
            RemoveMark(cuts,enemy);
            Vector3 point=enemy.AimPoint;
            Deal(enemy,Damage(MutationKind.OpeningCut,55),gun);
            Blast(point,2.3f,Damage(MutationKind.OpeningCut,55)*.6f,3,gun,enemy);
        }
        if(gun is MachineGun&&Has(MutationKind.Fork))
        {
            int n=(forkHits.TryGetValue(gun,out int count)?count:0)+1;forkHits[gun]=n;
            if(n%4==0)
            {
                int used=0;
                foreach(var other in Enemies(enemy.AimPoint,5))
                {
                    if(other==enemy||!Visible(hit.point,other.AimPoint))continue;
                    MutationVisual.Line(hit.point,other.AimPoint,new Color(1,.55f,.1f),.07f,.22f,this);
                    Deal(other,gun.damage*.5f,gun);if(++used>=2)break;
                }
            }
        }
        if(Has(MutationKind.Carrier))
        {
            if(carrier!=null&&carrier.Live&&carrier.Target==enemy&&carrier.Transfers<2)
            {
                var next=Enemies(enemy.AimPoint,5).FirstOrDefault(e=>e!=enemy&&Visible(enemy.AimPoint,e.AimPoint));
                if(next!=null)
                {MutationVisual.Line(enemy.AimPoint,next.AimPoint,Color.yellow,.09f,.2f,this);carrier.Target=next;carrier.Transfers++;}
            }
            else if(gun is Pistol&&carrierArmed&&(carrier==null||!carrier.Live))
            {
                carrierArmed=false;
                carrier=Spawn(MutationWorldKind.Carrier,enemy.AimPoint,2.5f,gun,Damage(MutationKind.Carrier,60));
                if(carrier!=null)carrier.Target=enemy;
            }
        }
    }
    public void OnKnifeHit(Knife knife,NormalEnemyStateMachine enemy)
    {
        if(enemy==null||enemy.IsDead||!Has(MutationKind.OpeningCut))return;
        foreach(var key in cuts.Keys.ToArray())RemoveMark(cuts,key);
        SetMark(cuts,enemy,2,MutationWorldKind.CutMark,knife);
        MutationEnemyControl.For(enemy,this).IgnoreDecoy();
    }
    void SetMark(Dictionary<NormalEnemyStateMachine,Mark> marks,NormalEnemyStateMachine target,float seconds,MutationWorldKind kind,Component source)
    {
        RemoveMark(marks,target);
        var fx=Spawn(kind,target.AimPoint,seconds,source,0);if(fx==null)return;
        fx.Target=target;marks[target]=new Mark{Until=Clock+seconds,Visual=fx};
    }
    void RemoveMark(Dictionary<NormalEnemyStateMachine,Mark> marks,NormalEnemyStateMachine target)
    {if(marks.TryGetValue(target,out var mark)){if(mark.Visual!=null)mark.Visual.Finish();marks.Remove(target);}}
    void PruneMarks()
    {
        foreach(var pair in fuses.ToArray())if(pair.Key==null||pair.Key.IsDead||Clock>pair.Value.Until)RemoveMark(fuses,pair.Key);
        foreach(var pair in cuts.ToArray())if(pair.Key==null||pair.Key.IsDead||Clock>pair.Value.Until)RemoveMark(cuts,pair.Key);
    }
    public void Deal(NormalEnemyStateMachine target,float damage,Component source)
    {
        if(target==null||target.IsDead||damage<=0||Player==null||Player.IsDead||WeaponAction.CombatPaused)return;
        if(source==null)source=session.weapons.Weapons.FirstOrDefault(w=>w is BaseGun||w is Knife);
        Vector3 point=target.AimPoint;
        target.TakeDamage(damage);DerivedHits++;
        if(target.IsDead)CombatFeedback.ReportPlayerKill(target,source,false);
        target.GetComponent<RobotPresentation>()?.SetHealth(target.Health/Mathf.Max(1,target.maxHealth));
        CombatFeedback.ReportHit();
        CombatFeedback.ReportImpact(new CombatImpact(target.GetInstanceID(),point,Vector3.up,(point-Player.transform.position).normalized,damage,target.IsDead,false));
        MutationVisual.Burst(point,new Color(1,.55f,.15f),.18f,this);
    }
    public int Blast(Vector3 point,float radius,float damage,int maximum,Component source,NormalEnemyStateMachine excluded=null)
    {
        int count=0;
        MutationVisual.Burst(point,new Color(1,.3f,.04f),radius*.55f,this);
        foreach(var enemy in Enemies(point,radius))
        {
            if(enemy==excluded||!Visible(point,enemy.AimPoint))continue;
            Deal(enemy,damage,source);if(++count>=maximum)break;
        }
        return count;
    }
    void Funnel(BaseGun gun)
    {
        Vector3 origin=Player.transform.position;
        Vector3 forward=Vector3.ProjectOnPlane(View.transform.forward,Vector3.up).normalized;
        foreach(var enemy in Enemies(origin+Vector3.up,8))
        {
            Vector3 relative=Vector3.ProjectOnPlane(enemy.transform.position-origin,Vector3.up);
            if(Vector3.Angle(relative,forward)>32||!Visible(View.transform.position,enemy.AimPoint))continue;
            Vector3 goal=origin+forward*Vector3.Dot(relative,forward);goal.y=enemy.transform.position.y;
            MutationEnemyControl.For(enemy,this).MoveToward(goal,enemy.IsVeteran?.4f:1.5f);
        }
        MutationVisual.Cone(origin+Vector3.up*.25f,forward,4,new Color(1,.55f,.1f),this);
    }
    void Brake()
    {
        Vector3 forward=Player.transform.forward;
        foreach(var enemy in Enemies(Player.transform.position+Vector3.up,3.1f))
        {
            Vector3 delta=enemy.transform.position-Player.transform.position;delta.y=0;
            if(Vector3.Angle(forward,delta)>75||!Visible(Player.transform.position+Vector3.up,enemy.AimPoint))continue;
            MutationEnemyControl.For(enemy,this).MoveToward(enemy.transform.position+delta.normalized,enemy.IsVeteran?.3f:1);
        }
        MutationVisual.Cone(Player.transform.position+Vector3.up*.15f,forward,3,Color.cyan,this);
    }
    void ThrowBlade(Knife knife)
    {
        if(BladeInFlight||!knife.GetComponent<WeaponAction>().RequestFire())return;
        knife.GetComponent<WeaponAction>().BeginAttack(knife.attackCooldown);
        knife.GetComponent<WeaponAction>().NotifyShot();BladeInFlight=true;
        var blade=Spawn(MutationWorldKind.ReturningBlade,ProjectileOrigin(),3,knife,knife.damage);
        if(blade==null){BladeInFlight=false;return;}
        blade.Direction=View.transform.forward;
    }
    public void BladeReturned(){BladeInFlight=false;}
    public void SawRecovered(){SawReady=true;SawCharge=3;}
    public static bool TryDecoy(NormalEnemyStateMachine enemy,out Vector3 point)
    {
        point=Vector3.zero;var run=TowerSession.Instance?.GetComponent<MutationRun>();
        if(run==null||run.Player==null||run.Player.IsDead)return false;
        var control=enemy.GetComponent<MutationEnemyControl>();if(control!=null&&control.DecoyIgnored)return false;
        var decoy=run.effects.FirstOrDefault(e=>e!=null&&e.Live&&e.Kind==MutationWorldKind.Decoy);
        if(decoy==null||Vector3.Distance(enemy.transform.position,decoy.transform.position)>enemy.detectionRange)return false;
        point=decoy.transform.position;
        return run.Visible(enemy.AimPoint,point+Vector3.up*.6f);
    }
}
