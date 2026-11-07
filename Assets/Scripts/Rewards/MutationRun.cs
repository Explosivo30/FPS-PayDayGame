using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Run-owned abilities. Only direct player actions can generate reactions.</summary>
[DefaultExecutionOrder(100)]
public sealed partial class MutationRun : MonoBehaviour
{
    readonly Dictionary<MutationKind,RewardCard> owned=new Dictionary<MutationKind,RewardCard>();
    readonly Dictionary<MutationKind,float> readyAt=new Dictionary<MutationKind,float>();
    readonly Dictionary<BaseGun,int> spent=new Dictionary<BaseGun,int>();
    readonly List<MutationWorldEffect> effects=new List<MutationWorldEffect>();
    TowerSession session;
    public PlayerStateMachine Player=>session!=null?session.player:null;
    public Camera View { get; private set; }
    public float Clock { get; private set; }
    public int EffectCount=>effects.Count;
    public int DerivedHits { get; private set; }
    public int DirectHits { get; private set; }
    public int ArcCharge { get; private set; }
    public int SawCharge { get; private set; }
    public bool SawReady { get; private set; }
    public bool BladeInFlight { get; private set; }
    public bool JumpHeld { get; set; }
    public IReadOnlyList<MutationWorldEffect> Effects=>effects;
    public IEnumerable<MutationKind> Owned=>owned.Keys;
    Component chargeWeapon;
    float chargeStart;
    bool wasSliding,retroAirborne,retroSpent,shieldBroken;
    float slideDistance;
    Vector3 slidePrevious;
    readonly List<Vector3> slidePath=new List<Vector3>();
    MutationWorldEffect trail,carrier,hammerMark;
    BaseGun lastGun;
    GUIStyle hudStyle;

