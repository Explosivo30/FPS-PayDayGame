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
    bool strikingDecoy;
    Vector3 lockedForward;
    float chargeTravel;bool chargeBlocked;
    CombatHazardBudget.Lease primaryLease;
    public float ChargeTravel=>chargeTravel;
    public bool ChargeBlocked=>chargeBlocked;
    public Vector3 LockedForward=>lockedForward;
    public int CoverImpacts { get; private set; }
    public string ChargeStopReason { get; private set; }
    public bool StopChargeAtPlayer { get; set; }
    public void ConfigureVeteran()
    {
        var p=TowerSession.Instance?.Threat?.Profile??ThreatProfile.Basic;
        windupSeconds=Mathf.Max(.8f,Mathf.Max(p.minimumMeleeWarning,p.chargeWarning));
        recoverySeconds=Mathf.Max(1,Mathf.Max(p.minimumMeleeRecovery,p.chargeRecovery));
    }
    bool TryBeginAttack()
    {
        if(owner.IsVeteran&&!CombatHazardBudget.TryReservePrimary(owner,out primaryLease))return false;
        if(SquadManager.Instance==null||SquadManager.Instance.TryAttack(owner))return true;
        primaryLease?.Dispose();primaryLease=null;return false;
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
        if(Phase!=MeleeRobotPhase.Chase)
        {
            if(owner.IsVeteran&&(primaryLease==null||!primaryLease.IsValid)) { CancelAttack(false);return; }
            AdvanceAttack(Time.deltaTime);return;
        }
        owner.Perception.Tick();
        Vector3 delta=owner.Perception.LastKnown-transform.position;delta.y=0;
        bool permit=SquadManager.Instance==null||SquadManager.Instance.CanAttack(owner);
        float entry=engageDistance+(owner.IsVeteran?owner.Perception.Profile.chargeDistance:0);
        if(delta.sqrMagnitude>.001f)
            transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(delta),420*Time.deltaTime);
        bool contact=owner.Perception.UsingDecoy||ClearContact(victim);
        if(owner.Perception.Visible&&permit&&delta.magnitude<=entry&&Vector3.Angle(transform.forward,delta)<20&&contact
            &&TryBeginAttack())
        {
            agent.isStopped=true;agent.ResetPath();SquadManager.Instance?.ReleaseDestination(owner);lockedForward=transform.forward;
            strikingDecoy=owner.Perception.UsingDecoy;
            resolved=false;chargeTravel=0;chargeBlocked=false;ChargeStopReason="";phaseTime=0;AttacksStarted++;
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
            else
            {
                phaseTime=0;SquadManager.Instance?.ReleaseAttack(owner);
                primaryLease?.Dispose();primaryLease=null;SetPhase(MeleeRobotPhase.Chase);return;
            }
        }
    }
    void AdvanceCharge(float requestedDistance)
    {
        if(chargeBlocked)return;
        float maximum=owner.Perception.Profile.chargeDistance;
        float step=Mathf.Min(maximum-chargeTravel,Mathf.Max(0,requestedDistance));
        if(step<=0)return;
        Vector3 start=transform.position;
        int blockers=owner.layerMask;
        // A scene may stop the chassis at the player while the normal contact check resolves the single punch.
        // Check the screen before the NavMesh edge: its fixed plinth remains non-walkable.
        float radius=StopChargeAtPlayer?Mathf.Max(.32f,agent.radius):.32f;
        float lower=radius+.08f,upper=StopChargeAtPlayer?Mathf.Max(lower,agent.height-radius-.08f):1.5f;
        var hits=Physics.CapsuleCastAll(start+Vector3.up*lower,start+Vector3.up*upper,radius,lockedForward,step+.08f,blockers,QueryTriggerInteraction.Ignore);
        Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
        bool playerContact=false;
        foreach(var hit in hits)
        {
            if(hit.collider.GetComponentInParent<NormalEnemyStateMachine>()!=null)continue;
            if(hit.collider.GetComponentInParent<MutationObject>()!=null)continue;
            if(hit.collider.GetComponentInParent<MutationObject>()!=null)continue;
            if(hit.collider.GetComponentInParent<PlayerStateMachine>()!=null)
            {
                if(!StopChargeAtPlayer)continue;
                step=Mathf.Clamp(hit.distance-.04f,0,step);playerContact=true;break;
            }
            var screen=hit.collider.GetComponentInParent<ArenaBreakableCover>();
            var structure=FoundryStructure.From(hit.collider);
            if((screen!=null&&!screen.IsBroken&&screen.TryBreakFromCharge(hit.point,lockedForward)) ||
                (structure!=null&&structure.ApplyImpact(hit.point,lockedForward,120)))
            {
                CoverImpacts++;resolved=true;ContactsResolved++;PunchResolved?.Invoke(true);
            }
            ChargeStopReason="Collider: "+hit.collider.name;chargeBlocked=true;return;
        }
        Vector3 next=start+lockedForward*step;
        if(NavMesh.Raycast(start,next,out var edge,agent.areaMask)||!NavMesh.SamplePosition(next,out var ground,.25f,agent.areaMask)||Mathf.Abs(ground.position.y-start.y)>.25f)
        {ChargeStopReason="NavMesh edge or height";chargeBlocked=true;return;}
        agent.Move(ground.position-start);chargeTravel+=Vector3.Distance(start,transform.position);
        if(playerContact){ChargeStopReason="Player contact";chargeBlocked=true;}
    }
    void ResolvePunch()
    {
        resolved=true;ContactsResolved++;
        if(strikingDecoy){PunchResolved?.Invoke(false);return;}
        // The structural plinth stops the chassis before the raised fist reaches the
        // screen above it. Resolve that existing punch reach independently of travel.
        if(owner.IsVeteran&&TryPunchScreen()) { PunchResolved?.Invoke(true);return; }
        bool hit=false;
        if(victim!=null&&!victim.IsDead)
        {
            Vector3 flat=victim.transform.position-transform.position;flat.y=0;
            hit=flat.magnitude<=reach&&Vector3.Angle(lockedForward,flat)<=halfAngle&&ClearContact(victim);
            if(hit)victim.TakeDamage(punchDamage,transform.position,"Puñetazo de robot de seguridad");
        }
        PunchResolved?.Invoke(hit);
    }
    bool TryPunchScreen()
    {
        Vector3 origin=transform.position+Vector3.up*1.5f;
        var hits=Physics.SphereCastAll(origin,.15f,lockedForward,reach,owner.layerMask,QueryTriggerInteraction.Ignore);
        Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
        foreach(var hit in hits)
        {
            if(hit.collider.GetComponentInParent<NormalEnemyStateMachine>()!=null)continue;
            // A solid wall or a player in front wins; the fist never searches through it.
            var screen=hit.collider.GetComponentInParent<ArenaBreakableCover>();
            if(screen==null)
            {
                var structure=FoundryStructure.From(hit.collider);
                if(structure==null||!structure.ApplyImpact(hit.point,lockedForward,120))return false;
                CoverImpacts++;return true;
            }
            if(screen==null||screen.IsBroken)return false;
            if(!screen.TryBreakFromCharge(hit.point,lockedForward))return false;
            CoverImpacts++;return true;
        }
        return false;
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
        return MutationObject.EnemyRay(origin,direction.normalized,out var hit,direction.magnitude+.08f,
            owner.layerMask,true)&&hit.collider.GetComponentInParent<PlayerStateMachine>()==player;
    }
    void SetPhase(MeleeRobotPhase phase) { Phase=phase;PhaseChanged?.Invoke(phase); }
    public void CancelOnDeath()
    {
        CancelAttack(true);
    }
    void CancelAttack(bool dead)
    {
        victim=null;resolved=true;phaseTime=0;primaryLease?.Dispose();primaryLease=null;
        SquadManager.Instance?.ReleaseAttack(owner);SetPhase(dead?MeleeRobotPhase.Dead:MeleeRobotPhase.Chase);
    }
    void OnDisable() { CancelAttack(owner!=null&&owner.IsDead); }
}
