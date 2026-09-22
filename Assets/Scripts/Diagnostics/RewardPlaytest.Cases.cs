#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Object=UnityEngine.Object;

public partial class RewardPlaytest
{
    void LedgerChecks()
    {
        var original=weapons.Weapons.OfType<Pistol>().First();
        var a=Instantiate(original.gameObject).GetComponent<Pistol>();var b=Instantiate(original.gameObject).GetComponent<Pistol>();
        a.gameObject.SetActive(false);b.gameObject.SetActive(false);
        var x=WeaponStatLedger.For(a);var y=WeaponStatLedger.For(b);
        float raw=a.damage;int ammo=a.currentAmmo,reserve=a.reserveAmmo;
        x.SetCard("power",WeaponStat.Damage,.2f);x.SetShop("power",WeaponStat.Damage,4);
        y.SetShop("power",WeaponStat.Damage,4);y.SetCard("power",WeaponStat.Damage,.2f);
        Check(Mathf.Abs(a.damage-(raw+4)*1.2f)<.001f&&Mathf.Abs(a.damage-b.damage)<.001f,"Shop/card order yields identical damage");
        x.SetCard("power",WeaponStat.Damage,.6f);
        Check(Mathf.Abs(a.damage-(raw+4)*1.6f)<.001f,"Three damage ranks total +60 percent, not compounding");
        float applied=a.damage;x.SetCard("power",WeaponStat.Damage,.6f);
        Check(a.damage==applied,"Replacing a modifier source is idempotent");
        int capacity=a.ammo;x.SetCard("capacity",WeaponStat.AmmoCapacity,.25f);
        Check(a.ammo==Mathf.CeilToInt(capacity*1.25f)&&a.currentAmmo==ammo&&a.reserveAmmo==reserve,"Capacity rounds up without generating ammunition");
        float reload=b.Action.reloadDuration;
        x.SetShop("reload1",WeaponStat.ReloadTime,.1f);x.SetCard("reload",WeaponStat.ReloadTime,.24f);
        y.SetCard("reload",WeaponStat.ReloadTime,.24f);y.SetShop("reload1",WeaponStat.ReloadTime,.1f);
        Check(Mathf.Abs(a.Action.reloadDuration-Mathf.Max(.6f,reload*.9f*.76f))<.001f&&Mathf.Abs(a.Action.reloadDuration-b.Action.reloadDuration)<.001f,"Reload factors preserve shop discount and order independence");
        x.SetCard("reload",WeaponStat.ReloadTime,10);
        Check(a.Action.reloadDuration==.6f&&a.Action.shellInsert==.2f&&a.Action.reloadStart==.05f&&a.Action.reloadEnd==.05f,"Reload safety floors apply to all phases");
        var shotgun=Instantiate(weapons.Weapons.OfType<ShotGun>().First().gameObject).GetComponent<ShotGun>();shotgun.gameObject.SetActive(false);
        float start=shotgun.Action.reloadStart,insert=shotgun.Action.shellInsert,end=shotgun.Action.reloadEnd;
        WeaponStatLedger.For(shotgun).SetCard("reload",WeaponStat.ReloadTime,.12f);
        Check(Mathf.Abs(shotgun.Action.reloadStart-start*.88f)<.001f&&Mathf.Abs(shotgun.Action.shellInsert-insert*.88f)<.001f&&Mathf.Abs(shotgun.Action.reloadEnd-end*.88f)<.001f,"Shotgun card changes start, insertion and closure");
        Check(original.damage==raw,"Instance upgrades do not modify original weapon or shared prefab");
        Destroy(a.gameObject);Destroy(b.gameObject);Destroy(shotgun.gameObject);
    }
    IEnumerator ModalActionChecks()
    {
        var shotgun=weapons.Weapons.OfType<ShotGun>().First();
        weapons.EquipImmediate(2);yield return new WaitForSeconds(.3f);
        shotgun.currentAmmo=1;shotgun.reserveAmmo=10;shotgun.Action.BeginReload();
        shotgun.Action.Tick(shotgun.Action.reloadStart);shotgun.Action.Tick(shotgun.Action.shellInsert);
        Check(shotgun.currentAmmo==2&&shotgun.reserveAmmo==9,"Shotgun commits each inserted shell before the next insertion");
        weapons.CancelForModal();shotgun.Action.Tick(10);
        Check(shotgun.currentAmmo==2&&shotgun.reserveAmmo==9&&shotgun.Action.State==WeaponActionState.Ready,"Modal cancellation keeps committed shells and cancels remaining insertions");
        weapons.EquipImmediate(0);yield return new WaitForSeconds(.3f);
        weapons.SelectWeapon(1);yield return null;weapons.CancelForModal();
        yield return new WaitForSeconds(.5f);
        Check(!weapons.IsSwitching&&weapons.CurrentIndex==0&&weapons.Weapons.Count(w=>w.gameObject.activeSelf)==1,"Cancelling holster keeps one coherent equipped weapon");
        weapons.SelectWeapon(1);yield return new WaitForSeconds(.19f);weapons.CancelForModal();
        yield return new WaitForSeconds(.4f);
        Check(!weapons.IsSwitching&&weapons.CurrentIndex==1&&weapons.Weapons.Count(w=>w.gameObject.activeSelf)==1,"Cancelling draw retains newly equipped weapon without a later switch");
        Check((bool)Get(weapons,"requireRelease"),"Closing a modal requires releasing fire before combat input resumes");
        weapons.EquipImmediate(0);
        foreach(var gun in weapons.Weapons.OfType<BaseGun>()){gun.currentAmmo=gun.ammo;gun.reserveAmmo=gun.maxReserveAmmo;}
    }
    IEnumerator SpecialChecks()
    {
        var shield=player.GetComponent<Shield>();
        var shotgun=weapons.Weapons.OfType<ShotGun>().First();
        var prefab=(GameObject)Get(GameManager.Instance,"enemyPrefab");
        int attributed=0;Action<PlayerKill> observed=k=>attributed++;CombatFeedback.PlayerEliminated+=observed;
        using(var shieldReward=new KillShieldReward(rewards.Context,5))
        using(var loader=new AutoLoaderReward(rewards.Context,.1f))
        {
            weapons.EquipImmediate(2);yield return new WaitForSeconds(.3f);
            shotgun.currentAmmo=3;shotgun.reserveAmmo=17;shield.SetMaxShield(50);shield.ConsumeDamage(30);
            var enemy=SpawnTarget(prefab,15,4);
            float hip=shotgun.Recoil.spreadHip,ads=shotgun.Recoil.spreadADS;shotgun.Recoil.spreadHip=shotgun.Recoil.spreadADS=0;
            Aim(enemy.AimPoint);int reserve=shotgun.reserveAmmo;shotgun.Use();yield return null;
            Check(enemy.IsDead&&attributed==1,"Eight pellet shot attributes one player kill");
            Check(shield.Current==25,"Kill restores exactly five shield");
            Check(shotgun.currentAmmo==3&&shotgun.reserveAmmo==reserve-1,"Autoloader transfers one shotgun shell from real reserve");
            CombatFeedback.ReportPlayerKill(enemy,shotgun);
            Check(attributed==1&&shield.Current==25,"Repeated kill notification cannot duplicate special effects");
            Destroy(enemy.gameObject);
            enemy=SpawnTarget(prefab,10,4);float before=shield.Current;reserve=shotgun.reserveAmmo;
            enemy.TakeDamage(1000);yield return null;
            Check(attributed==1&&shield.Current==before&&shotgun.reserveAmmo==reserve,"Environmental/direct death grants no player special");
            Destroy(enemy.gameObject);
            yield return new WaitForSeconds(.85f);
            enemy=SpawnTarget(prefab,10,4);shotgun.currentAmmo=2;shotgun.reserveAmmo=0;Aim(enemy.AimPoint);shotgun.Use();yield return null;
            Check(shotgun.currentAmmo==1&&shotgun.reserveAmmo==0,"Autoloader cannot create ammo without reserve");
            Destroy(enemy.gameObject);shotgun.Recoil.spreadHip=hip;shotgun.Recoil.spreadADS=ads;
            weapons.EquipImmediate(3);yield return new WaitForSeconds(.3f);
            enemy=SpawnTarget(prefab,20,1.3f);before=shield.Current;reserve=shotgun.reserveAmmo;Aim(enemy.AimPoint);
            weapons.currentWeapon.Use();yield return new WaitForSeconds(.25f);
            Check(enemy.IsDead&&shield.Current==before+5&&shotgun.reserveAmmo==reserve,"Knife kill restores shield without loading another weapon");Destroy(enemy.gameObject);
        }
        CombatFeedback.PlayerEliminated-=observed;
        player.GetComponent<Shield>().SetMaxShield(50);
        foreach(var gun in weapons.Weapons.OfType<BaseGun>()){gun.currentAmmo=gun.ammo;gun.reserveAmmo=gun.maxReserveAmmo;}
    }
    NormalEnemyStateMachine SpawnTarget(GameObject prefab,float health,float distance)
    {
        var enemy=Instantiate(prefab,new Vector3(0,0,-16+distance),Quaternion.Euler(0,180,0)).GetComponent<NormalEnemyStateMachine>();
        enemy.ConfigureDifficulty(health,5,.5f);enemy.enabled=false;
        if(enemy.agent.isOnNavMesh)enemy.agent.isStopped=true;
        return enemy;
    }
    void Aim(Vector3 point)
    {
        var rotation=Quaternion.LookRotation(point-view.transform.position);
        player.transform.rotation=Quaternion.Euler(0,rotation.eulerAngles.y,0);
        player.headCam.localRotation=Quaternion.Euler(rotation.eulerAngles.x,0,0);Physics.SyncTransforms();
    }
    IEnumerator IntegratedRun()
    {
        var manager=GameManager.Instance;var floor=session.CurrentFloor;
        manager.StopFloor();floor.introductionSeconds=.1f;
        Set(manager,"spawnInterval",.08f);Set(manager,"announcementDuration",.15f);
        manager.ConfigureFloor(floor);
        int startOffers=rewards.OffersIssued,startWaves=session.RunWaves;
        for(int wave=1;wave<=4;wave++)
        {
            if(wave==4)Check(manager.RequestExtraWave(),"Player can explicitly start an extra wave after choosing");
            float deadline=Time.realtimeSinceStartup+18;bool answered=false;
            while((!answered||rewards.Pending)&&Time.realtimeSinceStartup<deadline)
            {
                player.Heal(100);
                foreach(var enemy in Object.FindObjectsByType<NormalEnemyStateMachine>(FindObjectsSortMode.None))
                    if(!enemy.IsDead)enemy.TakeDamage(10000);
                if(rewards.State==RewardState.Choosing){answered=rewards.Confirm(0);}
                yield return null;
            }
            Check(answered&&manager.CompletedWaves==wave&&rewards.OffersIssued==startOffers+wave,"Exactly one card for actual wave "+wave);
        }
        Check(session.RunWaves==startWaves+4,"Run wave count tracks three required waves and one extra");
        int rankSum=rewards.catalog.Sum(c=>rewards.Level(c));
        var oldRewards=rewards;string next=floor.nextScene;
        player.TeleportTo(floor.arrival.position,floor.arrival.rotation);
        yield return null;
        Check(session.RequestTravel(floor.elevator),"Elevator works after reward resolves");
        yield return WaitFor(()=>!session.IsTransitioning,20,"Floor transition completes");
        Check(session.CurrentFloor.gameObject.scene.name==next&&session.Rewards==oldRewards&&rewards.catalog.Sum(c=>rewards.Level(c))==rankSum,"Cards and service persist across floor transition");
        manager.StopFloor();player.enabled=false;
        // Die in the completion frame before the delayed pause is acquired.
        int oldPlayer=player.GetInstanceID();int cores=session.Progression.Data.cores;
        rewards.QueueWave(98,session.RunWaves+1,false);player.TakeDamage(100000,player.transform.position,"QA de prioridades");
        yield return null;
        Check(session.RunEnded&&session.Menu.ShowingDeath&&!rewards.Pending&&Time.timeScale==0,"Death aborts a pending reward and retains death pause");
        session.RestartRun();
        yield return WaitFor(()=>TowerSession.Instance!=null&&TowerSession.Instance!=session&&TowerSession.Instance.CurrentFloor!=null,45,"Retry starts a new run");
        var fresh=TowerSession.Instance;
        Check(fresh.player.GetInstanceID()!=oldPlayer&&fresh.Rewards.catalog.All(c=>fresh.Rewards.Level(c)==0),"Retry clears card ranks and creates fresh player");
        Check(fresh.weapons.Weapons.OfType<Pistol>().First().damage==30&&fresh.player.Health==100&&fresh.player.GetComponent<Shield>().Max==50,"Retry resets modifiers, health and shield");
        Check(fresh.Progression.Data.cores==cores&&!RunPause.IsPaused&&Time.timeScale==1,"Permanent cores survive; no pause lease leaks into retry");
        GameManager.Instance.StopFloor();
    }
}
#endif
