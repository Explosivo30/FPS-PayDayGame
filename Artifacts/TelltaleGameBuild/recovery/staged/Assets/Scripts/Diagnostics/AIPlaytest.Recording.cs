#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
public partial class AIPlaytest
{
    [Serializable] class RecordingInfo {public int frames;public float seconds,audioSeconds,audioPeak;}
    IEnumerator Record()
    {
        Directory.CreateDirectory(Path.Combine(output,"Frames"));
        Application.targetFrameRate=30;QualitySettings.vSyncCount=0;
        var audio=player.GetComponentInChildren<AudioListener>().gameObject.AddComponent<CombatFeelAudioCapture>();audio.Begin();
        int frame=0;float start=Time.realtimeSinceStartup;var pistol=session.weapons.Weapons.OfType<Pistol>().First();
        foreach(int wave in new[]{1,9})
        {
            Clear();yield return null;player.TeleportTo(home,Quaternion.identity);player.Heal(100);player.GetComponent<Shield>().Restore(100);
            while((session.Threat.Current?.Wave??0)<wave)session.Threat.BeginWave();
            var plan=session.Threat.Current;SquadManager.Instance.Configure(plan.AttackLimit,2);
            Set(GameManager.Instance,"<WavePlan>k__BackingField",plan);Set(GameManager.Instance,"<CurrentWave>k__BackingField",wave==1?1:3);
            session.ShowMessage("AMENAZA "+wave+" · "+(wave==1?"PATRULLA INICIAL":"VETERANOS · RÁFAGA Y CARGA"),14);
            for(int i=0;i<5;i++)
            {
                var enemy=Spawn(i>=3,6+i*.8f,true,wave==9&&(i==0||i==3));
                enemy.agent.Warp(new Vector3((i-2)*2.1f,0,-9+(i%2)*2));
                enemy.ConfigureDifficulty(plan.Health*(enemy.IsVeteran?1.2f:1),plan.Damage,plan.Accuracy);
                enemy.agent.speed=plan.Speed;enemy.Perception.SeedSearch(home);
            }
            float section=Time.realtimeSinceStartup,nextShot=section+3;bool screenshot=false;
            while(Time.realtimeSinceStartup-section<11)
            {
                float t=Time.realtimeSinceStartup-section;
                Set(GameManager.Instance,"remaining",fixtures.Count(e=>e!=null&&!e.IsDead));
                player.TeleportTo(home+Vector3.right*Mathf.Sin(t*.8f)*4-Vector3.forward*Mathf.Min(2.5f,t*.3f),Quaternion.identity);player.Heal(100);player.GetComponent<Shield>().Restore(50);
                var enemy=fixtures.Where(e=>e!=null&&!e.IsDead).OrderBy(e=>Vector3.Distance(e.transform.position,player.transform.position)).FirstOrDefault();
                Aim(t<1.5f?new Vector3(0,1.2f,-8):enemy!=null?enemy.AimPoint:new Vector3(0,1,-7));
                if(enemy!=null&&Time.realtimeSinceStartup>=nextShot){pistol.currentAmmo=pistol.ammo;pistol.Use();nextShot=Time.realtimeSinceStartup+.7f;}
                yield return null;Capture("Frames/frame-"+frame.ToString("0000")+".jpg",true);frame++;
                if(t>1.2f&&!screenshot){screenshot=true;Capture("wave-"+wave+".png");}
            }
        }
        audio.End(Path.Combine(output,"ai-audio.wav"));
        File.WriteAllText(Path.Combine(output,"recording.json"),JsonUtility.ToJson(new RecordingInfo{frames=frame,seconds=Time.realtimeSinceStartup-start,audioSeconds=audio.Seconds,audioPeak=audio.Peak},true));
        Check(frame>60,"Recorded early and veteran combat at 1080p");Clear();Destroy(audio);
    }
}
#endif
