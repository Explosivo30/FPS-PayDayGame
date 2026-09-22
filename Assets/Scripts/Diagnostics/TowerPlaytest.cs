#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

/// <summary>Opt-in end-to-end scene loading, persistence and navigation checks.</summary>
public class TowerPlaytest : MonoBehaviour
{
    readonly List<string> results=new List<string>(),errors=new List<string>();
    string output;
    TowerSession session;
    PlayerStateMachine player;
    Camera cameraView;
    RenderTexture target;
    bool preview;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if(!Environment.GetCommandLineArgs().Contains("-tower-qa")&&!Environment.GetCommandLineArgs().Contains("-tower-preview"))return;
        if(Object.FindFirstObjectByType<TowerPlaytest>()==null)
        { var go=new GameObject("Tower integration validation");DontDestroyOnLoad(go);go.AddComponent<TowerPlaytest>(); }
    }
    void Awake()
    {
        output=Path.GetFullPath(Path.Combine(Application.dataPath,"../Artifacts/Tower"));Directory.CreateDirectory(output);
        var args=Environment.GetCommandLineArgs();int destination=Array.IndexOf(args,"-tower-output");
        if(destination>=0&&destination+1<args.Length) { output=args[destination+1];Directory.CreateDirectory(output); }
        Application.runInBackground=true;
        preview=Environment.GetCommandLineArgs().Contains("-tower-preview");
        Application.logMessageReceived+=Log;
    }
    void Log(string text,string stack,LogType type)
    { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(text+"\n"+stack); }
    void Check(bool pass,string text) { results.Add((pass?"PASS ":"FAIL ")+text);Debug.Log(results.Last());if(!pass)errors.Add(text); }
    IEnumerator Start()
    {
        float deadline=Time.realtimeSinceStartup+70;
        while((TowerSession.Instance==null||TowerSession.Instance.CurrentFloor==null)&&Time.realtimeSinceStartup<deadline)yield return null;
        if(TowerSession.Instance==null||TowerSession.Instance.CurrentFloor==null) { Check(false,"Floor boot within 70 seconds");Finish();yield break; }
        Bind();
        Check(SceneManager.GetSceneByName("TowerCore").isLoaded,"Core loads additively when opening a floor directly");
        Check(Object.FindObjectsByType<PlayerStateMachine>(FindObjectsSortMode.None).Length==1,"Exactly one persistent player");
        Check(!session.RequestTravel(session.CurrentFloor.elevator),"Elevator cannot skip mandatory waves");
        Navigation();
        if(session.CurrentFloor.floorNumber==2)
        {
            View(new Vector3(-12,5.6f,-18),new Vector3(0,5,3));yield return new WaitForSeconds(.4f);Capture("forest-overview.png");
            View(new Vector3(0,1.05f,-17),new Vector3(0,3,3));yield return new WaitForSeconds(.4f);Capture("forest-gameplay.png");
            if(!preview)yield return Ramps();
        }
        else
        {
            View(new Vector3(8,1.05f,-15),new Vector3(0,2,4));yield return new WaitForSeconds(.4f);Capture("laboratory-gameplay.png");
        }
        for(int i=0;i<session.weapons.Weapons.Count;i++)
        {
            session.weapons.EquipImmediate(i);yield return new WaitForSeconds(.3f);
            Capture("weapon-"+i+".png");
        }
        if(preview) { Finish();yield break; }
        var gun=player.GetComponentsInChildren<BaseGun>(true).First(g=>g is Pistol);
        session.weapons.EquipImmediate(0);yield return new WaitForSeconds(.4f);
        gun.currentAmmo=0;gun.reserveAmmo=2;gun.Action.BeginReload();gun.Action.Tick(2);
        Check(gun.currentAmmo==2&&gun.reserveAmmo==0,"Reload transfers only available reserve");
        gun.currentAmmo=0;gun.Action.BeginReload();
        Check(gun.Action.State!=WeaponActionState.Reloading,"Empty reserve blocks infinite reloads");
        gun.reserveAmmo=30;gun.currentAmmo=3;gun.Action.BeginReload();gun.Action.Cancel();
        Check(gun.reserveAmmo==30&&gun.currentAmmo==3,"Cancelled magazine reload preserves reserve and magazine");
        GameManager.Instance.SetPlayerPoints(10000);
        var upgrade=UpgradeManager.Instance.catalog.First();
        UpgradeManager.Instance.BuyUpgrade(upgrade);
        int upgradeLevel=UpgradeManager.Instance.GetLevel(upgrade);
        Check(upgradeLevel==1,"Upgrade can be purchased before travelling");
        ShopManager.Instance.OpenShop();ShopManager.Instance.CloseShop();
        yield return ClearWaves();
        Check(session.ExitUnlocked,"Three real spawned waves unlock elevator");
        if(!session.ExitUnlocked) { Finish();yield break; }
        Check(session.Progression.EarnedThisRun==1,"Mandatory objective banks one core through real wave completion");
        int banked=session.Progression.Data.cores;
        yield return new WaitForSeconds(.3f);
        Check(GameManager.Instance.WaitingForExtraWave&&GameManager.Instance.CurrentWave==3,"Optional farming waits for the player decision");
        Check(GameManager.Instance.RequestExtraWave()&&!GameManager.Instance.RequestExtraWave(),"One explicit request starts one extra wave");
        Time.timeScale=8;deadline=Time.realtimeSinceStartup+12;
        while(GameManager.Instance.CurrentWave<4&&Time.realtimeSinceStartup<deadline)yield return null;
        Time.timeScale=1;
        foreach(var enemy in Object.FindObjectsByType<NormalEnemyStateMachine>(FindObjectsSortMode.None))enemy.enabled=false;
        Check(GameManager.Instance.CurrentWave==4&&session.ExitUnlocked,"Elevator remains unlocked during active optional wave");
        Check(session.Progression.Data.cores==banked,"Starting optional farming cannot duplicate banked cores");
        View(new Vector3(0,1.05f,-15),new Vector3(0,1,0));
        Check(!session.RequestTravel(session.CurrentFloor.elevator),"Travel button requires being inside cabin");
        string previousScene=session.CurrentFloor.gameObject.scene.name;
        string nextScene=session.CurrentFloor.nextScene;
        var persistentPlayer=player;var persistentGun=gun;
        int weaponIndex=2;session.weapons.EquipImmediate(weaponIndex);
        gun.currentAmmo=3;gun.reserveAmmo=19;gun.damage+=7;
        float damage=gun.damage;
        GameManager.Instance.SetPlayerPoints(777);
        float health=player.Health;
        player.TeleportTo(session.CurrentFloor.arrival.position,session.CurrentFloor.arrival.rotation);
        yield return new WaitForSeconds(.2f);
        Check(session.RequestTravel(session.CurrentFloor.elevator),"Inside panel starts door closure and asynchronous travel");
        Check(!session.RequestTravel(session.CurrentFloor.elevator),"Repeated interaction cannot start duplicate travel");
        yield return WaitForTravel();
        Check(session.CurrentFloor.gameObject.scene.name==nextScene,"Next floor loaded");
        Check(!SceneManager.GetSceneByName(previousScene).isLoaded,"Old floor unloaded");
        Check(player==persistentPlayer&&gun==persistentGun,"Player and weapon instances preserved");
        Check(gun.currentAmmo==3&&gun.reserveAmmo==19&&gun.damage==damage,"Magazine, reserve and weapon values preserved");
        Check(session.weapons.CurrentIndex==weaponIndex,"Equipped weapon preserved");
        Check(Mathf.Approximately(player.Health,health)&&GameManager.Instance.GetPlayerPoints()==777,"Health and chips preserved");
        Check(UpgradeManager.Instance.GetLevel(upgrade)==upgradeLevel,"Purchased upgrade level preserved");
        Check(GameManager.Instance.CurrentWave==0&&GameManager.Instance.CompletedWaves==0,"New floor begins a fresh wave sequence");
        Check(Object.FindObjectsByType<NormalEnemyStateMachine>(FindObjectsSortMode.None).Length==0,"No enemies leak from previous floor");
        Check(Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Any(c=>c.name=="Player status"),"Player HUD survives floor unloading");
        ShopManager.Instance.OpenShop();
        Check(session.Menu.Visible,"Shop opens after travelling with persistent UI");
        ShopManager.Instance.CloseShop();
        Navigation();
        View(new Vector3(-10,4.6f,-16),new Vector3(0,3,3));yield return new WaitForSeconds(.5f);Capture(nextScene=="CrystalGarden"?"garden-overview.png":"forest-arrival.png");
        yield return ClearWaves();
        player.TeleportTo(session.CurrentFloor.arrival.position,session.CurrentFloor.arrival.rotation);
        Check(session.RequestTravel(session.CurrentFloor.elevator),"Second floor can be completed and exited");
        yield return WaitForTravel();
        Check(session.FloorsCleared==2,"Two consecutive scene transitions");
        Navigation();
        if(session.CurrentFloor.floorNumber==1)
        {
            View(new Vector3(8,1.05f,-15),new Vector3(0,2,3));yield return new WaitForSeconds(.3f);Capture("laboratory-linked.png");
        }
        int oldId=player.GetInstanceID();
        player.GetComponent<Shield>()?.Absorb(100000);
        yield return null;
        Check(session.RunEnded&&session.Menu.ShowingDeath,"Death presents a run summary before restarting");
        session.RestartRun();
        yield return new WaitForSecondsRealtime(4);
        deadline=Time.realtimeSinceStartup+45;
        while((TowerSession.Instance==null||TowerSession.Instance.CurrentFloor==null)&&Time.realtimeSinceStartup<deadline)yield return null;
        session=TowerSession.Instance;
        Check(session!=null&&session.CurrentFloor!=null&&session.CurrentFloor.floorNumber==1,"Death restarts at the laboratory");
        if(session!=null&&session.CurrentFloor!=null)
        {
            Check(session.player.GetInstanceID()!=oldId,"Restart creates a fresh player");
            Check(GameManager.Instance.GetPlayerPoints()==100,"Restart resets run currency");
            Check(UpgradeManager.Instance.GetLevel(UpgradeManager.Instance.catalog.First())==0,"Restart resets purchased upgrades");
            Check(Object.FindObjectsByType<PlayerStateMachine>(FindObjectsSortMode.None).Length==1&&Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None).Length==1,"No duplicate player or game manager after restart");
        }
        Finish();
    }
    void Bind()
    {
        session=TowerSession.Instance;player=session.player;
        player.enabled=false;session.weapons.AutomatedInput=true;
        cameraView=player.GetComponentsInChildren<Camera>().First(c=>c.gameObject.layer!=8);
        target=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);target.Create();cameraView.targetTexture=target;
        foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if(canvas.name=="Laboratory HUD"||canvas.name=="Tower HUD")
            { canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cameraView;canvas.planeDistance=.15f; }
    }
    void Navigation()
    {
        foreach(var entry in session.CurrentFloor.spawnPoints)
        {
            bool a=NavMesh.SamplePosition(entry.position,out var start,2,NavMesh.AllAreas);
            bool b=NavMesh.SamplePosition(session.CurrentFloor.arrival.position,out var end,2,NavMesh.AllAreas);
            var path=new NavMeshPath();
            Check(a&&b&&NavMesh.CalculatePath(start.position,end.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,
                session.CurrentFloor.displayName+" connected spawn to elevator: "+entry.name);
        }
    }
    IEnumerator Ramps()
    {
        var cc=player.GetComponent<CharacterController>();
        foreach(int side in new[]{-1,1})
        {
            player.TeleportTo(new Vector3(side*18,1.05f,.1f),Quaternion.identity);
            for(float t=0;t<4.5f;t+=Time.deltaTime) { cc.Move(new Vector3(0,-3,3)*Time.deltaTime);yield return null; }
            Check(player.transform.position.y>3.7f,"CharacterController climbs forest ramp "+side);
        }
    }
    IEnumerator ClearWaves()
    {
        player.TeleportTo(new Vector3(0,1.05f,-16),Quaternion.identity);
        Time.timeScale=8;
        float deadline=Time.realtimeSinceStartup+45;
        int notifications=0;
        while((GameManager.Instance.CompletedWaves<3||(session.Rewards?.Pending??false))&&Time.realtimeSinceStartup<deadline)
        {
            if(session.Rewards!=null&&session.Rewards.State==RewardState.Choosing)session.Rewards.Confirm(0);
            player.Heal(100);
            foreach(var enemy in Object.FindObjectsByType<NormalEnemyStateMachine>(FindObjectsSortMode.None))
            {
                if(enemy.IsDead)continue;
                enemy.TakeDamage(10000);enemy.TakeDamage(10000);notifications++;
            }
            yield return null;
        }
        Time.timeScale=1;
        Check(GameManager.Instance.CompletedWaves==3,"Wave counter completes exactly three mandatory waves");
        Check(notifications==21,"5 + 7 + 9 enemies, with duplicate death calls ignored");
        Check(GameManager.Instance.ActiveEnemies==0,"Wave tracking cleared after deaths");
    }
    IEnumerator WaitForTravel()
    {
        float deadline=Time.realtimeSinceStartup+60;
        while(session.IsTransitioning&&Time.realtimeSinceStartup<deadline)yield return null;
        Check(!session.IsTransitioning,"Scene transition completes within 60 seconds");
        player.enabled=false;
    }
    void View(Vector3 position,Vector3 targetPoint)
    {
        player.TeleportTo(position,Quaternion.identity);
        var look=Quaternion.LookRotation(targetPoint-cameraView.transform.position);
        player.transform.rotation=Quaternion.Euler(0,look.eulerAngles.y,0);player.headCam.localRotation=Quaternion.Euler(look.eulerAngles.x,0,0);
    }
    void Capture(string filename)
    {
        var previous=RenderTexture.active;RenderTexture.active=target;
        var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
        File.WriteAllBytes(Path.Combine(output,filename),image.EncodeToPNG());Destroy(image);RenderTexture.active=previous;
    }
    void Finish()
    {
        File.WriteAllText(Path.Combine(output,preview?"preview-results.txt":"tower-results.txt"),string.Join("\n",results)+"\nERRORS\n"+string.Join("\n",errors));
        Debug.Log("TOWER PLAYTEST COMPLETE: "+errors.Count+" errors");
        Application.logMessageReceived-=Log;Time.timeScale=1;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(errors.Count==0?0:1);
#else
        Application.Quit(errors.Count==0?0:1);
#endif
    }
}
#endif