    public static MutationRun For(TowerSession owner)
    {
        if(owner==null)return null;
        var run=owner.GetComponent<MutationRun>();
        return run!=null?run:owner.gameObject.AddComponent<MutationRun>();
    }
    public static MutationRun From(Component weapon)
    {
        var tower=TowerSession.Instance;
        if(tower==null||weapon==null||weapon.GetComponentInParent<PlayerStateMachine>()!=tower.player)return null;
        return tower.GetComponent<MutationRun>();
    }
    public bool Has(MutationKind kind)=>owned.ContainsKey(kind);
    public float Damage(MutationKind kind,float fallback)=>owned.TryGetValue(kind,out var card)&&card.value>0?card.value:fallback;
    public bool Unlock(RewardCard card)
    {
        if(card==null||card.mutation==MutationKind.None||Has(card.mutation))return false;
        owned.Add(card.mutation,card);return true;
    }
    public bool Ready(MutationKind kind)=>!readyAt.TryGetValue(kind,out float until)||Clock>=until;
    public float Cooldown(MutationKind kind)=>readyAt.TryGetValue(kind,out float until)?Mathf.Max(0,until-Clock):0;
    bool Use(MutationKind kind,float seconds)
    {if(!Has(kind)||!Ready(kind))return false;readyAt[kind]=Clock+seconds;return true;}
    void Awake()
    {
        session=GetComponent<TowerSession>();
        if(Player!=null)
        {
            foreach(var camera in Player.GetComponentsInChildren<Camera>(true))if(camera.gameObject.layer!=8){View=camera;break;}
            Player.Damaged+=OnDamage;Player.Died+=OnDeath;
        }
        CombatFeedback.PlayerEliminated+=OnKill;
    }
    void OnDestroy()
    {
        CombatFeedback.PlayerEliminated-=OnKill;
        if(Player!=null){Player.Damaged-=OnDamage;Player.Died-=OnDeath;}
        ClearWorld();
    }
    void OnDeath(){ClearWorld();CancelInput();}
    void OnDamage(PlayerDamage damage){if(damage.ShieldBroken)shieldBroken=true;}
    void Update(){Tick(Time.deltaTime);}
    public void Tick(float dt)
    {
        if(Player==null)return;
        if(Player.IsDead||session.RunEnded){ClearWorld();return;}
        if(session.IsTransitioning)return;
        if(WeaponAction.CombatPaused)return;
        Clock+=Mathf.Max(0,dt);
        if(retroSpent&&!Player.Grounded)retroAirborne=true;
        if(retroAirborne&&Player.Grounded){retroSpent=retroAirborne=false;}
        ObserveSlide(Player.IsSliding,Player.transform.position);
        // Effects may spawn or finish other effects while damage is resolved.
        var snapshot=effects.ToArray();
        for(int i=snapshot.Length-1;i>=0;i--)
        {
            var fx=snapshot[i];
            if(fx==null||!fx.Live)continue;
            fx.Tick(dt);
        }
        effects.RemoveAll(fx=>fx==null||!fx.Live);
        PruneMarks();
    }
    public void CancelInput(){chargeWeapon=null;}
    public bool ProcessAttack(Component weapon,bool held,bool previouslyHeld)
    {
        bool ram=weapon is Pistol&&Has(MutationKind.RamChamber);
        bool blade=weapon is Knife&&Has(MutationKind.ReturningBlade);
        if(!ram&&!blade){CancelInput();return false;}
        if(Player==null||Player.IsDead||WeaponAction.CombatPaused){CancelInput();return true;}
        var action=weapon.GetComponent<WeaponAction>();
        if(chargeWeapon!=null&&chargeWeapon!=weapon)CancelInput();
        if(held&&!previouslyHeld&&!action.IsBusy&&!(blade&&BladeInFlight))
        {chargeWeapon=weapon;chargeStart=Clock;}
        if(!held&&previouslyHeld&&chargeWeapon==weapon)
        {
            float elapsed=Clock-chargeStart;CancelInput();
            if(action.IsBusy)return true;
            if(ram)
            {
                var pistol=(Pistol)weapon;
                if(elapsed>=.6f&&pistol.currentAmmo>=3)pistol.FireCharged();else pistol.Use();
            }
            else
            {
                var knife=(Knife)weapon;
                if(elapsed>=.5f)ThrowBlade(knife);else knife.Use();
            }
        }
        return true;
    }
    public void OnGunShot(BaseGun gun,int cost)
    {
        lastGun=gun;spent[gun]=(spent.TryGetValue(gun,out int old)?old:0)+cost;
        if(gun is ShotGun)
        {
            if(Has(MutationKind.ScrapFunnel))Funnel(gun);
            if(JumpHeld&&!retroSpent&&Use(MutationKind.Retrothruster,6))
            {
                retroSpent=true;retroAirborne=false;
                Vector3 direction=-Vector3.ProjectOnPlane(View.transform.forward,Vector3.up).normalized;
                Player.BeginCardImpulse(direction,4,.18f);
                MutationVisual.Burst(Player.transform.position+Vector3.up*.5f,Color.cyan,.55f,this);
            }
        }
    }
    public void OnReloadStarted(BaseGun gun,int loaded)
    {
        CancelInput();lastGun=gun;
        int used=spent.TryGetValue(gun,out int amount)?amount:0;spent[gun]=0;
        Vector3 drop=Ground(Player.transform.position+Player.transform.forward*.9f);
        bool magazines=gun is Pistol||gun is MachineGun;
        if(magazines&&used>=3&&NoEffect(MutationWorldKind.TimedMagazine)&&Has(MutationKind.ExplosiveReload))
            Spawn(MutationWorldKind.TimedMagazine,drop+Player.transform.right*.25f,1,gun,Damage(MutationKind.ExplosiveReload,45));
        if(magazines&&loaded>0&&NoEffect(MutationWorldKind.MagazineBomb)&&Use(MutationKind.MagazineBomb,10))
            Spawn(MutationWorldKind.MagazineBomb,drop-Player.transform.right*.25f,6,gun,Damage(MutationKind.MagazineBomb,60));
        if(loaded==0&&Use(MutationKind.DecoyBeacon,14))
            Spawn(MutationWorldKind.Decoy,drop,2,gun,0);
        if(Has(MutationKind.MagneticAnchor)&&Ready(MutationKind.MagneticAnchor)&&TryGroundAim(8,out var anchor)&&Use(MutationKind.MagneticAnchor,14))
            Spawn(MutationWorldKind.Anchor,anchor,2,gun,0);
        if(hammerTarget!=null&&!hammerTarget.IsDead&&hammerHits>=3&&Use(MutationKind.WorkHammer,12))
        {Spawn(MutationWorldKind.Hammer,Ground(hammerTarget.transform.position),.8f,gun,Damage(MutationKind.WorkHammer,85));if(hammerMark!=null)hammerMark.Finish();hammerMark=null;hammerTarget=null;hammerHits=0;}
        if(SawReady&&NoEffect(MutationWorldKind.Saw))
        {SawReady=false;SawCharge=0;var saw=Spawn(MutationWorldKind.Saw,ProjectileOrigin(),8,gun,Damage(MutationKind.RecoverableSaw,50));if(saw!=null)saw.Direction=View.transform.forward;}
        if(shieldBroken&&Use(MutationKind.DischargeCoil,18))
        {shieldBroken=false;Spawn(MutationWorldKind.Coil,drop,4,gun,Damage(MutationKind.DischargeCoil,25));}
    }
    public void OnReloadCompleted(BaseGun gun){if(gun is Pistol)carrierArmed=true;}
    void OnKill(PlayerKill kill)
    {
        if(!kill.Direct||From(kill.Weapon)!=this||Player==null||Player.IsDead)return;
        if(Has(MutationKind.GroundArc))ArcCharge=Mathf.Min(3,ArcCharge+1);
        if(Has(MutationKind.RecoverableSaw)&&kill.Weapon is BaseGun&&!SawReady&&NoEffect(MutationWorldKind.Saw))
        {SawCharge=Mathf.Min(3,SawCharge+1);if(SawCharge>=3)SawReady=true;}
        if(Use(MutationKind.MagneticScrap,8))Spawn(MutationWorldKind.ScrapMagnet,Ground(kill.Enemy.transform.position),1.2f,kill.Weapon,0);
    }
    public void OnWorldHit(BaseGun gun,RaycastHit hit)
    {
        if(hammerGun!=gun||hammerLastShot!=gun.ShotSequence){hammerTarget=null;hammerHits=0;if(hammerMark!=null)hammerMark.Finish();hammerMark=null;}
        if(ArcCharge>=3&&Has(MutationKind.GroundArc)&&hit.normal.y>.65f&&hit.collider.GetComponentInParent<MutationObject>()==null)
        {
            ArcCharge=0;var fx=Spawn(MutationWorldKind.GroundArc,hit.point,1.5f,gun,Damage(MutationKind.GroundArc,35));
            Vector3 side=Vector3.Cross(Vector3.up,View.transform.forward).normalized;
            Vector3 a=hit.point-side*2,b=hit.point+side*2;
            if(Physics.Linecast(hit.point+Vector3.up*.08f,a+Vector3.up*.08f,out var left,1<<3))a=left.point;
            if(Physics.Linecast(hit.point+Vector3.up*.08f,b+Vector3.up*.08f,out var right,1<<3))b=right.point;
            fx.Path.Add(Ground(a));fx.Path.Add(Ground(b));
        }
    }
    public void ObserveSlide(bool sliding,Vector3 position)
    {
        if(sliding&&!wasSliding){slidePath.Clear();slideDistance=0;slidePrevious=position;slidePath.Add(Ground(position));}
        if(sliding&&wasSliding)
        {
            float step=Vector3.Distance(position,slidePrevious);slidePrevious=position;
            if(step<2.5f)slideDistance+=step;
            if(slidePath.Count<32&&Vector3.Distance(slidePath[slidePath.Count-1],Ground(position))>.25f)slidePath.Add(Ground(position));
            if(slideDistance>=2&&trail==null&&Use(MutationKind.SlagTrail,4))
            {trail=Spawn(MutationWorldKind.SlagTrail,slidePath[0],2,lastGun,Damage(MutationKind.SlagTrail,24));trail.Path.AddRange(slidePath);}
            if(trail!=null&&trail.Live){trail.Path.Clear();trail.Path.AddRange(slidePath);}
        }
        if(!sliding&&wasSliding)
        {
            if(Use(MutationKind.EmergencyBrake,5))Brake();
            trail=null;
        }
        wasSliding=sliding;
    }
    public void ClearWorld()
    {
        foreach(var fx in effects)if(fx!=null)fx.Finish();effects.Clear();
        fuses.Clear();cuts.Clear();hitStamps.Clear();carrier=null;trail=null;BladeInFlight=false;
        hammerTarget=null;hammerMark=null;hammerHits=0;wasSliding=false;CancelInput();
        if(Player!=null)Player.CancelCardImpulse();
        foreach(var signal in GetComponentsInChildren<MutationSignal>())Destroy(signal.gameObject);
    }
    public void ResetRun(){ClearWorld();owned.Clear();readyAt.Clear();spent.Clear();forkHits.Clear();carrierArmed=false;lastGun=null;Clock=0;DirectHits=DerivedHits=0;ArcCharge=SawCharge=0;SawReady=shieldBroken=retroSpent=retroAirborne=JumpHeld=false;}
    bool NoEffect(MutationWorldKind kind)=>!effects.Any(f=>f!=null&&f.Live&&f.Kind==kind);
    public MutationWorldEffect Spawn(MutationWorldKind kind,Vector3 position,float lifetime,Component source,float damage)
    {
        effects.RemoveAll(e=>e==null||!e.Live);
        if(effects.Count>=64)return null;
        var go=new GameObject(kind.ToString());go.transform.SetParent(transform,false);go.transform.position=position;
        var fx=go.AddComponent<MutationWorldEffect>();fx.Initialize(this,kind,lifetime,source,damage);effects.Add(fx);return fx;
    }
    public Vector3 Ground(Vector3 point)
    {return Physics.Raycast(point+Vector3.up*2,Vector3.down,out var hit,5,1<<3,QueryTriggerInteraction.Ignore)?hit.point+Vector3.up*.04f:point;}
    public bool TryGroundAim(float range,out Vector3 point)
    {
        point=Vector3.zero;if(View==null)return false;
        if(!Physics.Raycast(View.transform.position,View.transform.forward,out var hit,range,1<<3,QueryTriggerInteraction.Ignore)||hit.normal.y<.65f)return false;
        point=hit.point+Vector3.up*.04f;return true;
    }
    public Vector3 ProjectileOrigin()
    {
        Vector3 origin=View.transform.position,desired=origin+View.transform.forward*.4f;
        return Physics.Linecast(origin,desired,out var hit,1<<3,QueryTriggerInteraction.Ignore)?hit.point-View.transform.forward*.15f:desired;
    }
    void OnGUI()
    {
        if(owned.Count==0||Player==null||Player.IsDead||WeaponAction.CombatPaused||session.IsTransitioning)return;
        string status="";
        if(chargeWeapon!=null)status=chargeWeapon is Pistol?(Clock-chargeStart>=.6f?"CÁMARA CARGADA · Suelta para perforar":"CARGANDO · Mantén para perforar"):"HOJA DE RETORNO · Suelta para lanzar";
        else if(BladeInFlight)status="HOJA EN VUELO · Puedes cambiar de arma";
        else if(SawReady)status="SIERRA LISTA · Recarga para lanzar";
        else if(Has(MutationKind.GroundArc))status=ArcCharge>=3?"ARCO CARGADO · Dispara al suelo":"ARCO DE SUELO · "+ArcCharge+" / 3 bajas directas";
        else if(Has(MutationKind.WorkHammer)&&hammerTarget!=null)status="MARTILLO · "+hammerHits+" / 3 impactos al mismo robot";
        if(string.IsNullOrEmpty(status))return;
        if(hudStyle==null)hudStyle=new GUIStyle(GUI.skin.box){fontSize=15,alignment=TextAnchor.MiddleCenter,wordWrap=true};
        GUI.color=Color.white;GUI.Box(new Rect(Screen.width*.5f-230,Screen.height-180,460,45),status,hudStyle);
    }
}
