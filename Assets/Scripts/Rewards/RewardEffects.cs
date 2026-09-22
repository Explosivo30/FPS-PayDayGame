using System;
using System.Linq;
using UnityEngine;

public sealed class RewardContext
{
    public readonly TowerSession session;
    public PlayerStateMachine Player=>session.player;
    public Shield Shield=>Player.GetComponent<Shield>();
    public RewardContext(TowerSession value){session=value;}
    public Component Weapon(string id)=>session.weapons.Weapons.FirstOrDefault(w=>w is BaseGun gun?gun.GunTypeID==id:w is Knife&&id=="Knife");
    public string EquippedId=>session.weapons.currentWeapon is BaseGun gun?gun.GunTypeID:"Knife";
    public static string Name(string id)=>id=="Pistol"?"Pistola":id=="MachineGun"?"Carabina":id=="ShotGun"?"Escopeta":id=="Knife"?"Cuchillo":"Operador";
}
public interface IRewardEffect
{
    bool Eligible(RewardCard card,RewardContext context);
    string Preview(RewardCard card,RewardContext context,int nextLevel);
    void Apply(RewardCard card,RewardContext context,int nextLevel);
}
public sealed class WeaponCardEffect : IRewardEffect
{
    public bool Eligible(RewardCard c,RewardContext x)=>x.Weapon(c.weaponId)!=null;
    public string Preview(RewardCard c,RewardContext x,int rank)
    {
        var weapon=x.Weapon(c.weaponId);if(weapon==null)return "Arma no disponible";
        var ledger=WeaponStatLedger.For(weapon);var next=ledger.Preview(c.stat,c.value*rank,true,"card:"+c.id);
        return RewardText.Stat(c.stat,ledger.Current,next,weapon is BaseGun gun&&gun.Action.shellReload);
    }
    public void Apply(RewardCard c,RewardContext x,int rank)=>WeaponStatLedger.For(x.Weapon(c.weaponId)).SetCard(c.id,c.stat,c.value*rank);
}
public sealed class PlayerCardEffect : IRewardEffect
{
    public bool Eligible(RewardCard c,RewardContext x)=>c.kind!=RewardKind.Repair||x.Player.Health<x.Player.MaxHealth||x.Shield.Current<x.Shield.Max;
    public string Preview(RewardCard c,RewardContext x,int rank)
    {
        if(c.kind==RewardKind.Health)return "Salud máxima "+RewardText.Number(x.Player.MaxHealth)+" → "+RewardText.Number(x.Player.MaxHealth+c.value)+"\nRecupera "+RewardText.Number(c.value)+" salud";
        if(c.kind==RewardKind.Shield)return "Escudo máximo "+RewardText.Number(x.Shield.Max)+" → "+RewardText.Number(x.Shield.Max+c.value)+"\nRecupera "+RewardText.Number(c.value)+" escudo";
        return "Salud +"+RewardText.Number(Mathf.Min(c.value,x.Player.MaxHealth-x.Player.Health))+
            "  /  Escudo +"+RewardText.Number(Mathf.Min(c.secondaryValue,x.Shield.Max-x.Shield.Current));
    }
    public void Apply(RewardCard c,RewardContext x,int rank)
    {
        if(c.kind==RewardKind.Health) { x.Player.ApplyPlayerStat(PlayerStat.MaxHealth,c.value,false);x.Player.Heal(c.value); }
        else if(c.kind==RewardKind.Shield)x.Shield.IncreaseCapacity(c.value,c.value);
        else { x.Player.Heal(c.value);x.Shield.Restore(c.secondaryValue); }
    }
}
public sealed class SupplyCardEffect : IRewardEffect
{
    public bool Eligible(RewardCard c,RewardContext x)=>x.Weapon(c.weaponId) is BaseGun gun&&gun.limitedReserve&&gun.reserveAmmo<gun.maxReserveAmmo;
    public string Preview(RewardCard c,RewardContext x,int rank)
    {
        var gun=x.Weapon(c.weaponId) as BaseGun;
        return gun==null?"Arma no disponible":"Reserva "+gun.reserveAmmo+" → "+Mathf.Min(gun.maxReserveAmmo,gun.reserveAmmo+Mathf.RoundToInt(gun.ammo*c.value))+"\nEl cargador conserva su munición";
    }
    public void Apply(RewardCard c,RewardContext x,int rank)
    { var gun=(BaseGun)x.Weapon(c.weaponId);gun.AddReserve(Mathf.RoundToInt(gun.ammo*c.value)); }
}
public sealed class SpecialCardEffect : IRewardEffect
{
    public bool Eligible(RewardCard c,RewardContext x)=>c.kind!=RewardKind.AutoLoader||x.session.weapons.Weapons.Any(w=>w is BaseGun g&&g.limitedReserve);
    public string Preview(RewardCard c,RewardContext x,int rank)=>c.kind==RewardKind.KillShield?
        "Por baja: +"+RewardText.Number(c.value)+" escudo\nHasta tu máximo":
        "Por baja: "+RewardText.Number(c.value*100)+"% del cargador\nConsume reserva real";
    public void Apply(RewardCard c,RewardContext x,int rank)=>x.session.Rewards.EnableSpecial(c);
}
public sealed class ChipsCardEffect : IRewardEffect
{
    public bool Eligible(RewardCard c,RewardContext x)=>true;
    public string Preview(RewardCard c,RewardContext x,int rank)=>"+"+Mathf.RoundToInt(c.value)+" chips para la tienda";
    public void Apply(RewardCard c,RewardContext x,int rank)=>GameManager.Instance.AddScore(Mathf.RoundToInt(c.value));
}
public static class RewardText
{
    public static string Number(float value)=>value.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture);
    public static string Stat(WeaponStat stat,WeaponStatLedger.Values before,WeaponStatLedger.Values after,bool shells=false)
    {
        string label=stat==WeaponStat.Damage?"Daño":stat==WeaponStat.FireRate?"Disparos/s":stat==WeaponStat.AmmoCapacity?"Capacidad":
            stat==WeaponStat.ReloadTime?"Recarga":stat==WeaponStat.RecoilKickUp?"Retroceso":"Dispersión";
        if(stat==WeaponStat.ReloadTime&&shells)
            return "Por cartucho "+Number(before.insert)+" → "+Number(after.insert)+" s\nInicio/cierre "+Number(after.start)+" / "+Number(after.end)+" s";
        return label+" "+Number(before.Read(stat))+" → "+Number(after.Read(stat))+(stat==WeaponStat.ReloadTime?" s":"");
    }
}
public readonly struct PlayerKill
{
    public readonly NormalEnemyStateMachine Enemy;
    public readonly Component Weapon;
    public PlayerKill(NormalEnemyStateMachine enemy,Component weapon){Enemy=enemy;Weapon=weapon;}
}
public abstract class KillReward : IDisposable
{
    protected readonly RewardContext context;
    protected readonly float amount;
    protected KillReward(RewardContext context,float amount)
    { this.context=context;this.amount=amount;CombatFeedback.PlayerEliminated+=OnKill; }
    void OnKill(PlayerKill kill)
    {
        if(context.Player==null||context.Player.IsDead||kill.Weapon==null||kill.Weapon.GetComponentInParent<PlayerStateMachine>()!=context.Player)return;
        Apply(kill);
    }
    protected abstract void Apply(PlayerKill kill);
    public void Dispose()=>CombatFeedback.PlayerEliminated-=OnKill;
}
public sealed class KillShieldReward : KillReward
{
    public KillShieldReward(RewardContext c,float amount):base(c,amount){}
    protected override void Apply(PlayerKill kill)=>context.Shield.Restore(amount);
}
public sealed class AutoLoaderReward : KillReward
{
    public AutoLoaderReward(RewardContext c,float amount):base(c,amount){}
    protected override void Apply(PlayerKill kill)
    {
        if(kill.Weapon is BaseGun gun&&gun.limitedReserve)
        { gun.TransferFromReserve(Mathf.CeilToInt(gun.ammo*amount));gun.Action.NotifyAmmo(); }
    }
}
