#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

/// <summary>Exercises selected cards through real guns, collisions, reload clocks and run offers.</summary>
public sealed partial class MutationPlaytest : MonoBehaviour
{
    readonly List<string> checks=new List<string>(),errors=new List<string>();
    readonly List<GameObject> fixtures=new List<GameObject>();
    TowerSession session;MutationRun run;PlayerStateMachine player;GunController weapons;
    Pistol pistol;MachineGun carbine;ShotGun shotgun;Knife knife;
    Camera view;RenderTexture target;GameObject rangedPrefab,meleePrefab;
    string output;float started;bool finished;
    readonly Vector3 home=new Vector3(0,1.05f,-16);
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if(!Environment.GetCommandLineArgs().Contains("-mutation-qa"))return;
        if(Object.FindFirstObjectByType<MutationPlaytest>()!=null)return;
        var go=new GameObject("Selected mutations validation");DontDestroyOnLoad(go);go.AddComponent<MutationPlaytest>();
    }
    void Awake()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-mutation-output");
        output=i>=0&&i+1<args.Length?Path.GetFullPath(args[i+1]):Path.GetFullPath("Artifacts/MutationCards/Editor");
        Directory.CreateDirectory(output);Application.logMessageReceived+=Log;Application.runInBackground=true;started=Time.realtimeSinceStartup;
    }
    void Log(string text,string stack,LogType kind){if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)errors.Add(text+"\n"+stack);}
    void Check(bool pass,string label){string message=(pass?"PASS ":"FAIL ")+label;checks.Add(message);Debug.Log(message);if(!pass)errors.Add(label);}
    void Update(){if(!finished&&Time.realtimeSinceStartup-started>360){Check(false,"Mutation watchdog");Finish();}}
    IEnumerator Start(){yield return Guard(Execute());}
    IEnumerator Guard(IEnumerator body)
    {
        while(!finished)
        {
            bool next=false;object yielded=null;Exception fault=null;
            try{next=body.MoveNext();if(next)yielded=body.Current;}catch(Exception error){fault=error;}
            if(fault!=null){errors.Add(fault.ToString());Check(false,"Unhandled mutation test exception");Finish();yield break;}
            if(!next)yield break;
            if(yielded is IEnumerator child)yield return Guard(child);else yield return yielded;
        }
    }
    IEnumerator Execute()
    {
        float deadline=Time.realtimeSinceStartup+80;
        while((TowerSession.Instance==null||TowerSession.Instance.CurrentFloor==null)&&Time.realtimeSinceStartup<deadline)yield return null;
        if(TowerSession.Instance!=null&&TowerSession.Instance.CurrentFloor!=null&&TowerSession.Instance.CurrentFloor.gameObject.scene.name!="RoboticForest")
        {
            // The shipped game starts in the laboratory. Use the same authored
            // fixture floor in Editor and standalone before binding any objects.
            yield return SceneManager.LoadSceneAsync("RoboticForest");deadline=Time.realtimeSinceStartup+80;
            while((TowerSession.Instance==null||TowerSession.Instance.CurrentFloor==null)&&Time.realtimeSinceStartup<deadline)yield return null;
        }
        session=TowerSession.Instance;
        Check(session!=null&&session.CurrentFloor!=null,"Playable tower boots");if(session==null||session.CurrentFloor==null){Finish();yield break;}
        player=session.player;weapons=session.weapons;run=MutationRun.For(session);view=run.View;
        player.enabled=false;weapons.AutomatedInput=true;GameManager.Instance.StopFloor();
        foreach(var pulse in Object.FindObjectsByType<RootPulse>(FindObjectsSortMode.None))pulse.enabled=false;
        rangedPrefab=(GameObject)typeof(GameManager).GetField("enemyPrefab",Private).GetValue(GameManager.Instance);
        meleePrefab=(GameObject)typeof(GameManager).GetField("meleeEnemyPrefab",Private).GetValue(GameManager.Instance);
        pistol=weapons.Weapons.OfType<Pistol>().First();carbine=weapons.Weapons.OfType<MachineGun>().First();shotgun=weapons.Weapons.OfType<ShotGun>().First();knife=weapons.Weapons.OfType<Knife>().First();
        target=new RenderTexture(1920,1080,24);target.Create();view.targetTexture=target;
        foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=view;canvas.planeDistance=.15f;}
        yield return Reset();
        var catalogue=session.Rewards.catalog.Where(c=>c.kind==RewardKind.Mutation).ToArray();
        Check(catalogue.Length==19&&catalogue.Select(c=>c.mutation).Distinct().Count()==19,"Exactly the 19 selected mutations, no unselected cards");
        Check(catalogue.All(c=>c.icon!=null&&c.maxLevel==1&&!string.IsNullOrEmpty(c.activationHint)),"Every mutation has an icon, action hint and one rank");
        var saved=weapons.WeaponAvailable;weapons.WeaponAvailable=w=>w is Pistol;
        Check(!session.Rewards.Eligible(Card(MutationKind.Fork))&&!session.Rewards.Eligible(Card(MutationKind.ReturningBlade))&&!session.Rewards.Eligible(Card(MutationKind.Retrothruster)),"Offers exclude absent carbine, knife and shotgun");
        Check(session.Rewards.Eligible(Card(MutationKind.RamChamber))&&session.Rewards.Eligible(Card(MutationKind.MagazineBomb)),"Pistol and compatible global cards remain available");
        weapons.WeaponAvailable=saved;
        yield return GunCases();yield return MotionCases();yield return DeviceCases();yield return LifecycleCases();Finish();
    }
    RewardCard Card(MutationKind kind)=>session.Rewards.catalog.First(c=>c.kind==RewardKind.Mutation&&c.mutation==kind);
    void Give(params MutationKind[] kinds){foreach(var kind in kinds)session.Rewards.Effect(Card(kind)).Apply(Card(kind),session.Rewards.Context,1);}
    IEnumerator Reset()
    {
        weapons.AutomatedInput=true;player.controls.isAttacking=false;run.ResetRun();
        EnemyProjectile.ClearAll();
        foreach(var go in fixtures)if(go!=null)Destroy(go);fixtures.Clear();
        SquadManager.Instance?.ClearCombat();
        player.Heal(1000);player.GetComponent<Shield>().SetMaxShield(50);player.TeleportTo(home,Quaternion.identity);
        weapons.WeaponAvailable=null;
        foreach(var gun in weapons.Weapons.OfType<BaseGun>()){gun.currentAmmo=gun.ammo;gun.reserveAmmo=gun.maxReserveAmmo;gun.Action.Cancel();}
        foreach(var gun in weapons.Weapons.OfType<BaseGun>())if(gun.Recoil!=null){gun.Recoil.spreadHip=0;gun.Recoil.spreadADS=0;}
        pistol.damage=30;carbine.damage=12;shotgun.damage=8;knife.damage=30;
        Equip(pistol);weapons.ProcessCombatInput(false,false);yield return null;
    }
    NormalEnemyStateMachine Spawn(float distance,float x=0,bool melee=false,bool veteran=false,float health=1000)
    {
        var want=new Vector3(x,0,-16+distance);
        if(NavMesh.SamplePosition(want,out var ground,1,NavMesh.AllAreas))want=ground.position;
        var go=Instantiate(melee?meleePrefab:rangedPrefab,want,Quaternion.Euler(0,180,0));fixtures.Add(go);
        var enemy=go.GetComponent<NormalEnemyStateMachine>();enemy.ConfigureDifficulty(health,5,.8f);enemy.ConfigureVeteran(veteran);enemy.enabled=false;
        if(enemy.agent.isOnNavMesh)enemy.agent.isStopped=true;Physics.SyncTransforms();return enemy;
    }
    void Aim(Vector3 point)
    {
        var rotation=Quaternion.LookRotation(point-view.transform.position);
        player.transform.rotation=Quaternion.Euler(0,rotation.eulerAngles.y,0);
        player.headCam.localRotation=Quaternion.Euler(rotation.eulerAngles.x,0,0);Physics.SyncTransforms();
    }
    void Equip(Component weapon)
    {
        int i=weapons.Weapons.ToList().FindIndex(w=>w==weapon);weapons.EquipImmediate(i);
        weapon.GetComponent<WeaponAction>().Cancel();
    }
    void Fire(BaseGun gun,Vector3 point)
    {Equip(gun);Aim(point);gun.GetType().GetField("lastShotTime",Private).SetValue(gun,float.NegativeInfinity);gun.Use();Physics.SyncTransforms();}
    void Reload(BaseGun gun,bool complete=false)
    {Equip(gun);gun.Reload();if(complete)for(int i=0;i<40&&gun.Action.State==WeaponActionState.Reloading;i++)gun.Action.Tick(.5f);}
    void Advance(float seconds)
    {for(float t=0;t<seconds;t+=.02f){run.Tick(Mathf.Min(.02f,seconds-t));Physics.SyncTransforms();}}
    MutationWorldEffect Find(MutationWorldKind kind)=>run.Effects.FirstOrDefault(f=>f!=null&&f.Live&&f.Kind==kind);
    GameObject Wall(Vector3 point,Vector3 scale)
    {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Mutation test cover";go.layer=3;go.transform.position=point;go.transform.localScale=scale;fixtures.Add(go);Physics.SyncTransforms();return go;}
    void Capture(string name)
    {
        // Reward canvases are created lazily after the first completed wave.
        foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=view;canvas.planeDistance=.15f;}
        Canvas.ForceUpdateCanvases();
        var weaponCameras=player.GetComponentsInChildren<Camera>().Where(c=>c.gameObject.layer==8&&c.enabled).ToArray();
        if(session.Rewards.Pending)foreach(var camera in weaponCameras)camera.enabled=false;
        RenderPipeline.SubmitRenderRequest(view,new RenderPipeline.StandardRequest{destination=target});
        var old=RenderTexture.active;RenderTexture.active=target;
        var picture=new Texture2D(1920,1080,TextureFormat.RGB24,false);picture.ReadPixels(new Rect(0,0,1920,1080),0,0);picture.Apply();
        File.WriteAllBytes(Path.Combine(output,name),picture.EncodeToPNG());Destroy(picture);RenderTexture.active=old;
        foreach(var camera in weaponCameras)camera.enabled=true;
    }
    void Finish()
    {
        if(finished)return;finished=true;Application.logMessageReceived-=Log;
        File.WriteAllText(Path.Combine(output,"results.txt"),string.Join("\n",checks)+"\nERRORS\n"+string.Join("\n",errors));
        Debug.Log("MUTATION TEST COMPLETE: "+checks.Count+" checks, "+errors.Count+" errors");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(errors.Count==0?0:1);
#else
        Application.Quit(errors.Count==0?0:1);
#endif
    }
}
#endif
