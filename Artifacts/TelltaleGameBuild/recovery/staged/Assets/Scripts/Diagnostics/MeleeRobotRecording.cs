#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>Opt-in, scripted in-engine capture of approach, dodge and elimination; never runs in normal play.</summary>
public class MeleeRobotRecording : MonoBehaviour
{
    [System.Serializable] class Info { public int frames;public float seconds,audioSeconds,audioPeak; }
    public IEnumerator Run(PlayerStateMachine player,GameObject prefab,Camera view,RenderTexture target,string output)
    {
        Directory.CreateDirectory(Path.Combine(output,"Frames"));Time.captureFramerate=0;
        Application.targetFrameRate=30;QualitySettings.vSyncCount=0;
        var audio=player.GetComponentInChildren<AudioListener>().gameObject.AddComponent<CombatFeelAudioCapture>();audio.Begin();
        player.TeleportTo(new Vector3(0,1.05f,-16),Quaternion.identity);
        var enemy=Instantiate(prefab,new Vector3(0,0,-9),Quaternion.Euler(0,180,0)).GetComponent<NormalEnemyStateMachine>();
        var combat=enemy.GetComponent<MeleeRobotCombat>();
        var weapons=TowerSession.Instance.weapons;weapons.EquipImmediate(0);
        var gun=weapons.currentWeapon as BaseGun;gun.Recoil.spreadHip=gun.Recoil.spreadADS=0;
        int frame=0;bool dodged=false;float missAt=-1,nextShot=0;float began=Time.realtimeSinceStartup;
        while(Time.realtimeSinceStartup-began<10)
        {
            if(combat.Phase==MeleeRobotPhase.Windup&&combat.PhaseProgress>.32f&&!dodged)
            { dodged=true;player.TeleportTo(new Vector3(2,1.05f,-16),Quaternion.identity); }
            if(dodged&&combat.Phase==MeleeRobotPhase.Recover&&missAt<0)missAt=Time.time;
            Vector3 point=enemy.transform.position+Vector3.up*1.1f;
            var look=Quaternion.LookRotation(point-view.transform.position);
            player.transform.rotation=Quaternion.Euler(0,look.eulerAngles.y,0);player.headCam.localRotation=Quaternion.Euler(look.eulerAngles.x,0,0);
            Physics.SyncTransforms();
            TowerSession.Instance.ShowMessage(!dodged?"ROBOT DE SEGURIDAD · PERSECUCIÓN":missAt<0?"PUÑETAZO · ESQUIVA LATERAL":"RECUPERACIÓN · CONTRAATAQUE",2);
            if(missAt>0&&Time.time-missAt>.3f&&!enemy.IsDead&&Time.time>=nextShot)
            { gun.Use();nextShot=Time.time+.3f; }
            yield return null;
            var previous=RenderTexture.active;RenderTexture.active=target;
            var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
            File.WriteAllBytes(Path.Combine(output,"Frames","frame-"+frame.ToString("0000")+".jpg"),image.EncodeToJPG(88));
            Destroy(image);RenderTexture.active=previous;frame++;
            if(enemy==null)break;
        }
        audio.End(Path.Combine(output,"melee-audio.wav"));
        File.WriteAllText(Path.Combine(output,"recording.json"),JsonUtility.ToJson(new Info{frames=frame,seconds=Time.realtimeSinceStartup-began,audioSeconds=audio.Seconds,audioPeak=audio.Peak},true));
        Time.captureFramerate=0;Destroy(audio);
    }
}
#endif
