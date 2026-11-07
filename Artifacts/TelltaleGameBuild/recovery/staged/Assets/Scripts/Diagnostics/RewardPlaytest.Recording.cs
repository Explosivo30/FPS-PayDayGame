#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

public partial class RewardPlaytest
{
    [Serializable] class RecordingInfo { public int frames;public float seconds,audioSeconds,audioPeak; }
    IEnumerator RecordSequence()
    {
        Directory.CreateDirectory(Path.Combine(output,"Frames"));
        Application.targetFrameRate=30;QualitySettings.vSyncCount=0;
        var manager=GameManager.Instance;
        // A shortened actual wave: the normal death, wave, score and reward pipeline is retained.
        Set(manager,"baseEnemyCount",1);Set(manager,"spawnInterval",.05f);
        session.CurrentFloor.introductionSeconds=.1f;manager.ConfigureFloor(session.CurrentFloor);
        weapons.EquipImmediate(0);
        yield return WaitFor(()=>manager.ActiveEnemies>0,10,"Recording wave spawned");
        var enemy=UnityEngine.Object.FindObjectsByType<NormalEnemyStateMachine>(FindObjectsSortMode.None).FirstOrDefault(e=>!e.IsDead);
        if(enemy==null)yield break;
        enemy.enabled=false;enemy.ConfigureDifficulty(40,5,.5f);
        if(enemy.agent.isOnNavMesh){enemy.agent.isStopped=true;enemy.agent.Warp(new Vector3(0,0,-11));}
        enemy.transform.position=new Vector3(0,0,-11);enemy.transform.rotation=Quaternion.Euler(0,180,0);
        var pistol=weapons.Weapons.OfType<Pistol>().First();pistol.Recoil.spreadHip=pistol.Recoil.spreadADS=0;
        yield return new WaitForSeconds(.3f);
        var audio=player.GetComponentInChildren<AudioListener>().gameObject.AddComponent<CombatFeelAudioCapture>();audio.Begin();
        int frame=0;float start=Time.realtimeSinceStartup;float chosenAt=-1;bool purchased=false,captured=false,shopCaptured=false;
        float nextShot=Time.unscaledTime+.7f;bool completed=false;
        while(Time.realtimeSinceStartup-start<11)
        {
            float elapsed=Time.realtimeSinceStartup-start;
            if(enemy!=null&&!enemy.IsDead)
            {
                Aim(enemy.AimPoint);
                if(Time.unscaledTime>=nextShot){pistol.Use();nextShot=Time.unscaledTime+.45f;}
            }
            completed|=manager.CompletedWaves==1&&rewards.Pending;
            if(rewards.State==RewardState.Choosing&&elapsed>3&&!captured)
            {captured=true;Capture("cards-in-game.png");}
            if(rewards.State==RewardState.Choosing&&elapsed>4&&chosenAt<0)
            {
                var rewardView=session.GetComponent<RewardCardView>();rewardView.Select(0);rewardView.Confirm();chosenAt=Time.realtimeSinceStartup;
            }
            if(chosenAt>0&&!rewards.Pending&&!purchased&&Time.realtimeSinceStartup-chosenAt>1)
            {
                purchased=true;GameManager.Instance.SetPlayerPoints(200);
                ShopManager.Instance.OpenShop();
                var upgrade=UpgradeManager.Instance.catalog.First(c=>c.target==UpgradeTarget.Weapon&&c.weaponStat==WeaponStat.Damage&&c.gunTypeID=="Pistol");
                UpgradeManager.Instance.BuyUpgrade(upgrade);
            }
            if(purchased&&!shopCaptured&&Time.realtimeSinceStartup-chosenAt>2)
            {shopCaptured=true;Capture("shop-in-game.png");}
            if(purchased&&Time.realtimeSinceStartup-chosenAt>3)ShopManager.Instance.CloseShop();
            BindCanvases();yield return null;
            var previous=RenderTexture.active;RenderTexture.active=target;
            var picture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            picture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);picture.Apply();
            File.WriteAllBytes(Path.Combine(output,"Frames","frame-"+frame.ToString("0000")+".jpg"),picture.EncodeToJPG(88));
            Destroy(picture);RenderTexture.active=previous;frame++;
        }
        audio.End(Path.Combine(output,"rewards-audio.wav"));
        File.WriteAllText(Path.Combine(output,"recording.json"),JsonUtility.ToJson(new RecordingInfo{frames=frame,seconds=Time.realtimeSinceStartup-start,audioSeconds=audio.Seconds,audioPeak=audio.Peak},true));
        Check(completed&&chosenAt>0&&purchased&&!rewards.Pending,"Recorded actual wave completion, choice, purchase and return to combat");
        Destroy(audio);
    }
}
#endif
