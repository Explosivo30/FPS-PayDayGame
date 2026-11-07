#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

/// <summary>Opt-in contract tests through the actual player, enemy prefabs and tower scenes.</summary>
public sealed class ReactiveGameplayPlaytest : MonoBehaviour
{
    readonly List<string> checks=new List<string>(),errors=new List<string>();
    readonly List<GameObject> fixtures=new List<GameObject>();
    TowerSession session;PlayerStateMachine player;GameObject rangedPrefab,meleePrefab;
    string output;float began;bool finished;Camera view;
    readonly Vector3 home=new Vector3(0,1.05f,-16);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if(!Environment.GetCommandLineArgs().Contains("-gameplay-qa")||Object.FindFirstObjectByType<ReactiveGameplayPlaytest>()!=null)return;
        var go=new GameObject("Reactive gameplay validation");DontDestroyOnLoad(go);go.AddComponent<ReactiveGameplayPlaytest>();
    }
    void Awake()
    {
        var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-gameplay-output");
        output=index>=0&&index+1<args.Length?args[index+1]:Path.GetFullPath("Artifacts/Gameplay-2026-10-21");
        Directory.CreateDirectory(output);Application.logMessageReceived+=Log;Application.runInBackground=true;began=Time.realtimeSinceStartup;
    }
    void Update(){if(!finished&&Time.realtimeSinceStartup-began>300){Check(false,"Gameplay watchdog");Finish();}}
    void Log(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(text+"\n"+stack);}
    void Check(bool ok,string label){checks.Add((ok?"PASS ":"FAIL ")+label);Debug.Log(checks.Last());if(!ok)errors.Add(label);}
    static object Get(object target,string name)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
    static void Set(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
    IEnumerator Until(Func<bool> predicate,float seconds,string label)
    {float end=Time.realtimeSinceStartup+seconds;while(!predicate()&&Time.realtimeSinceStartup<end)yield return null;Check(predicate(),label);}
    IEnumerator Start()
    {
        yield return Until(()=>TowerSession.Instance!=null&&TowerSession.Instance.CurrentFloor!=null,70,"Real tower/player boot");
        session=TowerSession.Instance;if(session==null){Finish();yield break;}
        player=session.player;player.enabled=false;session.weapons.AutomatedInput=true;
        rangedPrefab=(GameObject)Get(GameManager.Instance,"enemyPrefab");meleePrefab=(GameObject)Get(GameManager.Instance,"meleeEnemyPrefab");
        view=player.GetComponentsInChildren<Camera>().First(c=>c.gameObject.layer!=8);
        Stop();player.TeleportTo(home,Quaternion.identity);
        yield return Budget();yield return PulseBudget();yield return Projectiles();yield return EnemyRoles();yield return SpawnRecovery();
        Stop();
        for(int floor=0;floor<3;floor++)
        {
            yield return Arena();
            if(floor<2){yield return CompleteFloor();yield return Travel();Stop();}
        }
        yield return CompleteFloor();yield return Travel();Stop();yield return null;
        Check(Object.FindObjectsByType<ArenaBreakableCover>(FindObjectsSortMode.None).All(c=>!c.IsBroken),"A new floor visit restores its cover without resetting it between waves");
        yield return Until(()=>EnemyProjectile.ActiveCount==0&&CombatHazardBudget.ActiveSalvos==0&&CombatHazardBudget.ActivePrimary==0,2,"No attack resources survive floor stop");
        var finalOwner=Enemy(false,false,false);
        CombatHazardBudget.TryReserveSalvo(finalOwner,out var finalLease);var finalVolley=new EnemyProjectile.Volley(finalLease);
        EnemyProjectile.Launch(finalOwner,finalOwner.AimPoint,Vector3.up,1,1,5,.08f,finalOwner.layerMask,finalVolley);finalVolley.FinishEmission();
        player.TakeDamage(10000,finalOwner.transform.position,"Validation death");
        Check(session.RunEnded&&player.IsDead&&EnemyProjectile.ActiveCount==0&&CombatHazardBudget.ActiveSalvos==0&&CombatHazardBudget.ActivePrimary==0,"Actual player death clears travelling dangers immediately");
        Finish();
    }
    void Stop()
    {
        GameManager.Instance.StopFloor();
        foreach(var pulse in Object.FindObjectsByType<RootPulse>(FindObjectsSortMode.None))pulse.enabled=false;
        foreach(var fixture in fixtures)if(fixture!=null)Destroy(fixture);fixtures.Clear();
    }
    NormalEnemyStateMachine Enemy(bool melee,bool veteran,bool active,float distance=10)
    {
        var go=Instantiate(melee?meleePrefab:rangedPrefab,home+Vector3.forward*distance-Vector3.up*home.y,Quaternion.Euler(0,180,0));fixtures.Add(go);
        var enemy=go.GetComponent<NormalEnemyStateMachine>();enemy.ConfigureDifficulty(100,7,1);enemy.ConfigureVeteran(veteran);enemy.enabled=active;
        if(!active&&enemy.agent.isOnNavMesh)enemy.agent.isStopped=true;return enemy;
    }
    IEnumerator Budget()
    {
        var owner=new GameObject("Budget fixture");fixtures.Add(owner);
        bool first=CombatHazardBudget.TryReserveSalvo(owner,out var a),second=CombatHazardBudget.TryReserveSalvo(owner,out var b);
        Check(first&&second,"Two persistent salvos admitted");
        Check(!CombatHazardBudget.TryReserveSalvo(owner,out _),"Third live salvo is refused");
        CombatHazardBudget.TryReservePrimary(owner,out var primary);
        Check(!CombatHazardBudget.TryReservePrimary(owner,out _),"Laser/charge/environment share one main slot");
        a.Dispose();a.Dispose();b.Dispose();primary.Dispose();
        Check(CombatHazardBudget.ActiveSalvos==0&&CombatHazardBudget.ActivePrimary==0,"Reservations release exactly once");
        using(var pause=RunPause.Acquire(false))Check(!CombatHazardBudget.TryReservePrimary(owner,out _),"Pause refuses new danger");
        CombatHazardBudget.TryReservePrimary(owner,out primary);Destroy(owner);yield return null;
        Check(!primary.IsValid&&CombatHazardBudget.ActivePrimary==0,"Destroyed owner cannot hold budget");
    }
    IEnumerator Projectiles()
    {
        var enemy=Enemy(false,false,false);yield return null;
        Vector3 target=player.GetComponent<CharacterController>().bounds.center;
        Vector3 origin=enemy.transform.position+Vector3.up*1.2f;
        float health=player.Health+player.GetComponent<Shield>().Current;
        var bullet=EnemyProjectile.Launch(enemy,origin,target-origin,12,7,3,.08f,enemy.layerMask);
        yield return new WaitForSeconds(.1f);
        Check(bullet!=null&&bullet.IsLive&&bullet.Travelled>.3f&&player.Health+player.GetComponent<Shield>().Current==health,"Projectile travels before damage, rather than hitscan");
        using(var pause=RunPause.Acquire(false))
        {
            Vector3 position=bullet.transform.position;float life=bullet.RemainingLifetime;
            yield return new WaitForSecondsRealtime(.2f);
            Check(bullet.transform.position==position&&bullet.RemainingLifetime==life,"Pause freezes projectile and lifetime");
        }
        yield return Until(()=>!bullet.IsLive,2,"Travelling projectile reaches first collider");
        Check(player.Health+player.GetComponent<Shield>().Current<health,"Projectile damages actual player once on arrival");
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);fixtures.Add(wall);wall.layer=3;
        wall.transform.position=Vector3.Lerp(origin,target,.5f);wall.transform.localScale=new Vector3(4,4,.06f);Physics.SyncTransforms();
        health=player.Health+player.GetComponent<Shield>().Current;
        bullet=EnemyProjectile.Launch(enemy,origin,target-origin,200,7,2,.08f,enemy.layerMask);
        yield return Until(()=>!bullet.IsLive,1,"Swept projectile catches thin cover at high speed");
        Check(player.Health+player.GetComponent<Shield>().Current==health,"Thin cover prevents damage behind it");
        Destroy(wall);yield return null;
        CombatHazardBudget.TryReserveSalvo(enemy,out var lease);var volley=new EnemyProjectile.Volley(lease);
        bullet=EnemyProjectile.Launch(enemy,origin,Vector3.up,1,7,2,.08f,enemy.layerMask,volley);volley.FinishEmission();
        Check(CombatHazardBudget.ActiveSalvos==1,"Salvo budget stays occupied after emission while bullet lives");
        enemy.TakeDamage(10000);yield return null;
        Check(EnemyProjectile.ActiveCount==0&&CombatHazardBudget.ActiveSalvos==0,"Owner death cancels bullets and releases persistent salvo");
        Stop();yield return null;
    }
    IEnumerator PulseBudget()
    {
        var manager=GameManager.Instance;int previous=(int)Get(manager,"<CurrentWave>k__BackingField");Set(manager,"<CurrentWave>k__BackingField",1);
        var go=new GameObject("Pulse budget fixture");fixtures.Add(go);go.transform.position=home+Vector3.right*50;
        var pulse=go.AddComponent<RootPulse>();pulse.interval=2;pulse.warning=.3f;pulse.active=.25f;
        var ring=new GameObject("Warning outline fixture");ring.transform.SetParent(go.transform,false);pulse.ring=ring.transform;
        CombatHazardBudget.TryReservePrimary(this,out var occupied);pulse.Tick(2);
        Check(!pulse.IsWarning&&!pulse.IsActive,"Environmental pulse waits while main attack budget is occupied");
        occupied.Dispose();pulse.Tick(.01f);
        Check(pulse.IsWarning&&CombatHazardBudget.ActivePrimary==1,"RootPulse reserves shared budget before its warning");
        Vector3 radius=ring.transform.localScale;pulse.Tick(.1f);
        Check(ring.transform.localScale==radius&&radius.x>=pulse.radius*2,"Warning boundary never shrinks inside damaging radius");
        using(var pause=RunPause.Acquire(false))
        {yield return new WaitForSecondsRealtime(.15f);Check(pulse.IsWarning,"Pause freezes environmental warning");}
        pulse.Tick(.4f);
        Check(pulse.IsActive&&CombatHazardBudget.ActivePrimary==1,"Pulse keeps main slot throughout active damage window");
        pulse.enabled=false;
        Check(!pulse.IsActive&&CombatHazardBudget.ActivePrimary==0,"Disabling pulse clears its danger and releases budget");
        Set(manager,"<CurrentWave>k__BackingField",previous);Destroy(go);yield return null;
    }
    IEnumerator EnemyRoles()
    {
        player.TeleportTo(home,Quaternion.identity);
        var enemy=Enemy(false,false,true,10);Set(enemy.Ranged,"cooldown",0f);
        yield return Until(()=>enemy.Ranged.ProjectilesLaunched>=3,5,"Centinela emits three travelling projectiles");
        Check(enemy.Ranged.LaserShotsFired==0,"Centinela does not emit Aguja hitscan");
        Stop();yield return null;player.TeleportTo(home,Quaternion.identity);
        enemy=Enemy(false,true,true,10);Set(enemy.Ranged,"cooldown",0f);
        yield return Until(()=>enemy.Ranged.IsTargetLocked,5,"Aguja enters explicit target-lock phase");
        Vector3 committed=enemy.Ranged.CommittedTarget;
        using(var pause=RunPause.Acquire(false))
        {yield return new WaitForSecondsRealtime(.2f);Check(enemy.Ranged.IsTargetLocked&&enemy.Ranged.LaserShotsFired==0,"Pause freezes Aguja lock before firing");}
        player.TeleportTo(home+Vector3.right*3,Quaternion.identity);
        yield return Until(()=>enemy.Ranged.LaserShotsFired==1,3,"Aguja fires one announced precision laser");
        Check(enemy.Ranged.CommittedTarget==committed&&enemy.Ranged.ProjectilesLaunched==0,"Aguja does not follow dodge after lock or become ordinary salvo");
        yield return new WaitForSeconds(.35f);
        Check(enemy.Ranged.LaserShotsFired==1,"Aguja recovery prevents second instant shot");
        Stop();yield return null;player.TeleportTo(home,Quaternion.identity);
        enemy=Enemy(true,true,true,2.7f);var melee=enemy.GetComponent<MeleeRobotCombat>();
        yield return Until(()=>melee.Phase==MeleeRobotPhase.Windup,5,"Ariete visibly commits charge windup");
        Check(melee.windupSeconds>=.8f&&melee.recoverySeconds>=1,"Ariete keeps response and punishment windows");
        player.TeleportTo(home+Vector3.right*3,Quaternion.identity);
        yield return Until(()=>melee.Phase==MeleeRobotPhase.Recover,3,"Ariete resolves committed charge and recovers");
        Check(melee.ContactsResolved==1&&melee.ChargeTravel<=1.21f,"Charge has one contact and preserves bounded movement");
        Stop();yield return null;player.TeleportTo(home,Quaternion.identity);
    }
    IEnumerator SpawnRecovery()
    {
        var manager=GameManager.Instance;var floor=session.CurrentFloor;
        var previous=(Transform[])Get(manager,"spawnPoints");float intro=floor.introductionSeconds;
        float fallback=(float)Get(manager,"visibleEntryFallbackAfter");var camera=view.transform;
        Vector3 chosen=Vector3.zero;bool found=false;
        for(int z=-6;z<=4&&!found;z+=2)for(int x=-8;x<=8&&!found;x+=2)
        {
            if(!NavMesh.SamplePosition(new Vector3(x,0,z),out var sample,.8f,NavMesh.AllAreas))continue;
            Aim(sample.position+Vector3.up);
            var safe=(bool)typeof(GameManager).GetMethod("SafeEntry",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(manager,new object[]{sample.position});
            var visible=(bool)typeof(GameManager).GetMethod("InDirectView",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(manager,new object[]{sample.position});
            if(safe&&visible){chosen=sample.position;found=true;}
        }
        Check(found,"Fixture finds a visible, distant, reachable physical entry");
        if(found)
        {
            var point=new GameObject("Visible entrance test");fixtures.Add(point);point.transform.position=chosen;
            floor.introductionSeconds=.05f;manager.ConfigureFloor(floor);Set(manager,"spawnPoints",new[]{point.transform});Set(manager,"visibleEntryFallbackAfter",.35f);
            int initial=manager.SpawnFallbacks;
            yield return Until(()=>manager.SpawnFallbacks>initial,5,"All-visible entrances recover with announced safe spawn");
            Check(manager.ActiveEnemies>0&&manager.PendingSpawns<manager.WavePlan.Count,"Fallback really spawns instead of deleting pending enemies");
            var tracked=Object.FindObjectsByType<NormalEnemyStateMachine>(FindObjectsSortMode.None).FirstOrDefault(e=>!e.IsDead);
            if(tracked!=null)
            {
                int score=manager.GetPlayerPoints(),reconciled=manager.ReconciledEnemies;Destroy(tracked.gameObject);
                yield return Until(()=>manager.ReconciledEnemies>reconciled,2,"Destroyed unreported enemy cannot strand count");
                Check(manager.GetPlayerPoints()==score,"Bookkeeping recovery invents no reward");
            }
            Stop();floor.introductionSeconds=intro;Set(manager,"spawnPoints",previous);Set(manager,"visibleEntryFallbackAfter",fallback);
        }
        yield return null;
    }
    IEnumerator Arena()
    {
        string scene=session.CurrentFloor.gameObject.scene.name;
        var covers=Object.FindObjectsByType<ArenaBreakableCover>(FindObjectsSortMode.None).Where(x=>x.gameObject.scene.name==scene).ToArray();
        Check(covers.Length>0,scene+" contains installed reactive cover");
        foreach(var cover in covers)
        {
            var collider=cover.intactObject!=null?cover.intactObject.GetComponent<Collider>():null;
            if(collider==null)collider=cover.GetComponentInChildren<Collider>();
            if(collider==null){Check(false,scene+" cover has collider");continue;}
            var bounds=collider.bounds;Vector3 start=bounds.center+Vector3.forward*(bounds.extents.z+3);
            int screenMask=(1<<0)|(1<<3);
            Physics.SyncTransforms();bool blocked=Physics.Raycast(start,Vector3.back,out var hit,6,screenMask,QueryTriggerInteraction.Ignore)&&hit.collider.GetComponentInParent<ArenaBreakableCover>()==cover;
            Check(blocked,scene+" intact cover blocks real shot line");
            if(blocked)
            {
                // Use the installed screen and fixed plinth, not a synthetic breakable.
                Vector3 center=cover.transform.position,forward=cover.transform.forward,right=cover.transform.right;
                var ariete=Enemy(true,true,false);var melee=ariete.GetComponent<MeleeRobotCombat>();
                Vector3 entry=center-forward*2.2f;
                bool onNav=NavMesh.SamplePosition(entry,out var spawn,.7f,NavMesh.AllAreas);
                if(onNav)
                {
                    ariete.agent.Warp(spawn.position);ariete.transform.rotation=Quaternion.LookRotation(forward);
                    player.TeleportTo(center-forward*1.35f+Vector3.up*1.05f,Quaternion.LookRotation(-forward));Physics.SyncTransforms();ariete.enabled=true;
                    yield return Until(()=>melee.Phase==MeleeRobotPhase.Windup,4,scene+" Ariete can commit near actual screen");
                    player.TeleportTo(center-forward*1.35f+right*4+Vector3.up*1.05f,Quaternion.LookRotation(-forward));
                    yield return Until(()=>melee.Phase==MeleeRobotPhase.Recover,3,scene+" charge recovers against actual mount");
                    Check(cover.IsBroken&&melee.CoverImpacts==1&&melee.ContactsResolved==1&&melee.ChargeTravel<=1.21f,scene+" Ariete breaks actual screen exactly once without crossing plinth (travel="+melee.ChargeTravel.ToString("F2")+", impacts="+melee.CoverImpacts+")");
                }
                else Check(false,scene+" screen charge approach remains navigable");
                Destroy(ariete.gameObject);cover.ResetForVisit();yield return null;
                var pistol=session.weapons.Weapons.OfType<Pistol>().First();
                int index=Array.IndexOf(session.weapons.Weapons.ToArray(),pistol);session.weapons.EquipImmediate(index);
                player.TeleportTo(start,Quaternion.identity);Aim(bounds.center);yield return new WaitForSeconds(.5f);
                float shotBefore=cover.CurrentHealth;pistol.Use();yield return new WaitForSeconds(.12f);
                Check(Mathf.Abs(shotBefore-cover.CurrentHealth-pistol.damage)<.02f,scene+" actual pistol/muzzle impact applies exactly one damage event");
                float before=cover.CurrentHealth;
                cover.HandleRaycastHit(hit,1);
                Check(Mathf.Abs(cover.CurrentHealth-(before-1))<.01f,scene+" a weapon impact damages structure once");
                cover.ApplyStructuralImpact(10000,hit.point,Vector3.back);
                Check(cover.IsBroken,scene+" structural damage breaks designated screen");
                Physics.SyncTransforms();
                bool stillBlocked=Physics.Raycast(start,Vector3.back,out var after,6,screenMask,QueryTriggerInteraction.Ignore)&&after.collider.GetComponentInParent<ArenaBreakableCover>()==cover;
                Check(!stillBlocked,scene+" broken screen really opens line");
                Check(!cover.TryBreakFromCharge(hit.point,Vector3.back),scene+" repeated charge cannot break screen twice");
                Check(ArenaBreakableCover.AliveFragmentCount<=ArenaBreakableCover.MaximumActiveFragments,"Prepared fragments respect global active cap");
            }
        }
        foreach(var entry in session.CurrentFloor.spawnPoints)
        {
            bool valid=NavMesh.SamplePosition(entry.position,out var sample,2,NavMesh.AllAreas);
            var path=new NavMeshPath();valid&=NavMesh.CalculatePath(sample.position,session.CurrentFloor.arrival.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
            Check(valid,scene+" entry remains connected after destruction: "+entry.name);
        }
        var petal=Object.FindFirstObjectByType<ArenaMechanicalPetal>();
        if(petal!=null)
        {
            Check(PetalBlocksSideLine(petal),"Closed petal blocks a concrete side firing lane");
            var conductor=petal.GetComponentInChildren<ArenaPetalConductor>();
            Vector3 approach=conductor.transform.position-conductor.transform.forward*1.8f;
            player.TeleportTo(approach,Quaternion.identity);Aim(conductor.GetComponent<Collider>().bounds.center);
            var interaction=player.GetComponent<PlayerInteraction>();
            typeof(PlayerInteraction).GetMethod("Interact",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(interaction,null);
            bool initial=petal.IsOpen;
            Check(petal.CurrentState==ArenaMechanicalPetal.PetalState.Warning,"Player interaction ray activates marked conductor within reach");
            Check(!petal.Activate(),"Repeated activation cannot skip mechanism warning");
            using(var pause=RunPause.Acquire(false))
            {var state=petal.CurrentState;yield return new WaitForSecondsRealtime(.2f);Check(petal.CurrentState==state&&petal.IsOpen==initial,"Pause freezes mechanism transition");}
            yield return Until(()=>petal.IsOpen!=initial,6,"Mechanical petal completes a real state change");
            Check(!PetalBlocksSideLine(petal),"Opening petal really releases that same firing lane");
            petal.Tick(petal.cooldownSeconds+.1f);
            Vector3 previous=player.transform.position;
            player.TeleportTo(petal.transform.TransformPoint(petal.safetyCenter),Quaternion.identity);Physics.SyncTransforms();
            Check(petal.Activate(),"Petal can be reactivated after its cooldown");
            petal.Tick(petal.warningSeconds+.1f);petal.Tick(.2f);
            Check(petal.SafetyBlocked&&petal.MovementProgress==0,"An actor on the decorative island stops the complete sweep");
            player.TeleportTo(previous,Quaternion.identity);Physics.SyncTransforms();
            yield return Until(()=>petal.IsOpen==initial,5,"Mechanism resumes safely after actor leaves sweep");
            petal.Tick(petal.cooldownSeconds+.1f);
            var pistol=session.weapons.Weapons.OfType<Pistol>().First();
            session.weapons.EquipImmediate(Array.IndexOf(session.weapons.Weapons.ToArray(),pistol));
            player.TeleportTo(approach,Quaternion.identity);Aim(conductor.GetComponent<Collider>().bounds.center);yield return new WaitForSeconds(.5f);
            pistol.Use();yield return null;
            Check(petal.CurrentState==ArenaMechanicalPetal.PetalState.Warning,"Actual pistol hit activates conductor through normal gun hit path");
            Aim(petal.transform.position+Vector3.up*1.8f);
        }
        yield return new WaitForSeconds(.2f);Capture(scene+"-reactive.png");
    }
    bool PetalBlocksSideLine(ArenaMechanicalPetal petal)
    {
        Physics.SyncTransforms();
        Vector3 start=petal.panel.position+petal.transform.right*1.2f-petal.transform.forward*3;
        return Physics.Raycast(start,petal.transform.forward,out var hit,6,1<<0,QueryTriggerInteraction.Ignore)
            &&hit.collider.GetComponentInParent<ArenaMechanicalPetal>()==petal;
    }
    IEnumerator CompleteFloor()
    {
        // The shot-path fixture deliberately puts the disabled controller at eye height.
        // Resume the actual floor loop from its grounded arrival, rather than an airborne test pose.
        player.TeleportTo(session.CurrentFloor.arrival.position,session.CurrentFloor.arrival.rotation);
        var floor=session.CurrentFloor;float intro=floor.introductionSeconds;floor.introductionSeconds=.05f;
        Set(GameManager.Instance,"announcementDuration",.1f);GameManager.Instance.ConfigureFloor(floor);
        float deadline=Time.realtimeSinceStartup+70;Time.timeScale=6;
        while((GameManager.Instance.CompletedWaves<3||session.Rewards.Pending)&&Time.realtimeSinceStartup<deadline)
        {
            player.Heal(100);player.GetComponent<Shield>().Restore(100);
            foreach(var pulse in Object.FindObjectsByType<RootPulse>(FindObjectsSortMode.None))pulse.enabled=false;
            if(session.Rewards.State==RewardState.Choosing)session.Rewards.Confirm(0);
            foreach(var enemy in Object.FindObjectsByType<NormalEnemyStateMachine>(FindObjectsSortMode.None))if(!enemy.IsDead){enemy.TakeDamage(10000);enemy.TakeDamage(10000);}
            yield return null;
        }
        Time.timeScale=1;floor.introductionSeconds=intro;
        Check(GameManager.Instance.CompletedWaves==3&&GameManager.Instance.ActiveEnemies==0,"Actual mandatory waves complete with duplicate death protection");
        Check(Object.FindObjectsByType<ArenaBreakableCover>(FindObjectsSortMode.None).All(c=>c.IsBroken),"Destroyed cover remains broken through mandatory waves");
    }
    IEnumerator Travel()
    {
        var floor=session.CurrentFloor;player.TeleportTo(floor.arrival.position,floor.arrival.rotation);
        Check(session.RequestTravel(floor.elevator),"Real elevator accepts completed floor");
        yield return Until(()=>!session.IsTransitioning&&session.CurrentFloor!=floor,50,"Actual additive floor transition finishes");
        player.enabled=false;session.weapons.AutomatedInput=true;
    }
    void Aim(Vector3 point)
    {
        var look=Quaternion.LookRotation(point-view.transform.position);
        player.transform.rotation=Quaternion.Euler(0,look.eulerAngles.y,0);player.headCam.localRotation=Quaternion.Euler(look.eulerAngles.x,0,0);Physics.SyncTransforms();
    }
    void Capture(string file)
    {
        var target=new RenderTexture(1280,720,24);target.Create();
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(view,new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=target});
        var previous=RenderTexture.active;RenderTexture.active=target;
        var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
        File.WriteAllBytes(Path.Combine(output,file),image.EncodeToPNG());RenderTexture.active=previous;Destroy(image);target.Release();Destroy(target);
    }
    void Finish()
    {
        if(finished)return;finished=true;Time.timeScale=1;Application.logMessageReceived-=Log;
        File.WriteAllText(Path.Combine(output,"results.txt"),string.Join("\n",checks)+"\nERRORS\n"+string.Join("\n",errors));
        Debug.Log("REACTIVE GAMEPLAY TEST COMPLETE: "+checks.Count+" checks, "+errors.Count+" errors");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(errors.Count==0?0:1);
#else
        Application.Quit(errors.Count==0?0:1);
#endif
    }
}
#endif
