using UnityEngine;

[RequireComponent(typeof(PlayerStateMachine))]
public class Shield : MonoBehaviour,IShield,IUpgradeable
{
    public string Id=>"player_shield";
    int _level;
    [SerializeField] int maxUpgradeLevel=10;
    public int Level=>_level;
    public int MaxLevel=>maxUpgradeLevel;
    [SerializeField] float _maxShield=50;
    float _currentShield,delayRemaining;
    [Header("Recovery")]
    [SerializeField] float regenDelay=3,regenRate=12;
    PlayerStateMachine player;
    public float Current=>_currentShield;
    public float Max=>_maxShield;
    public bool IsRegenerating=>delayRemaining<=0&&Current<Max&&player!=null&&!player.IsDead;
    public float RecoveryDelay=>delayRemaining;
    void Awake()
    { player=GetComponent<PlayerStateMachine>();_currentShield=Max;GameManager.Instance?.Register(this); }
    public float ConsumeDamage(float amount)
    {
        if(amount<=0)return 0;
        delayRemaining=regenDelay;
        float absorbed=Mathf.Min(_currentShield,amount);_currentShield-=absorbed;
        return amount-absorbed;
    }
    public void Absorb(float amount){player.TakeDamage(amount);}
    public void Regenerate(){delayRemaining=regenDelay;}
    void Update()
    {
        if(player==null||player.IsDead||WeaponAction.CombatPaused)return;
        float dt=Time.deltaTime;
        if(delayRemaining>0)
        {
            float waiting=Mathf.Min(delayRemaining,dt);delayRemaining-=waiting;dt-=waiting;
        }
        if(delayRemaining<=0)_currentShield=Mathf.Min(Max,_currentShield+regenRate*dt);
    }
    public void Restore(float amount)
    { if(player!=null&&!player.IsDead&&amount>0)_currentShield=Mathf.Min(Max,Current+amount); }
    public void IncreaseCapacity(float amount,float restore)
    { _maxShield=Mathf.Max(0,Max+amount);_currentShield=Mathf.Min(Current,Max);Restore(restore); }
    public void SetMaxShield(float value)
    { _maxShield=Mathf.Max(0,value);_currentShield=_maxShield; }
    public void GetNewUpgrade(float value,bool percent)
    {
        if(_level>=MaxLevel)return;_level++;
        SetMaxShield(percent?Max*(1+value/100):Max+value);
    }
    public int GetUpgradeCost()=>80*(Level+1);
    public void ApplyUpgrade(){GetNewUpgrade(10,false);}
}
