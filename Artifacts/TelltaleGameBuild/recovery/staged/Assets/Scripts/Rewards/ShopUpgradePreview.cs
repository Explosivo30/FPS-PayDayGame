using System.Collections.Generic;
using UnityEngine;

public static class ShopUpgradePreview
{
    public static string Describe(StatUpgrade upgrade,PlayerStateMachine player)
    {
        var manager=UpgradeManager.Instance;
        int level=manager.GetLevel(upgrade);if(level>=upgrade.MaxLevel)return "Mejora completada";
        float value=upgrade.GetValue(level);
        if(upgrade.target==UpgradeTarget.Player)
        {
            float before=upgrade.playerStat==PlayerStat.MaxHealth?player.MaxHealth:upgrade.playerStat==PlayerStat.Shield?player.GetComponent<Shield>().Max:
                upgrade.playerStat==PlayerStat.Acceleration?player.maxGroundSpeed:player.jumpForce;
            float after=upgrade.upgradeMode==UpgradeMode.Percentual?before*(1+value/100):before+value;
            return upgrade.description+"\n"+RewardText.Number(before)+" → "+RewardText.Number(after);
        }
        var lines=new List<string>();
        foreach(var gun in player.GetComponentsInChildren<BaseGun>(true))
        {
            if(!string.IsNullOrEmpty(upgrade.gunTypeID)&&gun.GunTypeID!=upgrade.gunTypeID)continue;
            var ledger=WeaponStatLedger.For(gun);
            var after=ledger.Preview(upgrade.weaponStat,value,false);
            lines.Add(RewardContext.Name(gun.GunTypeID)+": "+(gun.Action.shellReload&&upgrade.weaponStat==WeaponStat.ReloadTime?
                RewardText.Number(ledger.Current.insert)+" → "+RewardText.Number(after.insert)+" s/cartucho":RewardText.Stat(upgrade.weaponStat,ledger.Current,after,false)));
        }
        return string.Join("\n",lines);
    }
}
