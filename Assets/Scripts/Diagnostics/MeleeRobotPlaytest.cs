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

public class MeleeRobotPlaytest : MonoBehaviour
{
    readonly List<string> checks=new List<string>(),errors=new List<string>();
    PlayerStateMachine player;
    GameObject prefab;
    NormalEnemyStateMachine enemy;
    MeleeRobotCombat combat;
    Camera view;
    RenderTexture target;
    string output;
    int damageEvents;
    float damageReceived;
    readonly Vector3 origin=new Vector3(0,1.05f,-16);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if(!Environment.GetCommandLineArgs().Contains("-melee-qa")&&!Environment.GetCommandLineArgs().Contains("-melee-record"))return;
        if(Object.FindFirstObjectByType<MeleeRobotPlaytest>()==null)
        { var go=new GameObject("Melee robot verification");DontDestroyOnLoad(go);go.AddComponent<MeleeRobotPlaytest>(); }
    }
    void Awake()
    {
        output=Path.GetFullPath("Artifacts/MeleeRobot");
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-melee-output");
        if(i>=0&&i+1<args.Length)output=args[i+1];
        Directory.CreateDirectory(output);Application.runInBackground=true;Application.logMessageReceived+=Log;
    }
    void Log(string message,string stack,LogType kind)
    { if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)errors.Add(message+"\n"+stack); }
    void Check(bool condition,string label)
    { string line=(condition?"PASS ":"FAIL ")+label;checks.Add(line);Debug.Log(line);if(!condition)errors.Add(label); }
    static object Get(object instance,string name)=>instance.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(instance);
    static void Set(object instance,string name,object value)=>instance.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(instance,value);
    IEnumerator Start()
    {
        float deadline=Time.realtimeSinceStartup+80;
        while((TowerSession.Instance==null||TowerSession.Instance.CurrentFloor==null)&&Time.realtimeSinceStartup<deadline)yield return null;
        if(TowerSession.Instance==null||TowerSession.Instance.CurrentFloor==null) { Check(false,"Tower boots");Finish();yield break; }
        if(TowerSession.Instance.CurrentFloor.gameObject.scene.name!="RoboticForest")
        {
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("RoboticForest");
            deadline=Time.realtimeSinceStartup+80;
            while((TowerSession.Instance==null||TowerSession.Instance.CurrentFloor==null)&&Time.realtimeSinceStartup<deadline)yield return null;
            if(TowerSession.Instance==null||TowerSession.Instance.CurrentFloor==null) { Check(false,"Forest boot");Finish();yield break; }
        }
        player=TowerSession.Instance.player;player.enabled=false;TowerSession.Instance.weapons.AutomatedInput=true;
        prefab=(GameObject)Get(GameManager.Instance,"meleeEnemyPrefab");GameManager.Instance.StopFloor();
        Check(prefab!=null,"Melee prefab connected to the real wave manager");
        if(prefab==null) { Finish();yield break; }
        player.Damaged+=d=>{damageEvents++;damageReceived+=d.HealthLoss+d.ShieldLoss;};
        view=player.GetComponentsInChildren<Camera>().First(c=>c.gameObject.layer!=8);
        target=new RenderTexture(1920,1080,24);target.Create();view.targetTexture=target;
        foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        { canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=view;canvas.planeDistance=.15f; }
        if(Environment.GetCommandLineArgs().Contains("-melee-record"))
        { yield return Record();Finish();yield break; }
        TowerSession.Instance.ShowMessage("ROBOT DE SEGURIDAD · CUERPO A CUERPO",60);
        Spawn(4);enemy.enabled=false;
        Check(Mathf.Approximately(enemy.Health,85)&&Mathf.Approximately(combat.punchDamage,18),"Distinct baseline: 85 health and 18 punch damage");
        Aim(enemy.transform.position+Vector3.up*1.1f);yield return new WaitForSeconds(.25f);Capture("robot.png");
        enemy.enabled=true;Vector3 before=enemy.transform.position;yield return new WaitForSeconds(.6f);
        Check(Vector3.Distance(before,enemy.transform.position)>.5f,"Pursues immediately using navigation");
        Clear();yield return null;
        Spawn(1.5f);yield return WaitPhase(MeleeRobotPhase.Windup);
        yield return new WaitForSeconds(.25f);Capture("anticipation.png");
        Check(damageEvents==0,"Windup visibly precedes damage");
        var root=enemy.transform.position;var rotation=enemy.transform.rotation;
        float progress=combat.PhaseProgress;
        ShopManager.Instance.OpenShop();yield return new WaitForSecondsRealtime(.25f);
        Check(combat.PhaseProgress==progress&&Vector3.Distance(root,enemy.transform.position)<.001f&&damageEvents==0,"Shop freezes pursuit and attack timing");
        ShopManager.Instance.CloseShop();
        yield return WaitPhase(MeleeRobotPhase.Recover);
        Capture("punch.png");
        Check(damageEvents==1&&Mathf.Abs(damageReceived-18)<.01f&&combat.ContactsResolved==1,"One punch contact produces exactly one damage event");
        Check(enemy.transform.position==root&&Quaternion.Angle(rotation,enemy.transform.rotation)<.1f,"Punch animation keeps collision and navigation root fixed");
        yield return new WaitForSeconds(.35f);
        Check(damageEvents==1&&combat.Phase==MeleeRobotPhase.Recover,"Recovery leaves a punish window without repeated hits");
        Clear();yield return null;
        foreach(string dodge in new[]{"side","back","behind","wall","above"})
        {
            Spawn(1.5f);yield return WaitPhase(MeleeRobotPhase.Windup);Quaternion committed=enemy.transform.rotation;
            GameObject wall=null;
            if(dodge=="side")player.TeleportTo(origin+Vector3.right*2,Quaternion.identity);
            if(dodge=="back")player.TeleportTo(origin-Vector3.forward*2,Quaternion.identity);
            if(dodge=="behind")player.TeleportTo(origin+Vector3.forward*2.7f,Quaternion.identity);
            if(dodge=="above")player.TeleportTo(origin+Vector3.up*3,Quaternion.identity);
            if(dodge=="wall")
            {
                wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=3;wall.name="Temporary QA occluder";
                wall.transform.position=new Vector3(0,1.2f,-15.25f);wall.transform.localScale=new Vector3(3,2.4f,.12f);Physics.SyncTransforms();
            }
            yield return WaitPhase(MeleeRobotPhase.Recover);
            Check(damageEvents==0&&combat.ContactsResolved==1,"Punch cannot hit after "+dodge+" avoidance");
            Check(Quaternion.Angle(committed,enemy.transform.rotation)<.1f,"Committed direction preserved after "+dodge+" avoidance");
            if(wall!=null)Destroy(wall);Clear();yield return null;
        }
        Spawn(1.5f);yield return WaitPhase(MeleeRobotPhase.Windup);
        int deaths=0;Action<IDamageable> death=d=>{if(ReferenceEquals(d,enemy))deaths++;};EnemyEvents.OnDeath+=death;
        enemy.TakeDamage(1000);enemy.TakeDamage(1000);
        Check(deaths==1&&enemy.IsDead&&combat.Phase==MeleeRobotPhase.Dead,"Death cancels attack and emits one elimination");
        Check(!enemy.agent.enabled&&enemy.GetComponentsInChildren<Collider>().All(c=>!c.enabled),"Death disables navigation and all hit colliders");
        yield return new WaitForSeconds(.8f);Capture("collapse.png");
        Check(damageEvents==0,"Killed windup never resolves damage");
        yield return new WaitForSeconds(2.3f);Check(enemy==null,"Body is removed after three seconds");EnemyEvents.OnDeath-=death;
        foreach(int fps in new[]{30,60,144})
        {
            int sync=QualitySettings.vSyncCount;QualitySettings.vSyncCount=0;Application.targetFrameRate=fps;
            Spawn(1.5f);yield return WaitPhase(MeleeRobotPhase.Windup);float start=Time.time;
            while(damageEvents==0&&Time.time-start<2)yield return null;
            float elapsed=Time.time-start;
            Check(damageEvents==1&&Mathf.Abs(elapsed-(combat.windupSeconds+combat.strikeSeconds*.45f))<.12f,"Single contact timing at "+fps+" FPS: "+elapsed.ToString("F3")+"s");
            Clear();yield return null;QualitySettings.vSyncCount=sync;
        }
        Application.targetFrameRate=60;
        foreach(int side in new[]{-1,1})
        {
            Spawn(5);enemy.agent.Warp(new Vector3(side*18,0,.1f));player.TeleportTo(new Vector3(side*18,4.05f,13),Quaternion.identity);
            deadline=Time.time+12;
            while(enemy.transform.position.y<2.8f&&Time.time<deadline)yield return null;
            Check(enemy.transform.position.y>2.8f,"Melee pursuer climbs forest ramp "+side);
            Clear();yield return null;
        }
        player.TeleportTo(origin,Quaternion.identity);
        var manager=GameManager.Instance;var floor=TowerSession.Instance.CurrentFloor;floor.introductionSeconds=.1f;
        Set(manager,"spawnInterval",.1f);Set(manager,"announcementDuration",.15f);manager.ConfigureFloor(floor);
        for(int wave=1;wave<=3;wave++)
        {
            deadline=Time.time+15;
            while((manager.CurrentWave<wave||manager.IsSpawning)&&Time.time<deadline)yield return null;
            var living=Object.FindObjectsByType<NormalEnemyStateMachine>(FindObjectsSortMode.None).Where(e=>!e.IsDead).ToArray();
            int count=5+(wave-1)*2,melee=Mathf.RoundToInt(count*.4f);
            Check(living.Length==count&&living.Count(e=>e.IsMelee)==melee,"Mixed wave "+wave+": "+melee+" melee / "+count+" total");
            int score=manager.GetPlayerPoints();
            foreach(var e in living) { e.TakeDamage(100000);e.TakeDamage(100000); }
            yield return null;
            Check(manager.EnemiesRemaining==0&&manager.ActiveEnemies==0,"Mixed wave "+wave+" counts each death once");
            Check(manager.GetPlayerPoints()==score+count*10+30,"Mixed wave "+wave+" rewards once");
            while(TowerSession.Instance.Rewards?.Pending??false)
            { if(TowerSession.Instance.Rewards.State==RewardState.Choosing)TowerSession.Instance.Rewards.Confirm(0);yield return null; }
        }
        yield return null;
        Check(manager.CompletedWaves==3&&manager.WaitingForExtraWave,"Three mixed waves unlock voluntary extra waves");
        manager.StopFloor();Finish();
    }
    void Spawn(float distance)
    {
        player.TeleportTo(origin,Quaternion.identity);player.Heal(100);player.GetComponent<Shield>().SetMaxShield(50);
        damageEvents=0;damageReceived=0;
        enemy=Instantiate(prefab,new Vector3(0,0,-16+distance),Quaternion.Euler(0,180,0)).GetComponent<NormalEnemyStateMachine>();
        enemy.ConfigureDifficulty(100,12,.55f);combat=enemy.GetComponent<MeleeRobotCombat>();
        Aim(enemy.transform.position+Vector3.up*1.2f);
    }
    void Clear() { if(enemy!=null)Destroy(enemy.gameObject); }
    IEnumerator WaitPhase(MeleeRobotPhase phase)
    {
        float end=Time.time+4;
        while(combat.Phase!=phase&&Time.time<end)yield return null;
        Check(combat.Phase==phase,"Reached "+phase);
    }
    void Aim(Vector3 point)
    {
        var rotation=Quaternion.LookRotation(point-view.transform.position);
        player.transform.rotation=Quaternion.Euler(0,rotation.eulerAngles.y,0);
        player.headCam.localRotation=Quaternion.Euler(rotation.eulerAngles.x,0,0);Physics.SyncTransforms();
    }
    void Capture(string name)
    {
        var previous=RenderTexture.active;RenderTexture.active=target;
        var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
        File.WriteAllBytes(Path.Combine(output,name),image.EncodeToPNG());Destroy(image);RenderTexture.active=previous;
    }
    IEnumerator Record()
    {
        yield return GetComponent<MeleeRobotRecording>()==null?
            gameObject.AddComponent<MeleeRobotRecording>().Run(player,prefab,view,target,output):null;
    }
    void Finish()
    {
        File.WriteAllText(Path.Combine(output,Environment.GetCommandLineArgs().Contains("-melee-record")?"record-results.txt":"results.txt"),string.Join("\n",checks)+"\nERRORS\n"+string.Join("\n",errors));
        Debug.Log("MELEE TEST COMPLETE: "+errors.Count+" errors");Application.logMessageReceived-=Log;Time.timeScale=1;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(errors.Count==0?0:1);
#else
        Application.Quit(errors.Count==0?0:1);
#endif
    }
}
#endif
