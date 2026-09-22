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
    float phaseTime,pathClock;
    bool resolved;
    Vector3 lockedForward;
    PlayerStateMachine victim;
    NormalEnemyStateMachine owner;
    NavMeshAgent agent;
    float Duration=>Phase==MeleeRobotPhase.Windup?Mathf.Max(.1f,windupSeconds):
        Phase==MeleeRobotPhase.Strike?Mathf.Max(.08f,strikeSeconds):Mathf.Max(.1f,recoverySeconds);

    void Awake()
    {
        owner=GetComponent<NormalEnemyStateMachine>();agent=GetComponent<NavMeshAgent>();
        if(agent!=null) { agent.updateRotation=false;agent.stoppingDistance=Mathf.Max(.6f,engageDistance-.12f); }
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
        Vector3 delta=victim.transform.position-transform.position;delta.y=0;
        if(delta.sqrMagnitude>.001f)
            transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(delta),420*Time.deltaTime);
        if(delta.magnitude<=engageDistance&&Vector3.Angle(transform.forward,delta)<20&&ClearContact(victim))
        {
            agent.isStopped=true;agent.ResetPath();lockedForward=transform.forward;
            resolved=false;phaseTime=0;AttacksStarted++;SetPhase(MeleeRobotPhase.Windup);return;
        }
        agent.isStopped=false;pathClock-=Time.deltaTime;
        if(pathClock<=0)
        {
            pathClock=.12f;
            if(NavMesh.SamplePosition(victim.transform.position,out var sample,2.5f,agent.areaMask))
                agent.SetDestination(sample.position);
        }
    }
    void AdvanceAttack(float dt)
    {
        agent.isStopped=true;
        // Direction commits at the beginning of anticipation; dodging is never followed by a snapping fist.
        transform.rotation=Quaternion.LookRotation(lockedForward);
        phaseTime+=dt;
        for(int i=0;i<3;i++)
        {
            if(Phase==MeleeRobotPhase.Strike&&!resolved&&phaseTime>=Duration*.45f)ResolvePunch();
            if(phaseTime<Duration)return;
            phaseTime-=Duration;
            if(Phase==MeleeRobotPhase.Windup)SetPhase(MeleeRobotPhase.Strike);
            else if(Phase==MeleeRobotPhase.Strike)SetPhase(MeleeRobotPhase.Recover);
            else { phaseTime=0;pathClock=0;SetPhase(MeleeRobotPhase.Chase);return; }
        }
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
        victim=null;resolved=true;phaseTime=0;SetPhase(MeleeRobotPhase.Dead);
    }
}
