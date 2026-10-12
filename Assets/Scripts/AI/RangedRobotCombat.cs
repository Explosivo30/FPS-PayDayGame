using System.Collections;
using UnityEngine;
public sealed class RangedRobotCombat : MonoBehaviour
{
    NormalEnemyStateMachine owner;EnemyPerception senses;EnemyNavigation navigation;RobotPresentation presentation;LineRenderer tracer;
    float damage,accuracy,spread,cooldownDuration,warning,range,cooldown;int shotsSinceMove;
    public bool IsAttacking { get; private set; }
    public int ShotsFired { get; private set; }
    public Vector3 CommittedTarget { get; private set; }
    public void Configure(float hitDamage,float precision,float spreadAngle,float interval,float anticipation,float attackRange)
    {
        owner=GetComponent<NormalEnemyStateMachine>();senses=GetComponent<EnemyPerception>();navigation=GetComponent<EnemyNavigation>();
        presentation=GetComponent<RobotPresentation>();tracer=GetComponent<LineRenderer>();
        damage=hitDamage;accuracy=precision;spread=spreadAngle;cooldownDuration=interval;warning=anticipation;range=attackRange;
        cooldown=Random.Range(.6f,1.5f);
    }
    public void Balance(float hitDamage,float precision){damage=hitDamage;accuracy=precision;}
    public void Tick()
    {
        if(owner.IsDead||WeaponAction.CombatPaused){navigation.Stop();return;}
        senses.Tick();
        presentation?.SetTarget(senses.LastKnown+Vector3.up*.6f);
        if(IsAttacking)return;
        navigation.Tick(false,SquadManager.Instance==null||SquadManager.Instance.CanAttack(owner));cooldown-=Time.deltaTime;
        float distance=Vector3.Distance(transform.position,senses.LastKnown);
        if(!senses.Visible||distance>range||distance<senses.Profile.retreatDistance||cooldown>0)return;
        if(SquadManager.Instance!=null&&!SquadManager.Instance.TryAttack(owner))return;
        StartCoroutine(Fire());
    }
    IEnumerator Fire()
    {
        IsAttacking=true;navigation.Stop();CommittedTarget=senses.LastKnown+Vector3.up*.6f;
        var p=senses.Profile;float lead=owner.IsVeteran?Mathf.Max(p.minimumRangedWarning,p.burstWarning):Mathf.Max(p.minimumRangedWarning,warning);
        presentation?.Telegraph(lead);GetComponent<EnemySignals>()?.Attack(owner.IsVeteran);
        yield return new WaitForSeconds(lead);
        int count=owner.IsVeteran?2:1;
        for(int i=0;i<count;i++)
        {
            while(WeaponAction.CombatPaused&&!owner.IsDead)yield return null;
            if(owner.IsDead)break;
            float nextShotAt=Time.time+p.burstInterval;
            Vector3 origin=presentation!=null?presentation.MuzzlePosition:owner.AimPoint;
            Vector3 direction=(CommittedTarget-origin).normalized;float variance=(1-accuracy)*spread;
            direction=Quaternion.Euler(Random.Range(-variance,variance),Random.Range(-variance,variance),0)*direction;
            Vector3 end=origin+direction*range;
            if(Physics.Raycast(origin,direction,out var hit,range,owner.layerMask,QueryTriggerInteraction.Ignore))
            {end=hit.point;hit.collider.GetComponentInParent<PlayerStateMachine>()?.TakeDamage(damage,origin,owner.IsVeteran?"Ráfaga de centinela veterano":"Disparo de robot centinela");}
            ShotsFired++;shotsSinceMove++;presentation?.Fire();
            if(tracer!=null){tracer.SetPosition(0,origin);tracer.SetPosition(1,end);tracer.enabled=true;}
            yield return new WaitForSeconds(.07f);
            if(tracer!=null)tracer.enabled=false;
            if(i+1<count)while(Time.time<nextShotAt)yield return null;
        }
        cooldown=cooldownDuration;IsAttacking=false;SquadManager.Instance?.ReleaseAttack(owner);
        if(owner.IsVeteran||shotsSinceMove>=2){shotsSinceMove=0;navigation.Reposition();}
    }
    public void Cancel()
    {
        StopAllCoroutines();IsAttacking=false;if(tracer!=null)tracer.enabled=false;
        if(owner!=null)SquadManager.Instance?.ReleaseAttack(owner);
    }
    void OnDisable(){Cancel();}
}
