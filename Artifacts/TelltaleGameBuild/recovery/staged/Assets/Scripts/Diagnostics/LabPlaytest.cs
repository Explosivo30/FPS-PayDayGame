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
using UnityEngine.UI;
using Object=UnityEngine.Object;

/// <summary>Opt-in integration harness. Normal gameplay never creates this component.</summary>
public class LabPlaytest : MonoBehaviour
{
    readonly List<string> results=new List<string>();
    readonly List<string> errors=new List<string>();
    GunController controller;
    PlayerStateMachine player;
    GameManager manager;
    Camera cameraView;
    RenderTexture target;
    string output;
    bool preview,record;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var args=Environment.GetCommandLineArgs();
        if(!args.Contains("-lab-preview")&&!args.Contains("-lab-qa")&&!args.Contains("-lab-record")&&!args.Contains("-lab-smoke")&&!args.Contains("-lab-perf")) return;
        var gm=Object.FindFirstObjectByType<GameManager>();if(gm==null)return;gm.AutomaticWaves=false;
        new GameObject("Laboratory validation").AddComponent<LabPlaytest>();
    }
    void Awake()
    {
        preview=Environment.GetCommandLineArgs().Contains("-lab-preview");
        record=Environment.GetCommandLineArgs().Contains("-lab-record");
        output=Path.Combine(Application.dataPath,"../Artifacts");
        var args=Environment.GetCommandLineArgs();int outputIndex=Array.IndexOf(args,"-lab-output");
        if(outputIndex>=0&&outputIndex+1<args.Length)output=args[outputIndex+1];
        Application.runInBackground=true;
        Directory.CreateDirectory(output);
        Application.logMessageReceived+=Log;
    }
    void Log(string text,string stack,LogType type)
    {
        if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert) errors.Add(text+"\n"+stack);
    }
    IEnumerator Start()
    {
        yield return null;yield return null;
        player=Object.FindFirstObjectByType<PlayerStateMachine>();controller=player.GetComponentInChildren<GunController>();
        manager=GameManager.Instance;
        player.enabled=false;controller.AutomatedInput=true;
        cameraView=player.GetComponentsInChildren<Camera>().First(c=>c.gameObject.layer!=8);
        target=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);target.Create();cameraView.targetTexture=target;
        foreach(var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if(c.name=="Laboratory HUD") { c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=cameraView;c.planeDistance=.15f; }
        }
        yield return new WaitForSeconds(.5f);
        if(Environment.GetCommandLineArgs().Contains("-lab-smoke")) { yield return new WaitForSeconds(3);Check(player!=null,"Level1 starts with the shared player");Finish();yield break; }
        if(record) { yield return Record();Finish();yield break; }
        if(Environment.GetCommandLineArgs().Contains("-lab-perf")) { yield return Performance();Finish();yield break; }
        View(new Vector3(0,1.05f,-15),Quaternion.Euler(2,0,0));
        for(int i=0;i<controller.Weapons.Count;i++)
        {
            controller.EquipImmediate(i);yield return new WaitForSeconds(.4f);
            Capture("weapon-"+i+".png");
            if(controller.currentWeapon is IAimable aim) { aim.StartAiming();yield return new WaitForSeconds(.4f);Capture("aim-"+i+".png");aim.StopAiming(); }
        }
        View(new Vector3(-17,5.3f,-12),Quaternion.Euler(12,37,0));
        yield return new WaitForSeconds(.2f);Capture("laboratory.png");
        Check(controller.Weapons.Count==4,"Four equipped weapons");
        Check(Object.FindObjectsByType<NormalEnemyStateMachine>(FindObjectsSortMode.None).Length==0,"No uncounted scene enemies");
        foreach(float x in new[]{-13f,13f})
        {
            bool start=NavMesh.SamplePosition(new Vector3(x,0,-4),out var a,1,NavMesh.AllAreas);
            bool end=NavMesh.SamplePosition(new Vector3(x,3,9),out var b,1,NavMesh.AllAreas);
            var path=new NavMeshPath();
            Check(start&&end&&NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,"Walkable elevated access "+x);
        }
        RecoilTests();
        if(preview) { Finish();yield break; }
        View(new Vector3(0,1.05f,-15),Quaternion.identity);
        yield return WeaponTests();
        if(errors.Count>0) { Finish();yield break; }
        yield return Traverse();
        yield return Waves();
        Finish();
    }
    void Check(bool pass,string name)
    {
        results.Add((pass?"PASS ":"FAIL ")+name);Debug.Log(results.Last());
        if(!pass) errors.Add(name);
    }
    void RecoilTests()
    {
        var data=ScriptableObject.CreateInstance<RecoilData>();data.recoilKickUp=.55f;data.recoilKickSide=0;data.verticalKickVariation=0;
        data.recoilSnappiness=38;data.maxVerticalRecoil=6;data.recoilReturnSpeed=10;
        float[] peaks=new float[3];int index=0;
        var recoil=GunRecoil.EnsureExists();
        foreach(int fps in new[]{30,60,144})
        {
            recoil.ResetRecoil();float peak=0;int shots=0;float sum=0;
            for(int frame=0;frame<fps*4;frame++)
            {
                float t=frame/(float)fps;
                if(shots<20&&t+.00001f>=shots*.1f) { recoil.ApplyRecoilAtTime(data,1,.1f,t);shots++; }
                sum+=recoil.Step(1f/fps,t,Vector2.zero).y;peak=Mathf.Max(peak,sum);
            }
            peaks[index++]=peak;
            Check(Mathf.Abs(sum)<.01f && recoil.Offset.magnitude<.01f,"Recoil returns without drift at "+fps+" FPS");
        }
        Check(peaks.Max()-peaks.Min()<.2f,"Recoil peak differs by less than 0.2 degrees across frame rates");
        recoil.ResetRecoil();recoil.ApplyRecoilAtTime(data,1,.1f,0);
        for(int i=0;i<8;i++)recoil.Step(1f/60,i/60f,Vector2.zero);
        float debt=recoil.Offset.y;recoil.Step(1f/60,.14f,new Vector2(0,-debt));
        Check(recoil.Offset.y<.03f,"Mouse compensation consumes the pending recovery");
        recoil.ResetRecoil();Object.Destroy(data);
    }
    IEnumerator WeaponTests()
    {
        var dummy=GameObject.CreatePrimitive(PrimitiveType.Cube);
        dummy.name="QA damage target";dummy.layer=6;dummy.transform.position=cameraView.transform.position+cameraView.transform.forward*5;
        dummy.transform.localScale=new Vector3(8,8,.2f);var damage=dummy.AddComponent<LabDamageTarget>();
        Physics.SyncTransforms();
        controller.EquipImmediate(0);yield return new WaitForSeconds(.3f);
        var pistol=(BaseGun)controller.currentWeapon;
        int ammo=pistol.currentAmmo;pistol.Use();pistol.Use();
        Check(pistol.currentAmmo==ammo-1&&damage.Total==30,"Pistol cadence and one ammunition debit");
        yield return new WaitForSeconds(.25f);
        pistol.currentAmmo=4;pistol.Reload();yield return new WaitForSeconds(.5f);
        controller.SelectWeapon(1);yield return new WaitForSeconds(.5f);
        Check(pistol.currentAmmo==4,"Switch cancels magazine reload before ammunition commits");
        var carbine=(BaseGun)controller.currentWeapon;carbine.currentAmmo=2;carbine.Reload();
        yield return new WaitForSeconds(1.95f);
        Check(carbine.currentAmmo==30&&!carbine.Action.IsBusy,"Carbine reload commits after 1.8 seconds");
        controller.EquipImmediate(2);yield return new WaitForSeconds(.3f);
        var shotgun=(ShotGun)controller.currentWeapon;damage.Total=0;ammo=shotgun.currentAmmo;
        shotgun.Use();Check(shotgun.currentAmmo==ammo-1&&damage.Total==80,"Shotgun emits eight damage rays for one cartridge");
        shotgun.currentAmmo=0;yield return new WaitForSeconds(.85f);shotgun.Reload();
        yield return new WaitForSeconds(.84f);
        Check(shotgun.currentAmmo==1,"Shotgun inserts one cartridge after opening");
        controller.SelectWeapon(0);yield return new WaitForSeconds(.5f);
        Check(shotgun.currentAmmo==1,"Interrupted shell reload preserves inserted cartridges");
        pistol=(BaseGun)controller.currentWeapon;pistol.currentAmmo=2;pistol.Reload();
        yield return new WaitForSeconds(.2f);
        Time.timeScale=0;float progress=pistol.Action.Progress;ammo=pistol.currentAmmo;pistol.Use();pistol.Action.Tick(2);
        Check(pistol.currentAmmo==ammo&&Mathf.Approximately(progress,pistol.Action.Progress),"Pause blocks firing and freezes reload progress");
        Time.timeScale=1;yield return new WaitForSeconds(1.3f);Check(pistol.currentAmmo==pistol.ammo,"Reload resumes after pause");
        var original=Get<RecoilData>(carbine,"recoilData");
        float originalKick=original.recoilKickUp,previousKick=carbine.Recoil.recoilKickUp;
        carbine.ApplyWeaponStat(WeaponStat.RecoilKickUp,.1f);
        Check(!carbine.gameObject.activeSelf&&carbine.Recoil.recoilKickUp<previousKick&&Mathf.Approximately(original.recoilKickUp,originalKick),"Inactive weapon upgrades are isolated from shared recoil assets");
        controller.EquipImmediate(3);yield return new WaitForSeconds(.3f);
        dummy.transform.position=cameraView.transform.position+cameraView.transform.forward*1.3f;dummy.transform.localScale=new Vector3(1,2,.1f);Physics.SyncTransforms();damage.Total=0;
        var knife=(Knife)controller.currentWeapon;knife.Use();yield return new WaitForSeconds(.22f);
        Check(damage.Total==50,"Knife damages each target once during the contact window");
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=cameraView.transform.position+cameraView.transform.forward*.7f;wall.transform.localScale=new Vector3(3,3,.1f);wall.layer=3;Physics.SyncTransforms();damage.Total=0;
        yield return new WaitForSeconds(.5f);knife.Use();yield return new WaitForSeconds(.22f);
        Check(damage.Total==0,"Knife cannot hit through cover");
        Object.Destroy(wall);Object.Destroy(dummy);
        yield return null;
    }
    IEnumerator Traverse()
    {
        var cc=player.GetComponent<CharacterController>();
        foreach(float x in new[]{-13f,13f})
        {
            View(new Vector3(x,1.03f,-4),Quaternion.identity);yield return null;
            float until=Time.time+4;
            while(Time.time<until && player.transform.position.z<8) { cc.Move((Vector3.forward*4+Vector3.down*2)*Time.deltaTime);yield return null; }
            Check(player.transform.position.y>3.7f&&player.transform.position.z>6.5f,"CharacterController traverses ramp "+x);
        }
    }
    IEnumerator Waves()
    {
        View(new Vector3(0,1.05f,-13),Quaternion.identity);controller.EquipImmediate(1);
        Time.timeScale=2;
        player.GetComponent<CharacterController>().enabled=false;
        var bot=player.gameObject.AddComponent<NavMeshAgent>();
        bot.radius=.5f;bot.height=2;bot.baseOffset=1;bot.speed=6;bot.acceleration=30;bot.stoppingDistance=.25f;bot.updateRotation=false;
        if(NavMesh.SamplePosition(player.transform.position,out var startPoint,2,NavMesh.AllAreas))bot.Warp(startPoint.position);
        manager.BeginWaves();float end=Time.time+360;float shotClock=0;
        // The bot uses real hitscan and ammunition. Health is replenished only by this opt-in harness.
        int maxAlive=0;float nextStatus=0;float realDeadline=Time.realtimeSinceStartup+300;
        while(manager.CompletedWaves<5&&Time.time<end&&Time.realtimeSinceStartup<realDeadline)
        {
            Set(player,"currentHPPlayer",100f);
            maxAlive=Mathf.Max(maxAlive,manager.ActiveEnemies);
            if(Time.realtimeSinceStartup>nextStatus)
            {
                nextStatus=Time.realtimeSinceStartup+5;
                File.WriteAllText(Path.Combine(output,"wave-progress.txt"),$"wave={manager.CurrentWave} completed={manager.CompletedWaves} live={manager.ActiveEnemies} remaining={manager.EnemiesRemaining} position={player.transform.position} time={Time.time} deadline={end}");
            }
            var enemies=Object.FindObjectsByType<NormalEnemyStateMachine>(FindObjectsSortMode.None).Where(e=>!e.IsDead).ToArray();
            var enemy=enemies.OrderBy(e=>Vector3.Distance(player.transform.position,e.transform.position)).FirstOrDefault();
            if(enemy!=null)
            {
                Vector3 delta=enemy.AimPoint-cameraView.transform.position;
                bool visible=Physics.Raycast(cameraView.transform.position,delta.normalized,out var hit,40,~(1<<7|1<<8|1<<2),QueryTriggerInteraction.Ignore)&&hit.collider.GetComponentInParent<NormalEnemyStateMachine>()==enemy;
                var currentGun=controller.currentWeapon as BaseGun;
                var muzzle=currentGun!=null?Get<Transform>(currentGun,"muzzleTransform"):null;
                if(muzzle!=null && Physics.Linecast(muzzle.position,enemy.AimPoint,out var muzzleHit,~(1<<7|1<<8|1<<2),QueryTriggerInteraction.Ignore))
                    visible &= muzzleHit.collider.GetComponentInParent<NormalEnemyStateMachine>()==enemy;
                if(!visible||Vector3.Distance(player.transform.position,enemy.transform.position)<3)
                {
                    if(bot.isOnNavMesh) { bot.isStopped=false;bot.SetDestination(FiringPosition(enemy)); }
                }
                else if(bot.isOnNavMesh)bot.isStopped=true;
                LookAt(enemy.AimPoint);
                var gun=controller.currentWeapon as BaseGun;
                if(gun!=null)
                {
                    if(gun.currentAmmo==0) gun.Reload();
                    else if(visible&&Time.time>shotClock) { gun.Use();shotClock=Time.time+.1f; }
                }
            }
            yield return null;
        }
        Check(manager.CompletedWaves>=5,"Five waves complete through real weapon hits");
        Check(maxAlive<=12,"No more than twelve live enemies");
        results.Add("Automated test at 2x simulation speed; bot health replenished for repeatability.");
        Time.timeScale=1;
        Object.Destroy(bot);player.GetComponent<CharacterController>().enabled=true;
        Capture("combat.png");
    }
    Vector3 FiringPosition(NormalEnemyStateMachine enemy)
    {
        Vector3 best=enemy.transform.position;float bestDistance=float.PositiveInfinity;
        for(int i=0;i<12;i++)
        {
            float angle=i*Mathf.PI/6;
            Vector3 point=enemy.transform.position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*6;
            if(!NavMesh.SamplePosition(point,out var sample,2,NavMesh.AllAreas))continue;
            if(!Physics.Linecast(sample.position+Vector3.up*1.6f,enemy.AimPoint,out var hit,~(1<<7|1<<8|1<<2),QueryTriggerInteraction.Ignore)||hit.collider.GetComponentInParent<NormalEnemyStateMachine>()!=enemy)continue;
            float distance=Vector3.Distance(player.transform.position,sample.position);
            if(distance<bestDistance){best=sample.position;bestDistance=distance;}
        }
        return best;
    }
    IEnumerator Performance()
    {
        QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
        var prefab=Get<GameObject>(manager,"enemyPrefab");
        for(int i=0;i<12;i++)
        {
            float a=i*Mathf.PI/6;Vector3 p=new Vector3(Mathf.Cos(a)*12,0,Mathf.Sin(a)*10);
            if(NavMesh.SamplePosition(p,out var sample,2,NavMesh.AllAreas))Object.Instantiate(prefab,sample.position,Quaternion.identity);
        }
        yield return new WaitForSeconds(4);
        var times=new List<float>();float start=Time.realtimeSinceStartup,previous=start;
        while(Time.realtimeSinceStartup-start<20)
        {
            Set(player,"currentHPPlayer",100f);
            LookAt(new Vector3(Mathf.Sin(Time.realtimeSinceStartup*.5f)*8,1.3f,2));
            yield return null;
            float now=Time.realtimeSinceStartup;times.Add((now-previous)*1000);previous=now;
        }
        times.Sort();float mean=times.Average();
        string report=$"GPU: {SystemInfo.graphicsDeviceName}\nCPU: {SystemInfo.processorType}\nResolution: {target.width}x{target.height}\nRendered frames: {times.Count}\nAverage FPS: {1000/mean:F1}\nMedian frame ms: {times[times.Count/2]:F2}\n95th percentile frame ms: {times[(int)(times.Count*.95f)]:F2}\nTwelve moving enemies, uncapped, development player, camera render target.\n";
        File.WriteAllText(Path.Combine(output,"performance.txt"),report);
        results.Add(report);
    }
    IEnumerator Record()
    {
        string frames=Path.Combine(output,"Frames");Directory.CreateDirectory(frames);
        Time.captureFramerate=30;
        var enemyPrefab=Get<GameObject>(manager,"enemyPrefab");
        var enemy=Object.Instantiate(enemyPrefab,new Vector3(-4,0,-5),Quaternion.identity).GetComponent<NormalEnemyStateMachine>();
        enemy.enabled=false;enemy.agent.enabled=false;
        int frame=0;
        for(int weapon=0;weapon<4;weapon++)
        {
            controller.EquipImmediate(weapon);
            View(new Vector3(0,1.05f,-14),Quaternion.Euler(2,-8,0));
            for(int i=0;i<150;i++)
            {
                float seconds=i/30f;
                var gun=controller.currentWeapon as BaseGun;
                if(i==25&&controller.currentWeapon is IAimable aim)aim.StartAiming();
                if(i==52&&controller.currentWeapon is IAimable aimed)aimed.StopAiming();
                if(gun!=null&&i>=65&&i<100&&i%8==0)gun.Use();
                if(gun!=null&&i==103)gun.Reload();
                if(gun==null&&(i==45||i==80||i==115))controller.currentWeapon.Use();
                LookAt(new Vector3(Mathf.Sin(seconds*.5f)*2,1.5f,1));
                yield return null;
                CaptureFrame(Path.Combine(frames,$"frame-{frame++:0000}.jpg"));
            }
        }
        Time.captureFramerate=0;
        Object.Destroy(enemy.gameObject);
        results.Add("Captured "+frame+" frames at 1920x1080, 30 fps.");
    }
    void CaptureFrame(string path)
    {
        var previous=RenderTexture.active;RenderTexture.active=target;
        var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
        File.WriteAllBytes(path,image.EncodeToJPG(88));Object.Destroy(image);RenderTexture.active=previous;
    }
    void View(Vector3 position,Quaternion rotation)
    {
        var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.SetPositionAndRotation(position,Quaternion.Euler(0,rotation.eulerAngles.y,0));cc.enabled=true;
        player.headCam.localRotation=Quaternion.Euler(rotation.eulerAngles.x,0,0);Physics.SyncTransforms();
    }
    void LookAt(Vector3 point)
    {
        var rotation=Quaternion.LookRotation(point-cameraView.transform.position);
        player.transform.rotation=Quaternion.Euler(0,rotation.eulerAngles.y,0);player.headCam.localRotation=Quaternion.Euler(rotation.eulerAngles.x,0,0);
    }
    void Capture(string name)
    {
        var previous=RenderTexture.active;RenderTexture.active=target;
        var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
        File.WriteAllBytes(Path.Combine(output,name),image.EncodeToPNG());Object.Destroy(image);RenderTexture.active=previous;
    }
    static T Get<T>(object obj,string name)
    {
        var type=obj.GetType();while(type!=null){var f=type.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null)return (T)f.GetValue(obj);type=type.BaseType;}return default;
    }
    static void Set(object obj,string name,object value)
    {
        var type=obj.GetType();while(type!=null){var f=type.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null){f.SetValue(obj,value);return;}type=type.BaseType;}
    }
    void Finish()
    {
        File.WriteAllText(Path.Combine(output,record?"recording-results.txt":Environment.GetCommandLineArgs().Contains("-lab-smoke")?"level1-results.txt":preview?"preview-results.txt":"playtest-results.txt"),string.Join("\n",results)+"\nERRORS\n"+string.Join("\n",errors));
        Debug.Log("LAB PLAYTEST COMPLETE: "+errors.Count+" errors");
        Application.logMessageReceived-=Log;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(errors.Count==0?0:1);
#else
        Application.Quit(errors.Count==0?0:1);
#endif
    }
}
public class LabDamageTarget:MonoBehaviour,IDamageable
{
    public float Total;
    public void TakeDamage(float amount) { Total+=amount; }
}
#endif
