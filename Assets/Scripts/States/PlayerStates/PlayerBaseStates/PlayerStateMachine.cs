using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-50)]
public partial class PlayerStateMachine : StateMachine, IDamageable, IUpgradeable, IImpulse
{
    [NonSerialized] public InputReader controls;
    [SerializeField] float _height;
    [SerializeField] float _castRadius=.36f, _castLength=.74f, _maxAngle=45;
    [SerializeField] LayerMask _groundMask=1<<3;
    [SerializeField] CharacterController cc;
    public LayerMask GroundMask=>_groundMask;
    Vector3 _groundNormal=Vector3.up, _slideNormal=Vector3.up;
    bool _grounded, _sliding;
    public Vector3 GroundNormal=>_groundNormal;
    public Vector3 SlideNormal=>_slideNormal;
    public bool Grounded=>_grounded;
    public bool Sliding=>_sliding;
    [Header("Movement")]
    public float maxGroundSpeed=8, maxCrouchSpeed=4, maxAirSpeed=5;
    public float groundAcceleration=80, airAcceleration=12, groundFriction=8, airFriction=.5f;
    [Header("Jump and slide")]
    public float jumpForce=6.5f, slideBoost=2, slideFriction=2;
    public float crouchHeightMultiplier=.5f, slideDuration=.7f, heightTransitionSpeed=12;
    [SerializeField] float rotationSpeed=50, _gravityForce=22;
    public float GravityForce=>_gravityForce;
    Vector3 _gravityDir=Vector3.down;
    public Vector3 GravityDir { get=>_gravityDir; set=>_gravityDir=value.normalized; }
    [HideInInspector] public Vector3 PlayerVelocity;
    float originalHeight,originalCamLocalY,targetHeight,verticalRotation;
    Vector3 originalCenter;
    public Transform headCam;
    [NonSerialized] public CameraTilt cameraTilt;
    [SerializeField] float minVerticalAngle=-80,maxVerticalAngle=80;
    [Header("Upgradeable stats")]
    [SerializeField] float accelerationIncrement=2;
    [SerializeField] int maxUpgradeLevel=5;
    int upgradeLevel;
    public string Id=>"player";
    public int Level=>upgradeLevel;
    public int MaxLevel=>maxUpgradeLevel;
    protected float maxHPPlayer=100,currentHPPlayer;
    [SerializeField] Volume damageVolume;
    [SerializeField] float fadeInTime=.2f,fadeOutTime=1;
    public float Health=>currentHPPlayer;
    public float MaxHealth=>maxHPPlayer;
    public bool IsDead { get; private set; }
    public string LastDamageCause { get; private set; }="Daño recibido";
    public event Action<PlayerDamage> Damaged;
    public event Action Died,Jumped;
    public event Action<float> Landed,Healed;
    public event Action Stepped;
    Shield shield;
    PlayerFeedback feedback;
    void Awake()
    {
        controls=GetComponent<InputReader>();cc=cc!=null?cc:GetComponent<CharacterController>();
        cameraTilt=GetComponentInChildren<CameraTilt>();
        currentHPPlayer=maxHPPlayer;
        originalHeight=cc.height;originalCenter=cc.center;targetHeight=originalHeight;
        originalCamLocalY=headCam.localPosition.y;
        shield=GetComponent<Shield>();
        if(damageVolume!=null)damageVolume.weight=0;
        GameManager.Instance?.Register(this);GameManager.Instance?.AddPlayerTransforms(transform);
        feedback=GetComponent<PlayerFeedback>();
        if(feedback==null)feedback=gameObject.AddComponent<PlayerFeedback>();
    }
    void OnEnable() { if(controls!=null)controls.JumpEvent+=QueueJump; }
    void OnDisable() { if(controls!=null)controls.JumpEvent-=QueueJump; jumpQueuedUntil=float.NegativeInfinity; }
    void Start()
    {
        // All scene managers have finished Awake before registration is required by shops and waves.
        GameManager.Instance?.Register(this);GameManager.Instance?.AddPlayerTransforms(transform);
        GunRecoil.EnsureExists().ResetRecoil();SwitchState(new PlayerLocomotionState(this));
    }
    public void PlayerLook()
    {
        if(IsDead||WeaponAction.CombatPaused)return;
        Vector2 mouse=(controls!=null?controls.LookValue:Vector2.zero)*(rotationSpeed/60f);
        Vector2 recoil=GunRecoil.Instance?.Consume(mouse)??Vector2.zero;
        transform.Rotate(0,mouse.x+recoil.x,0);
        verticalRotation=Mathf.Clamp(verticalRotation-mouse.y-recoil.y,minVerticalAngle,maxVerticalAngle);
        headCam.localRotation=Quaternion.Euler(verticalRotation,0,0);
    }
    public Vector2 GetInput()=>controls!=null?Vector2.ClampMagnitude(controls.MovementValue,1):Vector2.zero;
    public Vector3 GetCameraForward()=>Vector3.ProjectOnPlane(headCam.forward,Vector3.up).normalized;
    public Vector3 GetCameraRight()=>Vector3.ProjectOnPlane(headCam.right,Vector3.up).normalized;
    public void Heal(float amount)
    {
        if(IsDead||amount<=0||float.IsNaN(amount)||float.IsInfinity(amount))return;
        float before=currentHPPlayer;currentHPPlayer=Mathf.Min(maxHPPlayer,currentHPPlayer+amount);
        if(currentHPPlayer>before)Healed?.Invoke(currentHPPlayer-before);
    }
    public void TeleportTo(Vector3 position,Quaternion rotation)
    {
        bool enabledController=cc.enabled;cc.enabled=false;
        transform.SetPositionAndRotation(position,rotation);
        PlayerVelocity=Vector3.zero;verticalRotation=0;headCam.localRotation=Quaternion.identity;
        cc.height=originalHeight;cc.center=originalCenter;targetHeight=originalHeight;
        var local=headCam.localPosition;local.y=originalCamLocalY;headCam.localPosition=local;
        ResetMovement();feedback?.ResetFeedback();cameraTilt?.DoTilt(0);
        GunRecoil.EnsureExists().ResetRecoil();cc.enabled=enabledController;Physics.SyncTransforms();
    }
    public void TakeDamage(float amount)=>TakeDamage(amount,transform.position,"Daño recibido");
    public void TakeDamage(float amount,Vector3 source,string cause)
    {
        if(IsDead||WeaponAction.CombatPaused||amount<=0||float.IsNaN(amount)||float.IsInfinity(amount))return;
        if(TowerSession.Instance!=null&&TowerSession.Instance.IsTransitioning)return;
        LastDamageCause=string.IsNullOrEmpty(cause)?"Daño recibido":cause;
        float before=shield!=null?shield.Current:0;
        float healthDamage=shield!=null?shield.ConsumeDamage(amount):amount;
        float lost=Mathf.Min(currentHPPlayer,healthDamage);
        currentHPPlayer=Mathf.Max(0,currentHPPlayer-healthDamage);
        bool broken=before>0&&shield!=null&&shield.Current<=0;
        Damaged?.Invoke(new PlayerDamage(before-(shield!=null?shield.Current:0),lost,broken,source,LastDamageCause));
        if(currentHPPlayer>0)return;
        IsDead=true;PlayerVelocity=Vector3.zero;jumpQueuedUntil=float.NegativeInfinity;
        Died?.Invoke();
        if(TowerSession.Instance!=null)TowerSession.Instance.EndRun(LastDamageCause);
        else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    public int GetUpgradeCost()=>80*(Level+1);
    public void ApplyUpgrade()
    {
        if(upgradeLevel>=maxUpgradeLevel)return;
        upgradeLevel++;maxGroundSpeed+=accelerationIncrement;groundAcceleration+=accelerationIncrement*5;
    }
    public void ApplyPlayerStat(PlayerStat stat,float value,bool isPercent)
    {
        switch(stat)
        {
            case PlayerStat.Acceleration:
                maxGroundSpeed=isPercent?maxGroundSpeed*(1+value/100):maxGroundSpeed+value;
                groundAcceleration=Mathf.Max(groundAcceleration,maxGroundSpeed*10);break;
            case PlayerStat.JumpHeight:
                jumpForce=isPercent?jumpForce*(1+value/100):jumpForce+value;break;
            case PlayerStat.MaxHealth:
                maxHPPlayer=isPercent?maxHPPlayer*(1+value/100):maxHPPlayer+value;
                currentHPPlayer=Mathf.Min(currentHPPlayer,maxHPPlayer);break;
            case PlayerStat.Shield:
                if(shield!=null)shield.GetNewUpgrade(value,isPercent);break;
        }
    }
    public void ApplyImpulse(Vector3 force) { if(!IsDead&&!WeaponAction.CombatPaused)PlayerVelocity+=force; }
}
public readonly struct PlayerDamage
{
    public readonly float ShieldLoss,HealthLoss;
    public readonly bool ShieldBroken;
    public readonly Vector3 Source;
    public readonly string Cause;
    public PlayerDamage(float shield,float health,bool broken,Vector3 source,string cause)
    { ShieldLoss=shield;HealthLoss=health;ShieldBroken=broken;Source=source;Cause=cause; }
}
public class PlayerLocomotionState : PlayerBaseState
{
    public PlayerLocomotionState(PlayerStateMachine player):base(player){}
    public override void Enter(){}
    public override void Tick(){stateMachine.TickLocomotion();}
    public override void Exit(){}
}
