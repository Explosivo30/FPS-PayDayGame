using System;
using UnityEngine;
using UnityEngine.AI;

public enum MeleeRobotPhase { Chase, Windup, Strike, Recover, Dead }

/// <summary>Optional combat role; health, hit feedback and wave accounting stay on the shared enemy.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NormalEnemyStateMachine))]
public class MeleeRobotCombat : MonoBehaviour
{
    [Header("Punch (seconds / metres)")]
    [Min(.1f)] public float windupSeconds=.62f;
    [Min(.08f)] public float strikeSeconds=.16f;
    [Min(.1f)] public float recoverySeconds=.85f;
    [Min(.1f)] public float engageDistance=1.65f,reach=1.85f;
    [Range(10,90)] public float halfAngle=42;
    [Min(1)] public float punchDamage=18;
    [Header("Wave balance")]
    [Min(.1f)] public float healthMultiplier=.85f,damageMultiplier=1.5f,speedMultiplier=1.12f;
    [Min(1)] public float maximumSpeed=4.6f;
    public MeleeRobotPhase Phase { get; private set; }
    public float PhaseProgress=>Mathf.Clamp01(phaseTime/Duration);
    public int AttacksStarted { get; private set; }
    public int ContactsResolved { get; private set; }
    public event Action<MeleeRobotPhase> PhaseChanged;
    public event Action<bool> PunchResolved;
    float phaseTime;
    bool resolved;
    Vector3 lockedForward;
    float chargeTravel;bool chargeBlocked;
    public float ChargeTravel=>chargeTravel;
    public void ConfigureVeteran()
    {
        var p=TowerSession.Instance?.Threat?.Profile??ThreatProfile.Basic;
        windupSeconds=Mathf.Max(p.minimumMeleeWarning,p.chargeWarning);recoverySeconds=Mathf.Max(p.minimumMeleeRecovery,p.chargeRecovery);
    }
    PlayerStateMachine victim;
    NormalEnemyStateMachine owner;
    NavMeshAgent agent;
    float Duration=>Phase==MeleeRobotPhase.Windup?Mathf.Max(.1f,windupSeconds):
        Phase==MeleeRobotPhase.Strike?Mathf.Max(.08f,strikeSeconds):Mathf.Max(.1f,recoverySeconds);

