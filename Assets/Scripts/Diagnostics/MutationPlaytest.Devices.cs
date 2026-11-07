#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class MutationPlaytest
{
    IEnumerator DeviceCases()
    {
        yield return Reset();Give(MutationKind.ExplosiveReload,MutationKind.MagazineBomb);
        var enemy=Spawn(2.7f);for(int i=0;i<3;i++)Fire(pistol,enemy.AimPoint);
        float hp=enemy.Health;Reload(pistol);
        Check(Find(MutationWorldKind.TimedMagazine)!=null&&Find(MutationWorldKind.MagazineBomb)!=null,"Timed explosive reload and manually shot magazine bomb coexist as different devices");
        Advance(.8f);Check(enemy.Health==hp,"Timed magazine respects its one-second warning");
        Advance(.3f);Check(enemy.Health<hp&&Find(MutationWorldKind.MagazineBomb)!=null,"Timed explosion damages enemies but does not chain-detonate the manual bomb");
        var bomb=Find(MutationWorldKind.MagazineBomb);hp=enemy.Health;
        Fire(pistol,bomb.transform.position+Vector3.up*.24f);
        Check(Find(MutationWorldKind.MagazineBomb)==null&&enemy.Health<hp,"Shooting the real bomb collider detonates it exactly once");
        int derived=run.DerivedHits;Advance(.5f);Check(run.DerivedHits==derived,"No delayed second detonation after a manual bomb is consumed");
        Capture("06-magazine-devices.png");

        yield return Reset();Give(MutationKind.MagazineBomb);pistol.currentAmmo=0;Reload(pistol);
        Check(Find(MutationWorldKind.MagazineBomb)==null,"Magazine bomb requires a partial magazine, not an empty reload");
        yield return Reset();Give(MutationKind.ExplosiveReload);pistol.currentAmmo=pistol.ammo-1;Reload(pistol);
        Check(Find(MutationWorldKind.TimedMagazine)==null,"Explosive reload cannot be farmed without firing three bullets");

        yield return Reset();Give(MutationKind.DecoyBeacon);pistol.currentAmmo=0;Reload(pistol,true);
        var decoy=Find(MutationWorldKind.Decoy);enemy=Spawn(8);
        enemy.Perception.Tick();Check(decoy!=null&&enemy.Perception.UsingDecoy&&Vector3.Distance(enemy.Perception.LastKnown,decoy.transform.position)<.01f,"Decoy changes the real enemy's perceived combat target");
        typeof(RangedRobotCombat).GetField("cooldown",Private).SetValue(enemy.Ranged,0f);enemy.Ranged.Tick();
        Check(decoy!=null&&Vector3.Distance(enemy.GetComponent<RobotPresentation>().AimTarget,decoy.transform.position+Vector3.up*.6f)<.1f,"The ranged body and weapon aim at the decoy rather than pretending in VFX");
        Fire(pistol,enemy.AimPoint);typeof(EnemyPerception).GetField("nextSense",Private).SetValue(enemy.Perception,0f);enemy.Perception.Tick();
        Check(!enemy.Perception.UsingDecoy,"A direct hit breaks that robot's distraction");
        Advance(2.1f);Check(Find(MutationWorldKind.Decoy)==null,"Decoy expires and leaves no permanent target");

        yield return Reset();Give(MutationKind.MagneticScrap);
        var weak=Spawn(5,0,false,false,1);enemy=Spawn(5,1.8f);Vector3 before=enemy.transform.position;
        Fire(pistol,weak.AimPoint);Check(Find(MutationWorldKind.ScrapMagnet)!=null,"A real direct kill creates magnetic scrap at the corpse");
        Advance(.4f);yield return null;
        Check(Vector3.Distance(enemy.transform.position,weak.transform.position)<Vector3.Distance(before,weak.transform.position)-.1f,"Magnetic scrap moves a nearby robot toward the kill site");
        Advance(1.2f);Check(Find(MutationWorldKind.ScrapMagnet)==null&&run.Cooldown(MutationKind.MagneticScrap)>6,"Scrap magnet has bounded duration and an eight-second cooldown");

        yield return Reset();Give(MutationKind.MagneticAnchor);enemy=Spawn(4,1.7f);var heavy=Spawn(4,-1.7f,true,true);
        before=enemy.transform.position;Vector3 heavyBefore=heavy.transform.position;float speed=heavy.agent.speed;
        pistol.currentAmmo-=1;Aim(new Vector3(0,0,-12));Reload(pistol);Advance(.4f);yield return null;
        Check(Find(MutationWorldKind.Anchor)!=null&&Mathf.Abs(enemy.transform.position.x)<Mathf.Abs(before.x)-.1f,"Reloading toward valid ground places an anchor and groups light robots");
        Check(Vector3.Distance(heavyBefore,heavy.transform.position)<.05f&&heavy.agent.speed<speed,"Anchor slows veterans without pulling their bodies");
        Capture("07-magnetic-anchor.png");Advance(2);yield return null;
        Check(Mathf.Abs(heavy.agent.speed-speed)<.01f,"The veteran's movement speed is restored after the magnetic field");

        yield return Reset();Give(MutationKind.WorkHammer);enemy=Spawn(5);
        for(int i=0;i<3;i++)Fire(pistol,enemy.AimPoint);
        Check(run.HammerHits==3,"Three distinct direct shots mark the same enemy for the hammer");
        hp=enemy.Health;Reload(pistol);Advance(.25f);Check(Find(MutationWorldKind.Hammer)!=null&&enemy.Health==hp,"Hammer telegraphs a committed world position before contact");
        Capture("08-hammer-warning.png");Advance(.6f);Check(enemy.Health<hp,"Hammer strikes the committed position after its warning");
        yield return Reset();Give(MutationKind.WorkHammer);enemy=Spawn(5);
        for(int i=0;i<3;i++)Fire(pistol,enemy.AimPoint);hp=enemy.Health;Reload(pistol);
        enemy.agent.Warp(enemy.transform.position+Vector3.right*3);Physics.SyncTransforms();Advance(.9f);
        Check(enemy.Health==hp,"An enemy can move out of the hammer instead of being homed on after lock");

        yield return Reset();Give(MutationKind.RecoverableSaw);
        for(int i=0;i<3;i++){weak=Spawn(5+i*1.5f,0,false,false,1);Fire(pistol,weak.AimPoint);}
        Check(run.SawReady&&run.SawCharge==3,"Three direct firearm kills prepare one recoverable saw");
        enemy=Spawn(5);Aim(enemy.AimPoint);Reload(pistol);Advance(.75f);
        var saw=Find(MutationWorldKind.Saw);
        Check(saw!=null&&saw.Dropped&&enemy.Health==950,"Reload launches a physical saw with one hit per enemy, then it drops");
        if(saw!=null){player.TeleportTo(saw.transform.position+Vector3.up*1.05f,Quaternion.identity);Advance(.05f);}
        Check(run.SawReady&&Find(MutationWorldKind.Saw)==null,"Picking up the dropped saw restores its availability");

        yield return Reset();Give(MutationKind.DischargeCoil);player.TakeDamage(50);pistol.currentAmmo=0;Reload(pistol,true);
        var coil=Find(MutationWorldKind.Coil);enemy=Spawn(8);
        Check(coil!=null,"Breaking shield and reloading creates a discharge coil");
        if(coil!=null)
        {
            Vector3 origin=coil.transform.position+Vector3.forward*2+Vector3.up*.7f;
            var bullet=EnemyProjectile.Launch(enemy,origin,Vector3.back,12,10,1,.08f,~(1<<6|1<<8|1<<2));
            yield return new WaitForSeconds(.3f);
            Check(coil.Captured==1&&bullet!=null&&!bullet.IsLive&&player.Health==100,"A real travelling enemy projectile is intercepted and stored");
            EnemyProjectile.Launch(enemy,origin,Vector3.back,12,10,1,.08f,~(1<<6|1<<8|1<<2));yield return new WaitForSeconds(.3f);
            var veteran=Spawn(9,0,false,true);
            typeof(RangedRobotCombat).GetField("cooldown",Private).SetValue(veteran.Ranged,0f);
            float deadline=Time.realtimeSinceStartup+4;
            while(veteran.Ranged.LaserShotsFired==0&&Time.realtimeSinceStartup<deadline){veteran.Ranged.Tick();yield return null;}
            Check(veteran.Ranged.LaserShotsFired==1&&coil.Captured==3&&player.Health==100,"Aguja's actual locked laser is captured as the coil's third charge");
            var tracer=veteran.GetComponent<LineRenderer>();
            Check(tracer!=null&&tracer.enabled&&Vector3.Distance(tracer.GetPosition(1),coil.transform.position+Vector3.up*.95f)<1.1f,"The absorbed third laser visibly ends at the coil instead of passing through");
            veteran.Ranged.Cancel();
            if(coil==null){Check(false,"Coil survived long enough for saturation case");yield break;}
            Check(coil.Captured==3&&!coil.Capture(),"Coil stores at most three shots");
            bool struck=MutationObject.EnemyRay(player.transform.position+Vector3.forward*3+Vector3.up*.6f,Vector3.back,out var next,5,~(1<<6|1<<8|1<<2));
            Check(struck&&next.collider.GetComponentInParent<PlayerStateMachine>()==player,"A saturated coil no longer grants unlimited cover");
            var nearby=Spawn(2.5f);hp=nearby.Health;Fire(pistol,coil.transform.position+Vector3.up*.8f);
            Check(!coil.Live&&nearby.Health<hp,"The player's shot releases the stored energy into nearby enemies");
            Capture("09-coil-discharge.png");
        }
    }
    IEnumerator LifecycleCases()
    {
        yield return Reset();Give(MutationKind.DoubleFuse,MutationKind.MagneticScrap,MutationKind.GroundArc);
        var first=Spawn(5,0,false,false,40);var neighbor=Spawn(5,1.4f,false,false,1);
        int direct=0,secondary=0;Action<PlayerKill> observed=k=>{if(k.Direct)direct++;else secondary++;};CombatFeedback.PlayerEliminated+=observed;
        Fire(pistol,first.AimPoint);Fire(pistol,first.AimPoint);
        Check(first.IsDead&&neighbor.IsDead&&direct==1&&secondary==1,"Explosions preserve exactly one attributed kill for each victim");
        Check(run.ArcCharge==1,"Derived kills do not recursively charge direct-kill cards");
        CombatFeedback.PlayerEliminated-=observed;

        yield return Reset();foreach(var card in session.Rewards.catalog.Where(c=>c.kind==RewardKind.Mutation))run.Unlock(card);
        Check(run.Owned.Count()==19&&!run.Unlock(Card(MutationKind.RamChamber)),"All selected cards can coexist, and a duplicate cannot install twice");
        first=Spawn(5);for(int i=0;i<4;i++)Fire(pistol,first.AimPoint);Reload(pistol);
        float clock=run.Clock;var fx=Find(MutationWorldKind.TimedMagazine);float remaining=fx!=null?fx.Remaining:0;
        using(var pause=RunPause.Acquire(false))
        {
            run.Tick(20);yield return new WaitForSecondsRealtime(.1f);
            Check(run.Clock==clock&&(fx==null||fx.Remaining==remaining),"Pause freezes every mutation clock, warning and cooldown");
        }
        Advance(1.1f);Check(run.EffectCount<=64&&run.DerivedHits<50,"Combined cards remain bounded and do not cascade indefinitely");

        yield return Reset();
        var manager=GameManager.Instance;float intro=session.CurrentFloor.introductionSeconds;
        session.CurrentFloor.introductionSeconds=.05f;
        typeof(GameManager).GetField("spawnInterval",Private).SetValue(manager,.05f);
        manager.ConfigureFloor(session.CurrentFloor);
        float until=Time.realtimeSinceStartup+40;
        while((manager.CurrentWave==0||manager.IsSpawning)&&Time.realtimeSinceStartup<until)
        {
            player.Heal(1000);player.GetComponent<Shield>().Restore(100);yield return null;
        }
        Check(manager.CurrentWave==1&&!manager.IsSpawning&&manager.ActiveEnemies>0,"A complete real wave enters the actual wave ledger: wave="+manager.CurrentWave+" active="+manager.ActiveEnemies+" pending="+manager.PendingSpawns+" wait="+manager.SpawnWaitReason);
        int count=manager.ActiveEnemies,money=manager.GetPlayerPoints();
        foreach(var e in UnityEngine.Object.FindObjectsByType<NormalEnemyStateMachine>(FindObjectsSortMode.None).Where(e=>!e.IsDead))
        {run.Deal(e,100000,pistol);run.Deal(e,100000,pistol);}
        yield return null;yield return null;
        Check(manager.ActiveEnemies==0&&manager.EnemiesRemaining==0&&manager.CompletedWaves==1,"Derived damage completes a real wave exactly once without leaving a missing enemy");
        Check(manager.GetPlayerPoints()==money+count*10+30,"Derived kills retain normal chips and the wave completion reward");
        until=Time.realtimeSinceStartup+4;
        while(session.Rewards.State!=RewardState.Choosing&&Time.realtimeSinceStartup<until)yield return null;
        Check(session.Rewards.Offer.Count==3&&session.Rewards.Offer.All(c=>c.kind==RewardKind.Mutation),"Real wave reward offers three selected, compatible mutations");
        yield return new WaitForSecondsRealtime(.25f);
        Capture("10-mutation-offer.png");
        var chosen=session.Rewards.Offer.FirstOrDefault();
        Check(session.Rewards.Confirm(0)&&!session.Rewards.Confirm(0),"The real card UI selection path applies only once");
        yield return new WaitForSecondsRealtime(.6f);
        Check(chosen!=null&&run.Has(chosen.mutation)&&session.Rewards.Level(chosen)==1,"Confirmed mutation is installed and recorded in run history");
        manager.StopFloor();session.CurrentFloor.introductionSeconds=intro;

        foreach(var card in session.Rewards.catalog.Where(c=>c.kind==RewardKind.Mutation))run.Unlock(card);
        run.Spawn(MutationWorldKind.MagazineBomb,run.Ground(home+Vector3.forward),6,pistol,60);
        var previous=session.CurrentFloor.gameObject.scene;
        yield return SceneManager.LoadSceneAsync("LaboratoryFloor",LoadSceneMode.Additive);
        var newScene=SceneManager.GetSceneByName("LaboratoryFloor");TowerFloor nextFloor=null;
        foreach(var root in newScene.GetRootGameObjects()){nextFloor=root.GetComponentInChildren<TowerFloor>();if(nextFloor!=null)break;}
        session.EnterFloor(nextFloor);manager.StopFloor();yield return SceneManager.UnloadSceneAsync(previous);
        Check(run.Owned.Count()==19&&run.EffectCount==0,"Actual additive floor load/unload retains all cards and clears old physical effects");

        run.Spawn(MutationWorldKind.Coil,run.Ground(player.transform.position+Vector3.forward),4,pistol,25);
        player.TakeDamage(100000);yield return null;
        Check(session.RunEnded&&run.EffectCount==0,"Player death cleans devices, flying blades and all active mutations");
        session.RestartRun();until=Time.realtimeSinceStartup+25;
        while((TowerSession.Instance==null||TowerSession.Instance==session||TowerSession.Instance.CurrentFloor==null)&&Time.realtimeSinceStartup<until)yield return null;
        var fresh=TowerSession.Instance;
        Check(fresh!=null&&fresh!=session&&MutationRun.For(fresh).Owned.Count()==0,"Restart through the real run menu starts with no previous mutations");
    }
}
#endif
