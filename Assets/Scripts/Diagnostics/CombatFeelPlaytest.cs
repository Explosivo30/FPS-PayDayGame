#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public class CombatFeelPlaytest : MonoBehaviour
{
    readonly List<string> checks=new List<string>(),errors=new List<string>();
    PlayerStateMachine player;
    GunController weapons;
    GameObject enemyPrefab;
    Camera view;
    RenderTexture target;
    string output;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var args=Environment.GetCommandLineArgs();
        if(!args.Contains("-feel-qa")&&!args.Contains("-feel-record"))return;
        new GameObject("Combat feel validation").AddComponent<CombatFeelPlaytest>();
    }
    void Awake()
    {
        output=Path.GetFullPath(Path.Combine(Application.dataPath,"../Artifacts/CombatFeel"));
        var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-feel-output");
        if(index>=0&&index+1<args.Length)output=args[index+1];
        Directory.CreateDirectory(output);Application.runInBackground=true;
        Application.logMessageReceived+=Log;
    }
    void Log(string text,string stack,LogType type)
    { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(text+"\n"+stack); }
    void Check(bool pass,string label) { checks.Add((pass?"PASS ":"FAIL ")+label);Debug.Log(checks.Last());if(!pass)errors.Add(label); }
    IEnumerator Start()
    {
        float deadline=Time.realtimeSinceStartup+60;
        while((TowerSession.Instance==null||TowerSession.Instance.CurrentFloor==null)&&Time.realtimeSinceStartup<deadline)yield return null;
        if(TowerSession.Instance==null||TowerSession.Instance.CurrentFloor==null) { Check(false,"Tower boot");Finish();yield break; }
        player=TowerSession.Instance.player;weapons=TowerSession.Instance.weapons;
        player.enabled=false;weapons.AutomatedInput=true;
        enemyPrefab=(GameObject)typeof(GameManager).GetField("enemyPrefab",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(GameManager.Instance);
        GameManager.Instance.StopFloor();
        player.TeleportTo(new Vector3(0,1.05f,-16),Quaternion.identity);
        view=player.GetComponentsInChildren<Camera>().First(c=>c.gameObject.layer!=8);
        target=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);target.Create();view.targetTexture=target;
        foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if(canvas.name=="Laboratory HUD"||canvas.name=="Tower HUD")
            { canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=view;canvas.planeDistance=.15f; }
        TowerSession.Instance.ShowMessage("COMBATE / IMPACTOS Y REACCIONES",60);
        yield return new WaitForSeconds(.4f);
        if(Environment.GetCommandLineArgs().Contains("-feel-record"))
        {
            var capture=gameObject.AddComponent<CombatFeelRecording>();
            yield return capture.Record(player,weapons,view,target,enemyPrefab,output);
            Finish();yield break;
        }
        var enemy=Spawn(2000,7);
        var robot=enemy.GetComponent<RobotPresentation>();
        var originalMaterial=enemyPrefab.GetComponent<RobotPresentation>().visual.GetComponentInChildren<MeshRenderer>().sharedMaterial;
        Color originalColor=originalMaterial.GetColor("_BaseColor");
        var audio=weapons.GetComponent<ImpactFeedbackPlayer>();
        weapons.EquipImmediate(0);yield return new WaitForSeconds(.3f);
        var pistol=weapons.currentWeapon as BaseGun;ZeroSpread(pistol);
        Aim(enemy.AimPoint);
        Vector3 navigationRoot=enemy.transform.position;
        float health=enemy.Health;int cue=audio.CuesPlayed;
        pistol.Use();yield return null;
        Check(Mathf.Approximately(health-enemy.Health,30),"Pistol damage unchanged");
        Check(robot.ImpactBursts==1&&robot.FlashStrength>0,"One visible flash and spark burst at real contact");
        Check(robot.VisualDisplacement>0&&robot.VisualDisplacement<=.131f,"Bounded directional visual hit reaction");
        Check(Vector3.Distance(navigationRoot,enemy.transform.position)<.001f,"Hit animation does not move navigation root");
        Check(audio.CuesPlayed==cue+1,"One impact confirmation");
        Capture("pistol-impact.png");
        yield return new WaitForSeconds(.8f);
        Check(robot.VisualDisplacement<.001f&&robot.FlashStrength==0,"Reaction recovers without drift");
        Check(originalMaterial.GetColor("_BaseColor")==originalColor,"Shared robot material remains unchanged");
        weapons.EquipImmediate(2);yield return new WaitForSeconds(.3f);
        var shotgun=weapons.currentWeapon as BaseGun;ZeroSpread(shotgun);Aim(enemy.AimPoint);
        health=enemy.Health;int burst=robot.ImpactBursts;cue=audio.CuesPlayed;int ammo=shotgun.currentAmmo;
        shotgun.Use();yield return null;
        Check(Mathf.Approximately(health-enemy.Health,80)&&shotgun.currentAmmo==ammo-1,"Eight pellets keep damage and consume one shell");
        Check(robot.ImpactBursts==burst+1&&audio.CuesPlayed==cue+1,"Eight pellet contacts merge into one visual and audio cue");
        Check(Mathf.Approximately(audio.LastImpact.Damage,80),"Merged contact retains full pellet energy");
        Capture("shotgun-impact.png");
        yield return new WaitForSeconds(.8f);
        weapons.EquipImmediate(1);yield return new WaitForSeconds(.3f);
        var carbine=weapons.currentWeapon as BaseGun;ZeroSpread(carbine);
        for(int i=0;i<18;i++) { Aim(enemy.AimPoint);carbine.Use();yield return new WaitForSeconds(.105f); }
        Check(robot.VisualDisplacement<.14f,"Sustained automatic fire cannot accumulate unlimited displacement");
        yield return new WaitForSeconds(1);
        Check(robot.VisualDisplacement<.001f,"Automatic fire recovers fully");
        Time.timeScale=0;health=enemy.Health;ammo=carbine.currentAmmo;cue=audio.CuesPlayed;
        carbine.Use();yield return new WaitForSecondsRealtime(.12f);
        Check(enemy.Health==health&&carbine.currentAmmo==ammo&&audio.CuesPlayed==cue,"Pause blocks shots and new impact cues");
        Time.timeScale=1;
        Object.Destroy(enemy.gameObject);yield return null;
        enemy=Spawn(20,7);robot=enemy.GetComponent<RobotPresentation>();
        weapons.EquipImmediate(0);yield return new WaitForSeconds(.3f);Aim(enemy.AimPoint);
        int deaths=0;Action<IDamageable> onDeath=d=>{if((d as NormalEnemyStateMachine)==enemy)deaths++;};EnemyEvents.OnDeath+=onDeath;
        cue=audio.CuesPlayed;pistol.Use();yield return null;
        Check(enemy.IsDead&&deaths==1&&audio.LastImpact.Fatal,"Fatal shot emits a single kill with contact information");
        Check(Vector3.Dot(robot.LastHitDirection,view.transform.forward)>.95f,"Fatal animation remembers the shot direction");
        Check(enemy.GetComponentsInChildren<Collider>().All(c=>!c.enabled),"Death still disables all hit colliders");
        Capture("elimination.png");
        enemy.TakeDamage(1000);Check(deaths==1,"Repeated damage cannot duplicate a kill");EnemyEvents.OnDeath-=onDeath;
        yield return new WaitForSeconds(.7f);Capture("robot-collapse.png");
        Object.Destroy(enemy.gameObject);yield return null;
        enemy=Spawn(300,1.35f);weapons.EquipImmediate(3);yield return new WaitForSeconds(.3f);
        Aim(enemy.AimPoint);health=enemy.Health;cue=audio.CuesPlayed;
        weapons.currentWeapon.Use();yield return new WaitForSeconds(.08f);
        Check(audio.CuesPlayed==cue&&enemy.Health==health,"Knife feedback waits for contact rather than swing start");
        yield return new WaitForSeconds(.15f);
        Check(Mathf.Approximately(health-enemy.Health,50)&&audio.CuesPlayed==cue+1&&audio.LastImpact.Melee,"Knife contact damages and confirms once");
        Capture("knife-impact.png");
        Object.Destroy(enemy.gameObject);
        SpringCheck();
        Finish();
    }
    NormalEnemyStateMachine Spawn(float health,float distance)
    {
        var go=Object.Instantiate(enemyPrefab,new Vector3(0,0,-16+distance),Quaternion.Euler(0,180,0));
        var enemy=go.GetComponent<NormalEnemyStateMachine>();enemy.ConfigureDifficulty(health,8,.55f);
        enemy.enabled=false;
        if(enemy.agent.isOnNavMesh)enemy.agent.isStopped=true;
        return enemy;
    }
    void Aim(Vector3 point)
    {
        var rotation=Quaternion.LookRotation(point-view.transform.position);
        player.transform.rotation=Quaternion.Euler(0,rotation.eulerAngles.y,0);
        player.headCam.localRotation=Quaternion.Euler(rotation.eulerAngles.x,0,0);
        Physics.SyncTransforms();
    }
    void ZeroSpread(BaseGun gun) { gun.Recoil.spreadHip=gun.Recoil.spreadADS=0; }
    void SpringCheck()
    {
        var method=typeof(RobotPresentation).GetMethod("Spring",BindingFlags.Static|BindingFlags.NonPublic);
        Vector3 reference=Vector3.zero;
        foreach(int fps in new[]{30,60,144})
        {
            object[] args={new Vector3(.1f,.02f,.1f),new Vector3(1,0,1),24f,1f/fps};
            for(int i=0;i<fps/2;i++)method.Invoke(null,args);
            var position=(Vector3)args[0];
            if(fps==30)reference=position;
            Check(position.magnitude<.001f&&Vector3.Distance(position,reference)<.00001f,"Visual spring consistent at "+fps+" FPS");
        }
    }
    void Capture(string name)
    {
        var previous=RenderTexture.active;RenderTexture.active=target;
        var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
        File.WriteAllBytes(Path.Combine(output,name),image.EncodeToPNG());Object.Destroy(image);RenderTexture.active=previous;
    }
    void Finish()
    {
        string name=Environment.GetCommandLineArgs().Contains("-feel-record")?"record-results.txt":"feel-results.txt";
        File.WriteAllText(Path.Combine(output,name),string.Join("\n",checks)+"\nERRORS\n"+string.Join("\n",errors));
        Debug.Log("COMBAT FEEL COMPLETE: "+errors.Count+" errors");Application.logMessageReceived-=Log;
        Time.timeScale=1;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(errors.Count==0?0:1);
#else
        Application.Quit(errors.Count==0?0:1);
#endif
    }
}
#endif