    void Awake()
    {
        owner=GetComponent<NormalEnemyStateMachine>();agent=GetComponent<NavMeshAgent>();
        if(agent!=null) { agent.updateRotation=false;agent.stoppingDistance=Mathf.Max(.45f,engageDistance-.5f); }
    }
    public void ConfigureDamage(float baseDamage) { punchDamage=Mathf.Max(1,baseDamage*damageMultiplier); }
    public void TickCombat()
    {
        if(!isActiveAndEnabled||owner.IsDead||Phase==MeleeRobotPhase.Dead)return;
        if(agent==null||!agent.enabled||!agent.isOnNavMesh)return;
        if(WeaponAction.CombatPaused) { agent.isStopped=true;return; }
        if(victim==null||victim.IsDead)
        {
            var players=GameManager.Instance?.GetPlayerTransforms();
            victim=players!=null&&players.Count>0?players[0].GetComponent<PlayerStateMachine>():null;
            if(victim==null||victim.IsDead) { agent.isStopped=true;return; }
        }
        if(Phase!=MeleeRobotPhase.Chase) { AdvanceAttack(Time.deltaTime);return; }
        owner.Perception.Tick();
        Vector3 delta=owner.Perception.LastKnown-transform.position;delta.y=0;
        bool permit=SquadManager.Instance==null||SquadManager.Instance.CanAttack(owner);
        float entry=engageDistance+(owner.IsVeteran?owner.Perception.Profile.chargeDistance:0);
        if(delta.sqrMagnitude>.001f)
            transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(delta),420*Time.deltaTime);
        if(owner.Perception.Visible&&permit&&delta.magnitude<=entry&&Vector3.Angle(transform.forward,delta)<20&&ClearContact(victim)
            &&(SquadManager.Instance==null||SquadManager.Instance.TryAttack(owner)))
        {
            agent.isStopped=true;agent.ResetPath();SquadManager.Instance?.ReleaseDestination(owner);lockedForward=transform.forward;
            resolved=false;chargeTravel=0;chargeBlocked=false;phaseTime=0;AttacksStarted++;
            GetComponent<EnemySignals>()?.Attack(owner.IsVeteran);SetPhase(MeleeRobotPhase.Windup);return;
        }
        owner.Navigation.Tick(true,permit);
    }
    void AdvanceAttack(float dt)
    {
        agent.isStopped=true;
        // Direction commits at the beginning of anticipation; dodging is never followed by a snapping fist.
        transform.rotation=Quaternion.LookRotation(lockedForward);
        phaseTime+=dt;
        for(int i=0;i<3;i++)
        {
            if(owner.IsVeteran&&Phase==MeleeRobotPhase.Strike&&!resolved)AdvanceCharge(Mathf.Min(1,phaseTime/(Duration*.45f))*owner.Perception.Profile.chargeDistance-chargeTravel);
            if(Phase==MeleeRobotPhase.Strike&&!resolved&&phaseTime>=Duration*.45f)ResolvePunch();
            if(phaseTime<Duration)return;
            phaseTime-=Duration;
            if(Phase==MeleeRobotPhase.Windup)SetPhase(MeleeRobotPhase.Strike);
            else if(Phase==MeleeRobotPhase.Strike)SetPhase(MeleeRobotPhase.Recover);
            else { phaseTime=0;SquadManager.Instance?.ReleaseAttack(owner);SetPhase(MeleeRobotPhase.Chase);return; }
        }
    }
    void AdvanceCharge(float requestedDistance)
    {
        if(chargeBlocked)return;
        float maximum=owner.Perception.Profile.chargeDistance;
        float step=Mathf.Min(maximum-chargeTravel,Mathf.Max(0,requestedDistance));
        if(step<=0)return;
        Vector3 start=transform.position,next=start+lockedForward*step;
        if(NavMesh.Raycast(start,next,out var edge,agent.areaMask)||!NavMesh.SamplePosition(next,out var ground,.25f,agent.areaMask)||Mathf.Abs(ground.position.y-start.y)>.25f)
        {chargeBlocked=true;return;}
        int blockers=owner.layerMask;
        // Ignore robots and the player here; the normal contact check resolves the single punch.
        var hits=Physics.CapsuleCastAll(start+Vector3.up*.4f,start+Vector3.up*1.5f,.32f,lockedForward,step+.08f,blockers,QueryTriggerInteraction.Ignore);
        foreach(var hit in hits)if(hit.collider.GetComponentInParent<PlayerStateMachine>()==null&&hit.collider.GetComponentInParent<NormalEnemyStateMachine>()==null)
        {chargeBlocked=true;return;}
        agent.Move(ground.position-start);chargeTravel+=Vector3.Distance(start,transform.position);
    }
    void ResolvePunch()
    {
        resolved=true;ContactsResolved++;
        bool hit=false;
        if(victim!=null&&!victim.IsDead)
        {
            Vector3 flat=victim.transform.position-transform.position;flat.y=0;
            hit=flat.magnitude<=reach&&Vector3.Angle(lockedForward,flat)<=halfAngle&&ClearContact(victim);
            if(hit)victim.TakeDamage(punchDamage,transform.position,"Puñetazo de robot de seguridad");
        }
        PunchResolved?.Invoke(hit);
    }
    bool ClearContact(PlayerStateMachine player)
    {
        var capsule=player.GetComponent<CharacterController>();
        if(capsule==null||!capsule.enabled)return false;
        Vector3 origin=transform.position+Vector3.up*1.12f;
        Vector3 contact=capsule.ClosestPoint(origin);
        if(Mathf.Abs(contact.y-origin.y)>.65f)return false;
        Vector3 direction=contact-origin;
        if(direction.sqrMagnitude<.0001f)return true;
        return Physics.Raycast(origin,direction.normalized,out var hit,direction.magnitude+.08f,
            owner.layerMask,QueryTriggerInteraction.Ignore)&&hit.collider.GetComponentInParent<PlayerStateMachine>()==player;
    }
    void SetPhase(MeleeRobotPhase phase) { Phase=phase;PhaseChanged?.Invoke(phase); }
    public void CancelOnDeath()
    {
        victim=null;resolved=true;phaseTime=0;SquadManager.Instance?.ReleaseAttack(owner);SetPhase(MeleeRobotPhase.Dead);
    }
}
