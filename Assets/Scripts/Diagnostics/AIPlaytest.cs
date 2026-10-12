#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

public partial class AIPlaytest : MonoBehaviour
{
    readonly List<string> checks=new List<string>(),errors=new List<string>();
    PlayerStateMachine player;TowerSession session;GameObject rangedPrefab,meleePrefab;
    readonly List<NormalEnemyStateMachine> fixtures=new List<NormalEnemyStateMachine>();
    string output;Camera view;RenderTexture target;float began;bool finished;
    readonly Vector3 home=new Vector3(0,1.05f,-16);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var args=Environment.GetCommandLineArgs();
        if(!args.Contains("-ai-qa")&&!args.Contains("-ai-record"))return;
        if(Object.FindFirstObjectByType<AIPlaytest>()==null){var go=new GameObject("AI validation");DontDestroyOnLoad(go);go.AddComponent<AIPlaytest>();}
    }
    void Awake()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-ai-output");
        output=i>=0&&i+1<args.Length?args[i+1]:Path.GetFullPath("Artifacts/AI");
        Directory.CreateDirectory(output);Application.logMessageReceived+=Log;Application.runInBackground=true;began=Time.realtimeSinceStartup;
    }
    void Update(){if(!finished&&Time.realtimeSinceStartup-began>200){Check(false,"AI validation watchdog");Finish();}}
    void Log(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(text+"\n"+stack);}
    void Check(bool pass,string label){checks.Add((pass?"PASS ":"FAIL ")+label);Debug.Log(checks.Last());if(!pass)errors.Add(label);}
    static object Get(object o,string field)=>o.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
    static void Set(object o,string field,object value)=>o.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,value);
    IEnumerator Until(Func<bool> condition,float seconds,string label)
    {float end=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<end)yield return null;Check(condition(),label);}
    IEnumerator Start()
    {
        yield return Until(()=>TowerSession.Instance!=null&&TowerSession.Instance.CurrentFloor!=null,80,"Tower boots");
        session=TowerSession.Instance;if(session==null){Finish();yield break;}
        player=session.player;player.enabled=false;session.weapons.AutomatedInput=true;
        foreach(var pulse in Object.FindObjectsByType<RootPulse>(FindObjectsSortMode.None))pulse.enabled=false;
        rangedPrefab=(GameObject)Get(GameManager.Instance,"enemyPrefab");meleePrefab=(GameObject)Get(GameManager.Instance,"meleeEnemyPrefab");
        GameManager.Instance.StopFloor();player.TeleportTo(home,Quaternion.identity);
        view=player.GetComponentsInChildren<Camera>().First(c=>c.gameObject.layer!=8);
        target=new RenderTexture(1920,1080,24);target.Create();view.targetTexture=target;
        foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=view;canvas.planeDistance=.15f;}
        if(Environment.GetCommandLineArgs().Contains("-ai-record")){yield return Record();Finish();yield break;}
        var p=session.Threat.Profile;
        foreach(int wave in new[]{1,3,6,9})
        {
            var plan=new WaveThreat(p,wave,100,10);
            Check(plan.Count==5+2*(wave-1)&&plan.AttackLimit==(wave>=6?4:wave>=3?3:2),"Wave "+wave+" count and simultaneous attack budget");
            Check(Mathf.Abs(plan.Health-(100+6*(wave-1)))<.001f&&Mathf.Abs(plan.Damage-(10+.4f*(wave-1)))<.001f,"Wave "+wave+" additive health and damage");
            Check(Enumerable.Range(0,plan.Count).Count(plan.IsMelee)==plan.MeleeCount&&plan.MeleeCount>0&&plan.MeleeCount<plan.Count,"Wave "+wave+" includes both roles");
            Check(Enumerable.Range(0,plan.Count).Count(plan.IsVeteran)==plan.Veterans&&plan.Veterans==(wave>=9?2:wave>=6?1:0),"Wave "+wave+" veteran allocation");
        }
        Check(new WaveThreat(p,999,100,10).Count==25&&new WaveThreat(p,999,100,10).Speed<=p.maximumRangedSpeed&&new WaveThreat(p,999,100,10).Accuracy<=p.maximumAccuracy,"Counts, speed and precision have caps");
        Check(Enumerable.Range(2,30).All(w=>new WaveThreat(p,w,100,10).Composition!=new WaveThreat(p,w-1,100,10).Composition),"Compositions never repeat consecutively");
        var coordinator=SquadManager.Instance;NormalEnemyStateMachine a=Spawn(false,5,false),b=Spawn(false,7,false),c=Spawn(true,9,false),d=Spawn(true,10,false),e=Spawn(true,11,false);
        coordinator.Configure(2,2);
        Check(coordinator.TryAttack(a)&&coordinator.TryAttack(b)&&!coordinator.TryAttack(c),"Initial budget blocks third attack");
        coordinator.ClearCombat();coordinator.Configure(4,2);
        Check(coordinator.TryAttack(c)&&coordinator.TryAttack(d)&&!coordinator.TryAttack(e)&&coordinator.TryAttack(a)&&coordinator.TryAttack(b),"Late budget allows four, at most two melee");
        coordinator.ClearCombat();
        Check(coordinator.Reserve(a,Vector3.zero,1.5f)&&!coordinator.Reserve(b,Vector3.right,1.5f)&&coordinator.Reserve(b,Vector3.right*2,1.5f),"Destination reservations reject occupied slots");
        coordinator.TryAttack(a);a.TakeDamage(10000);
        Check(coordinator.ActiveAttackers==0&&coordinator.ReservedDestinations==1,"Death releases attack and destination");
        Clear();yield return null;
        a=Spawn(false,5,false);a.Perception.Tick();
        Check(a.Perception.Visible,"Unobstructed player is visible");
        Vector3 remembered=a.Perception.LastKnown;
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=3;wall.transform.position=new Vector3(0,1.5f,-13.5f);wall.transform.localScale=new Vector3(12,4,.2f);Physics.SyncTransforms();
        player.TeleportTo(home+Vector3.right*2,Quaternion.identity);
        yield return new WaitForSeconds(.2f);a.Perception.Tick();
        Check(!a.Perception.Visible&&a.Perception.HasMemory&&a.Perception.LastKnown==remembered,"Wall blocks sight; memory does not track a hidden player");
        yield return new WaitForSeconds(3.1f);a.Perception.Tick();
        Check(!a.Perception.HasMemory,"Observed position expires after three seconds");
        CombatFeedback.ReportPlayerShot(player.transform.position);
        Check(a.Perception.HasMemory&&a.Perception.LastKnown==player.transform.position&&!a.Perception.Visible,"Nearby shot reveals location without granting sight");
        using(var pause=RunPause.Acquire(false))
        {
            remembered=a.Perception.LastKnown;CombatFeedback.ReportPlayerShot(home);int observations=a.Perception.Observations;
            yield return new WaitForSecondsRealtime(.2f);a.Perception.Tick();
            Check(remembered==a.Perception.LastKnown&&observations==a.Perception.Observations,"Pause freezes hearing and vision decisions");
        }
        Destroy(wall);Clear();yield return null;player.TeleportTo(home,Quaternion.identity);
        a=Spawn(false,5,true);Vector3 start=a.transform.position;
        yield return new WaitForSeconds(2);
        Check(Vector3.Distance(a.transform.position,home)>Vector3.Distance(start,home)+.5f,"Ranged robot retreats when player is closer than six metres");
        Check(a.Navigation.Decisions<15,"Navigation decisions are staggered rather than calculated every frame");
        Clear();yield return null;
        a=Spawn(false,8,true,true);Set(a.Ranged,"cooldown",0f);
        yield return Until(()=>a.Ranged.IsAttacking,3,"Veteran starts announced burst");
        Vector3 locked=a.Ranged.CommittedTarget;int shots=a.Ranged.ShotsFired;
        player.TeleportTo(home+Vector3.right*2,Quaternion.identity);
        using(var pause=RunPause.Acquire(false))
        {yield return new WaitForSecondsRealtime(.6f);Check(a.Ranged.ShotsFired==shots,"Pause freezes veteran burst windup");}
        yield return Until(()=>a.Ranged.ShotsFired>=2,3,"Veteran fires two rounds");
        Check(a.Ranged.CommittedTarget==locked,"Burst stays committed to pre-dodge target");
        yield return new WaitForSeconds(.15f);start=a.transform.position;yield return new WaitForSeconds(.8f);
        Check(Vector3.Distance(a.transform.position,start)>.2f,"Veteran repositions after burst");
        Clear();yield return null;player.TeleportTo(home,Quaternion.identity);
        foreach(bool blocked in new[]{false,true})
        {
            a=Spawn(true,2.7f,true,true);var melee=a.GetComponent<MeleeRobotCombat>();
            Check(melee.windupSeconds>=.8f&&melee.recoverySeconds>=1,"Veteran melee keeps readable windup and recovery");
            yield return Until(()=>melee.Phase==MeleeRobotPhase.Windup,4,"Veteran prepares charge");
            if(blocked){wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=3;wall.transform.position=new Vector3(0,1,-13.9f);wall.transform.localScale=new Vector3(3,2,.15f);Physics.SyncTransforms();}
            yield return Until(()=>melee.Phase==MeleeRobotPhase.Recover,3,"Veteran charge resolves");
            Check(melee.ChargeTravel<=1.21f&&(blocked?melee.ChargeTravel<.5f:melee.ChargeTravel>.9f),"Charge respects "+(blocked?"obstacle":"1.2m maximum")+": "+melee.ChargeTravel.ToString("F2"));
            Check(melee.ContactsResolved==1,"Charge has exactly one contact");
            if(blocked)Destroy(wall);Clear();yield return null;player.TeleportTo(home,Quaternion.identity);
        }
        a=Spawn(true,2.7f,true,true);var dying=a.GetComponent<MeleeRobotCombat>();
        yield return Until(()=>dying.Phase==MeleeRobotPhase.Windup,4,"Death fixture begins windup");
        int deaths=0,kills=0;Action<IDamageable> onDeath=x=>{if(ReferenceEquals(x,a))deaths++;};
        Action<PlayerKill> onKill=x=>kills++;EnemyEvents.OnDeath+=onDeath;CombatFeedback.PlayerEliminated+=onKill;
        a.TakeDamage(10000);a.TakeDamage(10000);
        CombatFeedback.ReportPlayerKill(a,session.weapons.Weapons.OfType<BaseGun>().First());CombatFeedback.ReportPlayerKill(a,session.weapons.Weapons.OfType<BaseGun>().First());
        yield return new WaitForSeconds(1);
        Check(deaths==1&&kills==1&&dying.ContactsResolved==0&&coordinator.ActiveAttackers==0,"Veteran death cancels attack and yields one attributed kill");
        EnemyEvents.OnDeath-=onDeath;CombatFeedback.PlayerEliminated-=onKill;
        Clear();yield return ActualWaves();Finish();
    }
    IEnumerator ActualWaves()
    {
        var manager=GameManager.Instance;var threat=session.Threat;var floor=session.CurrentFloor;
        float introduction=floor.introductionSeconds;floor.introductionSeconds=.1f;Set(manager,"announcementDuration",10f);
        foreach(int wave in new[]{1,3,6,9})
        {
            while((threat.Current?.Wave??0)<wave-1)threat.BeginWave();
            player.TeleportTo(home,Quaternion.identity);manager.ConfigureFloor(floor);Time.timeScale=6;
            var seen=new HashSet<int>();int melee=0,veterans=0,peak=0;bool correctStats=true,releasing=false,warning=false;
            float deadline=Time.realtimeSinceStartup+25;
            int expected=new WaveThreat(threat.Profile,wave,threat.BaseHealth,threat.BaseDamage).Count;
            while(seen.Count<expected&&Time.realtimeSinceStartup<deadline)
            {
                player.Heal(100);player.GetComponent<Shield>().Restore(100);
                warning|=Object.FindFirstObjectByType<EnemyEntrySignal>()!=null;
                var living=Object.FindObjectsByType<NormalEnemyStateMachine>(FindObjectsSortMode.None).Where(x=>!x.IsDead).ToArray();
                peak=Mathf.Max(peak,living.Length);
                foreach(var enemy in living)if(seen.Add(enemy.GetInstanceID()))
                {
                    if(enemy.IsMelee)melee++;if(enemy.IsVeteran)veterans++;
                    float hp=threat.BaseHealth*(1+.06f*(wave-1))*(enemy.IsMelee?.85f:1)*(enemy.IsVeteran?1.2f:1);
                    correctStats&=Mathf.Abs(enemy.maxHealth-hp)<.01f;
                }
                releasing|=seen.Count>=Mathf.Min(12,expected);
                if(releasing)foreach(var enemy in living)enemy.TakeDamage(100000);
                yield return null;
            }
            var plan=manager.WavePlan;
            Check(plan!=null&&plan.Wave==wave&&seen.Count==expected&&melee==plan.MeleeCount&&veterans==plan.Veterans,"Actual wave "+wave+" composition and veteran count");
            Check(correctStats&&peak<=12&&peak==Mathf.Min(12,expected)&&warning,"Actual wave "+wave+" scaling, twelve-alive cap and entry warning");
            manager.StopFloor();Time.timeScale=1;
            while(session.Rewards.Pending){if(session.Rewards.State==RewardState.Choosing)session.Rewards.Confirm(0);yield return null;}
            yield return null;
        }
        floor.introductionSeconds=introduction;
    }
    NormalEnemyStateMachine Spawn(bool melee,float distance,bool active,bool veteran=false)
    {
        var go=Instantiate(melee?meleePrefab:rangedPrefab,new Vector3(0,0,-16+distance),Quaternion.Euler(0,180,0));
        var enemy=go.GetComponent<NormalEnemyStateMachine>();fixtures.Add(enemy);
        enemy.ConfigureDifficulty(veteran?120:100,5,.7f);enemy.ConfigureVeteran(veteran);enemy.enabled=active;
        if(!active)enemy.agent.isStopped=true;return enemy;
    }
    void Clear(){foreach(var enemy in fixtures)if(enemy!=null)Destroy(enemy.gameObject);fixtures.Clear();SquadManager.Instance.ClearCombat();}
    void Aim(Vector3 point)
    {var rot=Quaternion.LookRotation(point-view.transform.position);player.transform.rotation=Quaternion.Euler(0,rot.eulerAngles.y,0);player.headCam.localRotation=Quaternion.Euler(rot.eulerAngles.x,0,0);Physics.SyncTransforms();}
    void Capture(string file,bool jpg=false)
    {
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(view,new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=target});
        var previous=RenderTexture.active;RenderTexture.active=target;
        var picture=new Texture2D(1920,1080,TextureFormat.RGB24,false);picture.ReadPixels(new Rect(0,0,1920,1080),0,0);picture.Apply();
        File.WriteAllBytes(Path.Combine(output,file),jpg?picture.EncodeToJPG(88):picture.EncodeToPNG());Destroy(picture);RenderTexture.active=previous;
    }
    void Finish()
    {
        if(finished)return;finished=true;Application.logMessageReceived-=Log;
        File.WriteAllText(Path.Combine(output,"results.txt"),string.Join("\n",checks)+"\nERRORS\n"+string.Join("\n",errors));Debug.Log("AI TEST COMPLETE: "+errors.Count+" errors");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(errors.Count==0?0:1);
#else
        Application.Quit(errors.Count==0?0:1);
#endif
    }
}
#endif
