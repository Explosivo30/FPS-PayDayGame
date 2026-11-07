#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public class PlayerPlaytest : MonoBehaviour
{
    readonly List<string> checks=new List<string>(),errors=new List<string>();
    TowerSession session;
    PlayerStateMachine player;
    Camera view;
    RenderTexture target;
    string output;
    GameObject platform;
    int jumps,landings;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if(!Environment.GetCommandLineArgs().Contains("-player-qa"))return;
        if(Object.FindFirstObjectByType<PlayerPlaytest>()!=null)return;
        var go=new GameObject("Player validation");DontDestroyOnLoad(go);go.AddComponent<PlayerPlaytest>();
    }
    void Awake()
    {
        output=Path.GetFullPath(Path.Combine(Application.dataPath,"../Artifacts/Player"));
        var args=Environment.GetCommandLineArgs();int destination=Array.IndexOf(args,"-player-output");
        if(destination>=0&&destination+1<args.Length)output=Path.GetFullPath(args[destination+1]);
        Directory.CreateDirectory(output);Application.runInBackground=true;Application.logMessageReceived+=Log;
    }
    void Log(string message,string stack,LogType type)
    { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+stack); }
    void Check(bool pass,string label){checks.Add((pass?"PASS ":"FAIL ")+label);Debug.Log(checks.Last());if(!pass)errors.Add(label);}
    IEnumerator Start()
    {
        float deadline=Time.realtimeSinceStartup+70;
        while((TowerSession.Instance==null||TowerSession.Instance.CurrentFloor==null)&&Time.realtimeSinceStartup<deadline)yield return null;
        if(TowerSession.Instance==null||TowerSession.Instance.CurrentFloor==null){Check(false,"Tower boot");Finish();yield break;}
        session=TowerSession.Instance;player=session.player;player.enabled=false;session.weapons.AutomatedInput=true;
        GameManager.Instance.StopFloor();
        foreach(var pulse in Object.FindObjectsByType<RootPulse>(FindObjectsSortMode.None))pulse.enabled=false;
        var shield=player.GetComponent<Shield>();var cc=player.GetComponent<CharacterController>();
        Check(player.Health==100&&shield.Current==50,"Initial player has 100 health and 50 shield");
        platform=GameObject.CreatePrimitive(PrimitiveType.Cube);platform.name="Temporary movement test floor";platform.layer=3;
        platform.transform.position=new Vector3(200,0,0);platform.transform.localScale=new Vector3(40,.2f,40);Physics.SyncTransforms();
        player.Jumped+=()=>jumps++;player.Landed+=speed=>landings++;
        ResetPosition();Step(.35f,Vector2.up);
        Check(player.HorizontalSpeed>7.9f&&player.HorizontalSpeed<=8.01f,"Ground acceleration reaches configured speed");
        Step(.3f,Vector2.zero);Check(player.HorizontalSpeed<.01f,"Releasing movement brakes without sliding");
        ResetPosition();Step(.5f,Vector2.one);
        Check(player.HorizontalSpeed<=8.01f,"Diagonal movement has no speed bonus");
        ResetPosition();float feet=cc.bounds.min.y;
        Step(.5f,Vector2.zero,true);
        Check(player.IsCrouched&&Mathf.Abs(feet-cc.bounds.min.y)<.035f,"Crouching keeps capsule feet on the floor");
        var roof=GameObject.CreatePrimitive(PrimitiveType.Cube);roof.layer=3;roof.transform.position=new Vector3(200,1.3f,0);roof.transform.localScale=new Vector3(3,.2f,3);Physics.SyncTransforms();
        Step(.3f,Vector2.zero,false);
        Check(player.IsCrouched&&!player.CanStandUp(),"Low ceiling prevents standing through geometry");
        Object.Destroy(roof);yield return null;Physics.SyncTransforms();Step(.6f,Vector2.zero,false);
        Check(!player.IsCrouched&&Mathf.Abs(cc.height-2)<.01f,"Player stands after leaving obstruction");
        ResetPosition();Step(.5f,Vector2.up);Step(.016f,Vector2.up,true);
        float slide=player.HorizontalSpeed;Step(.25f,Vector2.up,true);
        Check(slide>8&&slide<=12&&player.IsSliding,"Slide gives a bounded impulse");
        for(int i=0;i<20;i++){Step(.016f,Vector2.up,false);Step(.016f,Vector2.up,true);}
        Check(player.HorizontalSpeed<=12.01f,"Repeated crouch presses cannot stack unlimited slide speed");
        ResetPosition();int before=jumps;player.QueueJump();Step(.1f,Vector2.zero);
        Check(jumps==before+1&&player.PlayerVelocity.y>0,"Buffered input starts one jump");
        Step(1.2f,Vector2.zero);Check(landings>0&&player.Grounded,"Jump lands and reports impact");
        ResetPosition();platform.SetActive(false);Physics.SyncTransforms();Step(.048f,Vector2.zero);before=jumps;
        player.QueueJump();Step(.016f,Vector2.zero);
        Check(jumps==before+1,"Coyote jump works just after leaving an edge");
        Step(.4f,Vector2.zero);before=jumps;player.QueueJump();Step(.15f,Vector2.zero);
        Check(jumps==before,"Coyote jump cannot become a double jump");
        platform.SetActive(true);Physics.SyncTransforms();ResetPosition();
        player.TeleportTo(new Vector3(200,1.34f,0),Quaternion.identity);player.PlayerVelocity=Vector3.down*4;
        before=jumps;player.QueueJump();Step(.12f,Vector2.zero);
        Check(jumps==before+1,"Jump pressed shortly before landing is consumed on landing");
        float reference=0;
        foreach(int fps in new[]{30,60,144})
        {
            ResetPosition();player.QueueJump();float peak=player.transform.position.y;
            for(int i=0;i<fps*2;i++){player.StepMovement(1f/fps,Vector2.zero,false);peak=Mathf.Max(peak,player.transform.position.y);}
            if(fps==30)reference=peak;
            Check(Mathf.Abs(peak-reference)<.025f,"Jump height consistent at "+fps+" FPS");
        }
        ResetPosition();var position=player.transform.position;Time.timeScale=0;before=jumps;
        player.QueueJump();player.StepMovement(.04f,Vector2.up,true);float hp=player.Health;player.TakeDamage(10);
        Check(player.transform.position==position&&jumps==before&&player.Health==hp,"Pause blocks player motion, jump and damage");
        Time.timeScale=1;Step(.15f,Vector2.zero);Check(jumps==before,"Pause does not leave a queued jump");
        player.TeleportTo(new Vector3(0,1.05f,-16),Quaternion.identity);Object.Destroy(platform);
        view=player.GetComponentsInChildren<Camera>().First(c=>c.gameObject.layer!=8);
        target=new RenderTexture(1920,1080,24);target.Create();view.targetTexture=target;BindCanvases();
        PlayerDamage last=default;int hits=0;player.Damaged+=hit=>{last=hit;hits++;};
        player.TakeDamage(20,player.transform.position+Vector3.right*4,"Prueba de escudo");
        Check(shield.Current==30&&player.Health==100&&last.ShieldLoss==20&&last.HealthLoss==0,"Shield absorbs damage without health loss");
        yield return new WaitForSeconds(.1f);Capture("shield-hit.png");
        player.TakeDamage(40,player.transform.position+Vector3.left*4,"Prueba de rotura");
        Check(shield.Current==0&&player.Health==90&&last.ShieldBroken&&last.HealthLoss==10,"Shield break transfers exact overflow once");
        Check(hits==2,"Shield overflow emits one player damage event");
        yield return new WaitForSeconds(.1f);Capture("shield-break.png");
        yield return new WaitForSeconds(2.8f);player.TakeDamage(5);
        yield return new WaitForSeconds(.4f);
        Check(shield.Current==0,"Health damage restarts shield recovery delay");
        Time.timeScale=0;float remaining=shield.RecoveryDelay;yield return new WaitForSecondsRealtime(.2f);
        Check(Mathf.Approximately(shield.RecoveryDelay,remaining),"Shield recovery freezes while paused");
        Time.timeScale=1;yield return new WaitForSeconds(3.3f);
        Check(shield.Current>0&&player.Health==85,"Only shield regenerates; health needs healing");
        player.TakeDamage(-10);player.TakeDamage(float.NaN);
        Check(player.Health==85,"Invalid or negative damage cannot heal or corrupt player");
        GameManager.Instance.SetPlayerPoints(300);
        ShopManager.Instance.OpenShop();BindCanvases();
        Check(session.Menu.Visible&&Time.timeScale==0,"Shop displays player decisions and pauses combat");
        int money=GameManager.Instance.GetPlayerPoints();
        Check(session.Menu.PurchaseSupply(true)&&player.Health==100&&GameManager.Instance.GetPlayerPoints()==money-60,"Healing purchase charges once and caps health");
        money=GameManager.Instance.GetPlayerPoints();
        Check(!session.Menu.PurchaseSupply(true)&&money==GameManager.Instance.GetPlayerPoints(),"Full health does not consume currency");
        var carbine=player.GetComponentsInChildren<BaseGun>(true).First(g=>g is MachineGun);
        var capacity=UpgradeManager.Instance.catalog.First(u=>u.target==UpgradeTarget.Weapon&&u.weaponStat==WeaponStat.AmmoCapacity);
        int magazine=carbine.ammo,current=carbine.currentAmmo;UpgradeManager.Instance.BuyUpgrade(capacity);
        Check(carbine.ammo==magazine+6&&carbine.currentAmmo==current,"Stored weapon upgrade expands capacity without free ammo");
        var locked=UpgradeManager.Instance.catalog.First(u=>u.persistentUnlockId=="carbine_stability");
        money=GameManager.Instance.GetPlayerPoints();UpgradeManager.Instance.BuyUpgrade(locked);
        Check(UpgradeManager.Instance.GetLevel(locked)==0&&money==GameManager.Instance.GetPlayerPoints(),"Unresearched recipe cannot be bought");
        yield return new WaitForSecondsRealtime(.1f);Capture("shop.png");ShopManager.Instance.CloseShop();
        carbine.reserveAmmo=carbine.maxReserveAmmo-2;
        TowerAmmoPickup.Spawn(player.transform.position);
        var pickup=Object.FindFirstObjectByType<TowerAmmoPickup>();pickup.TryCollect(session);
        Check(carbine.reserveAmmo==carbine.maxReserveAmmo,"Ammo pickup respects reserve capacity");
        int cores=session.Progression.Data.cores;string token=Guid.NewGuid().ToString("N");
        Check(session.Progression.BankFloor(token,"RoboticForest",2),"Cleared objective can bank one permanent core");
        Check(!session.Progression.BankFloor(token,"RoboticForest",2)&&session.Progression.Data.cores==cores+1,"Same objective cannot award a core twice");
        session.Progression.BankFloor(token+"b","LaboratoryFloor",1);
        Check(session.Progression.TryUnlock(0)&&session.Progression.IsUnlocked("carbine_stability"),"Cores unlock an optional shop recipe");
        session.Progression.Load();Check(session.Progression.IsUnlocked("carbine_stability"),"Unlock survives a profile reload");
        GameManager.Instance.SetPlayerPoints(200);
        float recoil=carbine.Recoil.recoilKickUp;UpgradeManager.Instance.BuyUpgrade(locked);
        Check(carbine.Recoil.recoilKickUp<recoil&&UpgradeManager.Instance.GetLevel(locked)==1,"Researched recipe affects stored gun for this run");
        int deaths=0;player.Died+=()=>deaths++;int oldId=player.GetInstanceID();
        player.TakeDamage(10000,player.transform.position+Vector3.forward*5,"Robot centinela");
        player.TakeDamage(10000);
        Check(player.IsDead&&deaths==1&&session.RunEnded&&session.Menu.ShowingDeath,"Death occurs once and opens run summary");
        Check(Time.timeScale==0&&!session.weapons.enabled,"Death blocks movement and combat");
        BindCanvases();yield return new WaitForSecondsRealtime(.15f);Capture("death-summary.png");
        session.RestartRun();
        deadline=Time.realtimeSinceStartup+45;
        while((TowerSession.Instance==session||TowerSession.Instance==null||TowerSession.Instance.CurrentFloor==null)&&Time.realtimeSinceStartup<deadline)yield return null;
        var restarted=TowerSession.Instance;
        Check(restarted!=null&&restarted.player.GetInstanceID()!=oldId&&restarted.CurrentFloor.floorNumber==1,"Retry creates one fresh player at laboratory");
        if(restarted!=null&&restarted!=session)
        {
            Check(restarted.player.Health==100&&restarted.player.GetComponent<Shield>().Current==50,"Retry restores initial health and shield");
            Check(GameManager.Instance.GetPlayerPoints()==100&&UpgradeManager.Instance.GetLevel(UpgradeManager.Instance.catalog.First())==0,"Retry resets run chips and purchased stats");
            Check(restarted.Progression.IsUnlocked("carbine_stability"),"Permanent recipe survives death and full scene reload");
            Check(Object.FindObjectsByType<PlayerStateMachine>(FindObjectsSortMode.None).Length==1,"Retry has no duplicate player");
        }
        Finish();
    }
    void ResetPosition()
    { player.TeleportTo(new Vector3(200,1.15f,0),Quaternion.identity);Step(.2f,Vector2.zero); }
    void Step(float seconds,Vector2 movement,bool crouch=false)
    { for(float t=0;t<seconds;t+=.01f)player.StepMovement(Mathf.Min(.01f,seconds-t),movement,crouch); }
    void BindCanvases()
    {
        foreach(var camera in player.GetComponentsInChildren<Camera>())
            if(camera.gameObject.layer==8)camera.enabled=!session.Menu.Visible;
        foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        { canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=view;canvas.planeDistance=.15f; }
    }
    void Capture(string name)
    {
        var previous=RenderTexture.active;RenderTexture.active=target;
        var picture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        picture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);picture.Apply();
        File.WriteAllBytes(Path.Combine(output,name),picture.EncodeToPNG());Destroy(picture);RenderTexture.active=previous;
    }
    void Finish()
    {
        File.WriteAllText(Path.Combine(output,"player-results.txt"),string.Join("\n",checks)+"\nERRORS\n"+string.Join("\n",errors));
        Debug.Log("PLAYER PLAYTEST COMPLETE: "+errors.Count+" errors");Application.logMessageReceived-=Log;Time.timeScale=1;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(errors.Count==0?0:1);
#else
        Application.Quit(errors.Count==0?0:1);
#endif
    }
}
#endif
