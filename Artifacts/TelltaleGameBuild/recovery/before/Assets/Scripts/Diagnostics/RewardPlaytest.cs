#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public partial class RewardPlaytest : MonoBehaviour
{
    readonly List<string> checks=new List<string>(),errors=new List<string>();
    TowerSession session;RunRewards rewards;PlayerStateMachine player;GunController weapons;
    Camera view;RenderTexture target;string output;
    bool recording;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var args=Environment.GetCommandLineArgs();
        if(!args.Contains("-reward-qa")&&!args.Contains("-reward-record"))return;
        if(Object.FindFirstObjectByType<RewardPlaytest>()!=null)return;
        var go=new GameObject("Reward validation");DontDestroyOnLoad(go);go.AddComponent<RewardPlaytest>();
    }
    void Awake()
    {
        output=Path.GetFullPath("Artifacts/Rewards");var args=Environment.GetCommandLineArgs();
        int index=Array.IndexOf(args,"-reward-output");if(index>=0&&index+1<args.Length)output=args[index+1];
        Directory.CreateDirectory(output);recording=args.Contains("-reward-record");
        Application.runInBackground=true;Application.logMessageReceived+=Log;
    }
    void Log(string message,string stack,LogType type)
    {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+stack);}
    void Check(bool pass,string text){checks.Add((pass?"PASS ":"FAIL ")+text);Debug.Log(checks.Last());if(!pass)errors.Add(text);}
    static object Get(object o,string f)=>o.GetType().GetField(f,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
    static void Set(object o,string f,object value)=>o.GetType().GetField(f,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,value);
    IEnumerator Start()
    {
        yield return WaitFor(()=>TowerSession.Instance!=null&&TowerSession.Instance.CurrentFloor!=null,80,"Tower boot");
        if(TowerSession.Instance==null||TowerSession.Instance.CurrentFloor==null){Finish();yield break;}
        Bind();GameManager.Instance.StopFloor();
        Check(rewards!=null&&rewards.catalog.Length==21,"21 configured cards and reward service in persistent core");
        Check(rewards!=null&&rewards.catalog.All(c=>c.icon!=null),"Every card has a valid imported icon");
        if(rewards==null){Finish();yield break;}
        foreach(var pulse in Object.FindObjectsByType<RootPulse>(FindObjectsSortMode.None))pulse.enabled=false;
        player.TeleportTo(new Vector3(0,1.05f,-16),Quaternion.identity);
        if(recording){yield return RecordSequence();Finish();yield break;}
        LedgerChecks();
        var shield=player.GetComponent<Shield>();shield.ConsumeDamage(20);float delay=shield.RecoveryDelay;
        shield.Restore(5);Check(shield.Current==35&&shield.RecoveryDelay==delay,"Partial shield reward preserves regeneration delay");
        shield.Restore(999);Check(shield.Current==shield.Max,"Shield recovery caps at maximum");
        var repair=rewards.catalog.First(c=>c.kind==RewardKind.Repair);
        Check(!rewards.Eligible(repair),"Full health and shield exclude repair");
        shield.ConsumeDamage(10);Check(rewards.Eligible(repair),"Missing shield makes repair useful");
        rewards.Effect(repair).Apply(repair,rewards.Context,1);
        foreach(var gun in weapons.Weapons.OfType<BaseGun>())gun.reserveAmmo=gun.maxReserveAmmo;
        Check(rewards.catalog.Where(c=>c.kind==RewardKind.Supply).All(c=>!rewards.Eligible(c)),"Full reserves exclude all supply cards");
        var pistol=weapons.Weapons.OfType<Pistol>().First();var supply=rewards.catalog.First(c=>c.kind==RewardKind.Supply&&c.weaponId==pistol.GunTypeID);
        pistol.reserveAmmo-=1;int loaded=pistol.currentAmmo;
        rewards.Effect(supply).Apply(supply,rewards.Context,1);
        Check(pistol.reserveAmmo==pistol.maxReserveAmmo&&pistol.currentAmmo==loaded,"Supply caps reserve without filling magazine");
        yield return SpecialChecks();
        yield return ModalActionChecks();
        weapons.EquipImmediate(0);yield return new WaitForSeconds(.3f);
        pistol.currentAmmo=3;pistol.Action.BeginReload();((IAimable)pistol).StartAiming();
        Check(rewards.QueueWave(91,1,false),"Can queue first basic offer");
        Check(!rewards.QueueWave(91,1,false),"Repeated completion cannot queue a second offer");
        yield return WaitChoice();
        var offerIds=string.Join(",",rewards.Offer.Select(c=>c.id));int money=GameManager.Instance.GetPlayerPoints();
        Check(rewards.Offer.Count==3&&rewards.Offer.Select(c=>c.id).Distinct().Count()==3,"Three distinct eligible choices");
        Check(rewards.Offer.All(c=>!c.special),"First two completed waves exclude special cards");
        Check(rewards.Offer[0].weaponId==pistol.GunTypeID,"Offense prioritizes equipped weapon");
        Check(pistol.Action.State==WeaponActionState.Ready&&pistol.currentAmmo==3&&!pistol.IsAiming&&!weapons.IsSwitching,"Opening cards cancels reload, aim and switching without granting ammo");
        Check(Time.timeScale==0&&RunPause.IsPaused,"Reward owns a combat pause");
        ShopManager.Instance.OpenShop();Check(!ShopManager.Instance.IsOpen,"Shop cannot open during a pending choice");
        Check(!session.RequestTravel(session.CurrentFloor.elevator)&&!GameManager.Instance.RequestExtraWave(),"Travel and extra wave blocked by pending choice");
        var shopUpgrade=UpgradeManager.Instance.catalog.First(c=>c.target==UpgradeTarget.Weapon&&c.weaponStat==WeaponStat.Damage);
        GameManager.Instance.SetPlayerPoints(1000);int beforePurchase=GameManager.Instance.GetPlayerPoints();UpgradeManager.Instance.BuyUpgrade(shopUpgrade);
        Check(GameManager.Instance.GetPlayerPoints()==beforePurchase,"Direct purchase cannot bypass reward modal");
        pistol.reserveAmmo--;
        Check(!session.Menu.PurchaseSupply(false)&&GameManager.Instance.GetPlayerPoints()==beforePurchase,"Supply purchases cannot bypass a pending reward");
        GameManager.Instance.SetPlayerPoints(money);
        Check(string.Join(",",rewards.Offer.Select(c=>c.id))==offerIds,"Offer survives menu attempts without reroll");
        BindCanvases();yield return new WaitForSecondsRealtime(.25f);Capture("cards-basic.png");
        var cardView=session.GetComponent<RewardCardView>();cardView.Select(2);
        Check((int)Get(cardView,"selected")==2&&rewards.State==RewardState.Choosing,"Selecting a card changes focus without confirming or rerolling");
        cardView.Select(0);
        var chosen=rewards.Offer[0];float beforeDamage=pistol.damage,beforeRate=pistol.fireRate;
        Check(rewards.Confirm(0)&&!rewards.Confirm(0),"Confirmation applies once even with duplicate input");
        Check(rewards.Level(chosen)==1&&GameManager.Instance.GetPlayerPoints()==money,"Card has independent rank and no chip cost");
        Check(pistol.damage>beforeDamage||pistol.fireRate>beforeRate,"Chosen offensive card changes equipped weapon");
        yield return WaitIdle();
        Check(!RunPause.IsPaused&&Time.timeScale==1,"Reward releases its pause after feedback");
        Check(!rewards.QueueWave(91,1,false),"Completed token cannot be claimed again");
        // A separate owner must survive closing a shop or confirming a reward.
        ShopManager.Instance.OpenShop();var extraPause=RunPause.Acquire(false);ShopManager.Instance.CloseShop();
        Check(Time.timeScale==0&&RunPause.IsPaused,"Closing shop cannot release another pause owner");extraPause.Dispose();
        Check(Time.timeScale==1&&!RunPause.IsPaused,"Last pause owner restores the previous time scale");
        GameManager.Instance.SetPlayerPoints(2000);ShopManager.Instance.OpenShop();
        int shopLevel=UpgradeManager.Instance.GetLevel(shopUpgrade);float expected=(pistol.damage/((chosen.stat==WeaponStat.Damage)?1.2f:1)+shopUpgrade.GetValue(shopLevel))*((chosen.stat==WeaponStat.Damage)?1.2f:1);
        UpgradeManager.Instance.BuyUpgrade(shopUpgrade);
        Check(Mathf.Abs(pistol.damage-expected)<.001f&&rewards.Level(chosen)==1,"Shop flat is applied before card percentage, without consuming card ranks");
        BindCanvases();yield return new WaitForSecondsRealtime(.2f);Capture("shop-preview.png");
        var historyButton=Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name=="Collected cards");historyButton.onClick.Invoke();
        yield return new WaitForSecondsRealtime(.15f);Capture("card-history.png");
        Check(rewards.Summary().Contains(chosen.title),"Collected card is listed in shop history");ShopManager.Instance.CloseShop();
        Check(rewards.QueueWave(92,2,false),"Second basic offer queued");yield return WaitChoice();
        Check(rewards.Offer.All(c=>!c.special),"Second offer remains basic");rewards.Confirm(0);yield return WaitIdle();
        Check(rewards.QueueWave(93,3,true),"Floor reward queued");yield return WaitChoice();
        Check(rewards.Offer.Any(c=>c.special),"Completing a floor guarantees an available special");
        BindCanvases();yield return new WaitForSecondsRealtime(.2f);Capture("cards-special.png");
        rewards.Confirm(rewards.Offer.ToList().FindIndex(c=>c.special));yield return WaitIdle();
        var originalCatalog=rewards.catalog;
        var ranks=(Dictionary<string,int>)Get(rewards,"levels");
        foreach(var card in originalCatalog.Where(c=>c.maxLevel>0))ranks[card.id]=card.maxLevel;
        shield.SetMaxShield(shield.Max);player.Heal(10000);
        foreach(var gun in weapons.Weapons.OfType<BaseGun>())gun.reserveAmmo=gun.maxReserveAmmo;
        Check(originalCatalog.All(c=>!rewards.Eligible(c)),"Maxed stats and full supplies exhaust eligible catalogue");
        money=GameManager.Instance.GetPlayerPoints();rewards.QueueWave(94,4,false);yield return WaitIdle();
        Check(GameManager.Instance.GetPlayerPoints()==money+40,"Exhausted catalogue grants fallback chips without an empty choice");
        // Fresh ranks for the complete wave/travel run; previously exercised stat sources remain valid.
        ranks.Clear();
        yield return IntegratedRun();
        Finish();
    }
    void Bind()
    {
        session=TowerSession.Instance;rewards=session.Rewards;player=session.player;weapons=session.weapons;
        player.enabled=false;weapons.AutomatedInput=true;
        view=player.GetComponentsInChildren<Camera>().First(c=>c.gameObject.layer!=8);
        target=new RenderTexture(1920,1080,24);target.Create();view.targetTexture=target;BindCanvases();
    }
    void BindCanvases()
    {
        foreach(var camera in player.GetComponentsInChildren<Camera>())if(camera.gameObject.layer==8)camera.enabled=!(session.Menu.Visible||rewards.Pending);
        foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=view;canvas.planeDistance=.15f;}
    }
    IEnumerator WaitFor(Func<bool> condition,float seconds,string label)
    {
        float deadline=Time.realtimeSinceStartup+seconds;
        while(!condition()&&Time.realtimeSinceStartup<deadline)yield return null;
        Check(condition(),label);
    }
    IEnumerator WaitChoice()=>WaitFor(()=>rewards.State==RewardState.Choosing,5,"Reward becomes selectable");
    IEnumerator WaitIdle()=>WaitFor(()=>!rewards.Pending,5,"Reward presentation completes");
    void Capture(string name)
    {
        if(Application.isBatchMode)return;
        var previous=RenderTexture.active;RenderTexture.active=target;
        var picture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        picture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);picture.Apply();
        File.WriteAllBytes(Path.Combine(output,name),picture.EncodeToPNG());Destroy(picture);RenderTexture.active=previous;
    }
    void Finish()
    {
        File.WriteAllText(Path.Combine(output,recording?"record-results.txt":"results.txt"),string.Join("\n",checks)+"\nERRORS\n"+string.Join("\n",errors));
        Debug.Log("REWARD TEST COMPLETE: "+errors.Count+" errors");Application.logMessageReceived-=Log;
        if(!RunPause.IsPaused)Time.timeScale=1;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(errors.Count==0?0:1);
#else
        Application.Quit(errors.Count==0?0:1);
#endif
    }
}
#endif
