using System.Collections.Generic;
using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }
    public StatUpgrade[] catalog;
    public event System.Action<StatUpgrade> Purchased;
    readonly Dictionary<StatUpgrade,int> currentLevels=new Dictionary<StatUpgrade,int>();
    void Awake(){if(Instance!=null&&Instance!=this){Destroy(this);return;}Instance=this;}
    void OnDestroy(){if(Instance==this)Instance=null;}
    public int GetLevel(StatUpgrade upgrade)=>upgrade!=null&&currentLevels.TryGetValue(upgrade,out var level)?level:0;
    public int GetNextCost(StatUpgrade upgrade)=>upgrade!=null&&GetLevel(upgrade)<upgrade.MaxLevel?upgrade.GetCost(GetLevel(upgrade)):-1;
    public bool CanUpgrade(StatUpgrade upgrade)
    {
        if(upgrade==null||GetNextCost(upgrade)<0||CurrencyManager.Instance==null||(TowerSession.Instance?.Rewards?.Pending??false))return false;
        if(TowerSession.Instance!=null&&!TowerSession.Instance.Progression.IsUnlocked(upgrade.persistentUnlockId))return false;
        return CurrencyManager.Instance.SpendCheck(GetNextCost(upgrade));
    }
    public void BuyUpgrade(StatUpgrade upgrade)
    {
        if(!CanUpgrade(upgrade))return;
        var players=GameManager.Instance?.GetPlayerTransforms();
        if(players==null||players.Count==0)return;
        var player=players[0].GetComponent<PlayerStateMachine>();if(player==null||player.IsDead)return;
        var targets=new List<BaseGun>();
        if(upgrade.target==UpgradeTarget.Weapon)
        {
            foreach(var gun in player.GetComponentsInChildren<BaseGun>(true))
                if(string.IsNullOrEmpty(upgrade.gunTypeID)||gun.GunTypeID==upgrade.gunTypeID)targets.Add(gun);
            if(targets.Count==0)return;
        }
        int level=GetLevel(upgrade);
        if(!CurrencyManager.Instance.Spend(upgrade.GetCost(level)))return;
        float value=upgrade.GetValue(level);currentLevels[upgrade]=level+1;
        if(upgrade.target==UpgradeTarget.Player)player.ApplyPlayerStat(upgrade.playerStat,value,upgrade.upgradeMode==UpgradeMode.Percentual);
        else foreach(var gun in targets)WeaponStatLedger.For(gun).SetShop(upgrade.GetInstanceID()+":"+level,upgrade.weaponStat,value);
        Purchased?.Invoke(upgrade);
        ShopManager.Instance?.RefreshAllButtons();
    }
}
