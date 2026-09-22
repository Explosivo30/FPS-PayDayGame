using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class NormalEnemyStateMachine : StateMachine,IDamageable,IWeapon,ISquadMember,IHitReactable
{
    public NavMeshAgent agent;
    public Vector3 _currentFormationPosition;
    [SerializeField] private LayerMask enemyLayer=1<<6;
    public float separationDistance=1.5f,separationStrength=2;
    public float maxHealth=100,detectionRange=28,attackRange=24;
    [SerializeField,Range(0,1)] private float accuracy=.8f;
    [SerializeField] private float maxSpreadAngle=5;
    [SerializeField] private Color fullHealthColor=new Color(1,.5f,0),zeroHealthColor=Color.red;
    public LayerMask layerMask=~(1<<6|1<<8);
    [SerializeField] private float duration=.07f,shootCooldown=1.8f;
    [SerializeField] private float shotDamage=12,anticipation=.25f;
    private float health,cooldown,pathClock;
    private LineRenderer tracer;
    private RobotPresentation presentation;
    private MeleeRobotCombat meleeCombat;
    public bool IsMelee => meleeCombat!=null;
    private bool attacking;
    public bool IsDead { get; private set; }
    bool playerKillClaimed;
    public bool TryClaimPlayerKill()
    { if(!IsDead||playerKillClaimed)return false;playerKillClaimed=true;return true; }
    public float Health => health;
    public bool IsAutomatic=>true;
    public SwayData swayData=>null;
    public Transform Transform=>transform;
    public Vector3 AimPoint=>transform.position+Vector3.up*.9f;
    private void Awake()
    {
        agent=GetComponent<NavMeshAgent>(); tracer=GetComponent<LineRenderer>(); presentation=GetComponent<RobotPresentation>();
        meleeCombat=GetComponent<MeleeRobotCombat>();
        health=maxHealth; cooldown=Random.Range(.6f,1.5f);
        if(tracer!=null) tracer.enabled=false;
        if(meleeCombat==null)SquadManager.Instance?.Register(this);
        SwitchState(new IdleNormalEnemyState(this));
    }
    private void OnDestroy() { SquadManager.Instance?.Unregister(this); }
    public void TickCombat()
    {
        if(IsDead) return;
        if(meleeCombat!=null) { meleeCombat.TickCombat();return; }
        if(WeaponAction.CombatPaused) return;
        var players=GameManager.Instance?.GetPlayerTransforms();
        if(players==null||players.Count==0||agent==null||!agent.isOnNavMesh) return;
        Transform player=players[0]; Vector3 target=player.position+Vector3.up*.6f;
        float distance=Vector3.Distance(transform.position,player.position);
        Vector3 muzzle=presentation!=null?presentation.MuzzlePosition:AimPoint;
        bool visible=Physics.Linecast(muzzle,target,out var hit,layerMask,QueryTriggerInteraction.Ignore)&&hit.collider.GetComponentInParent<PlayerStateMachine>()!=null;
        presentation?.SetTarget(target);
        pathClock-=Time.deltaTime;
        if(pathClock<=0)
        {
            pathClock=.2f;
            agent.isStopped=visible&&distance<13;
            if(!agent.isStopped)
            {
                Vector3 desired=_currentFormationPosition;
                if(desired.sqrMagnitude<.01f) desired=player.position;
                if(NavMesh.SamplePosition(desired,out var sample,3,NavMesh.AllAreas)) agent.SetDestination(sample.position);
                else agent.SetDestination(player.position);
            }
        }
        cooldown-=Time.deltaTime;
        if(visible&&distance<attackRange&&cooldown<=0&&!attacking) StartCoroutine(Shoot(target));
    }
    private IEnumerator Shoot(Vector3 target)
    {
        attacking=true; presentation?.Telegraph(anticipation);
        yield return new WaitForSeconds(anticipation);
        if(IsDead) yield break;
        Vector3 muzzle=presentation!=null?presentation.MuzzlePosition:AimPoint;
        Vector3 direction=(target-muzzle).normalized;
        float spread=(1-accuracy)*maxSpreadAngle;
        direction=Quaternion.Euler(Random.Range(-spread,spread),Random.Range(-spread,spread),0)*direction;
        Vector3 end=muzzle+direction*attackRange;
        if(Physics.Raycast(muzzle,direction,out var hit,attackRange,layerMask,QueryTriggerInteraction.Ignore))
        {
            end=hit.point;
            hit.collider.GetComponentInParent<PlayerStateMachine>()?.TakeDamage(shotDamage,muzzle,"Disparo de robot centinela");
        }
        presentation?.Fire();
        if(tracer!=null) { tracer.SetPosition(0,muzzle);tracer.SetPosition(1,end);tracer.enabled=true; }
        yield return new WaitForSeconds(duration);
        if(tracer!=null) tracer.enabled=false;
        cooldown=shootCooldown; attacking=false;
    }
    public void ConfigureDifficulty(float hp,float damage,float precision)
    {
        maxHealth=Mathf.Max(1,hp*(meleeCombat!=null?meleeCombat.healthMultiplier:1)); health=maxHealth;
        meleeCombat?.ConfigureDamage(damage);
        shotDamage=Mathf.Max(1,damage); accuracy=Mathf.Clamp01(precision);
    }
    public void TakeDamage(float amount)
    {
        if(IsDead||amount<=0) return;
        health=Mathf.Max(0,health-amount);
        presentation?.SetHealth(health/Mathf.Max(1,maxHealth));
        if(health<=0)
        {
            IsDead=true; StopAllCoroutines();meleeCombat?.CancelOnDeath();
            if(agent!=null&&agent.isOnNavMesh) agent.isStopped=true;
            if(agent!=null) agent.enabled=false;
            foreach(var collider in GetComponentsInChildren<Collider>()) collider.enabled=false;
            if(tracer!=null) tracer.enabled=false;
            SquadManager.Instance?.Unregister(this);
            CombatFeedback.ReportKill(); EnemyEvents.OnDeath?.Invoke(this);
            presentation?.Die();
            Destroy(gameObject,3f);
        }
    }
    public void ReactToHit(RaycastHit hit,Vector3 direction,float amount) { if(!IsDead) presentation?.Hit(hit.point,hit.normal,direction,amount); }
    public void MoveToFormationPosition(Vector3 p) { _currentFormationPosition=p; }
    public Vector3 SeparationForce()=>Vector3.zero;
    public void ReduceShootCooldown() { }
    public bool CheckDistance()=>!IsDead;
    public void Use() { TickCombat(); }
}
