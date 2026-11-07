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
    [SerializeField] private float shootCooldown=1.8f;
    [SerializeField] private float shotDamage=12,anticipation=.25f;
    private float health;
    private LineRenderer tracer;
    private RobotPresentation presentation;
    private MeleeRobotCombat meleeCombat;
    public bool IsMelee => meleeCombat!=null;
    public EnemyPerception Perception { get; private set; }
    public EnemyNavigation Navigation { get; private set; }
    public RangedRobotCombat Ranged { get; private set; }
    public bool IsVeteran { get; private set; }
    public void ConfigureVeteran(bool veteran)
    {
        IsVeteran=veteran;
        if(veteran){GetComponent<EnemySignals>()?.SetVeteran();meleeCombat?.ConfigureVeteran();}
    }
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
        health=maxHealth;
        if(tracer!=null) tracer.enabled=false;
        Perception=gameObject.AddComponent<EnemyPerception>();Navigation=gameObject.AddComponent<EnemyNavigation>();
        gameObject.AddComponent<EnemySignals>();
        if(meleeCombat==null){Ranged=gameObject.AddComponent<RangedRobotCombat>();Ranged.Configure(shotDamage,accuracy,maxSpreadAngle,shootCooldown,anticipation,attackRange);}
        SquadManager.Instance?.Register(this);
        SwitchState(new IdleNormalEnemyState(this));
    }
    private void OnDestroy() { SquadManager.Instance?.Unregister(this); }
    public void TickCombat()
    {
        if(IsDead)return;
        Perception.Tick();
        if(meleeCombat!=null)meleeCombat.TickCombat();else Ranged.Tick();
    }
    public void ConfigureDifficulty(float hp,float damage,float precision)
    {
        maxHealth=Mathf.Max(1,hp*(meleeCombat!=null?meleeCombat.healthMultiplier:1)); health=maxHealth;
        meleeCombat?.ConfigureDamage(damage);
        shotDamage=Mathf.Max(1,damage); accuracy=Mathf.Clamp01(precision);Ranged?.Balance(shotDamage,accuracy);
    }
    public void TakeDamage(float amount)
    {
        if(IsDead||amount<=0) return;
        health=Mathf.Max(0,health-amount);
        presentation?.SetHealth(health/Mathf.Max(1,maxHealth));
        if(health<=0)
        {
            IsDead=true; StopAllCoroutines();Ranged?.Cancel();meleeCombat?.CancelOnDeath();
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
