#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class CombatFeelRecording : MonoBehaviour
{
    [Serializable] class RecordingInfo { public int frames;public float seconds;public float audioSeconds;public float audioPeak; }
    public IEnumerator Record(PlayerStateMachine player,GunController weapons,Camera view,RenderTexture target,GameObject prefab,string output)
    {
        Directory.CreateDirectory(Path.Combine(output,"Frames"));
        var listener=player.GetComponentInChildren<AudioListener>();
        var audio=listener.gameObject.AddComponent<CombatFeelAudioCapture>();audio.Begin();
        int previousRate=Application.targetFrameRate,previousSync=QualitySettings.vSyncCount;
        Application.targetFrameRate=30;QualitySettings.vSyncCount=0;
        int frame=0;float started=Time.realtimeSinceStartup;
        string[] labels={"PISTOLA / IMPACTO SECO","CARABINA / RÁFAGA CONTROLADA","ESCOPETA / IMPACTO PESADO","CUCHILLO / CONTACTO"};
        for(int index=0;index<4;index++)
        {
            player.TeleportTo(new Vector3(0,1.05f,-16),Quaternion.identity);
            float distance=index==3?1.35f:index==2?4.2f:6.5f;
            var go=UnityEngine.Object.Instantiate(prefab,new Vector3(0,0,-16+distance),Quaternion.Euler(0,180,0));
            var enemy=go.GetComponent<NormalEnemyStateMachine>();enemy.enabled=false;
            enemy.ConfigureDifficulty(index==0?120:index==1?180:index==2?160:100,8,.5f);
            if(enemy.agent.isOnNavMesh)enemy.agent.isStopped=true;
            weapons.EquipImmediate(index);
            if(weapons.currentWeapon is BaseGun gun) { gun.Recoil.spreadHip=gun.Recoil.spreadADS=0;gun.currentAmmo=gun.ammo; }
            TowerSession.Instance.ShowMessage(labels[index],8);
            float segmentStart=Time.realtimeSinceStartup;float nextShot=.65f;
            while(Time.realtimeSinceStartup-segmentStart<3.8f)
            {
                Vector3 point=new Vector3(0,.9f,-16+distance);
                var look=Quaternion.LookRotation(point-view.transform.position);
                player.transform.rotation=Quaternion.Euler(0,look.eulerAngles.y,0);
                player.headCam.localRotation=Quaternion.Euler(look.eulerAngles.x,0,0);
                Physics.SyncTransforms();
                float elapsed=Time.realtimeSinceStartup-segmentStart;
                if(elapsed>=nextShot&&enemy!=null&&!enemy.IsDead)
                {
                    weapons.currentWeapon.Use();
                    nextShot+=index==0?.28f:index==1?.11f:index==2?.86f:.78f;
                }
                yield return null;
                var previous=RenderTexture.active;RenderTexture.active=target;
                var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
                File.WriteAllBytes(Path.Combine(output,"Frames","frame-"+frame.ToString("0000")+".jpg"),image.EncodeToJPG(88));
                UnityEngine.Object.Destroy(image);RenderTexture.active=previous;frame++;
            }
            if(go!=null)UnityEngine.Object.Destroy(go);
        }
        float elapsedTotal=Time.realtimeSinceStartup-started;
        audio.End(Path.Combine(output,"combat-audio.wav"));
        File.WriteAllText(Path.Combine(output,"recording.json"),JsonUtility.ToJson(new RecordingInfo{
            frames=frame,seconds=elapsedTotal,audioSeconds=audio.Seconds,audioPeak=audio.Peak},true));
        Application.targetFrameRate=previousRate;QualitySettings.vSyncCount=previousSync;
        UnityEngine.Object.Destroy(audio);
    }
}
public class CombatFeelAudioCapture : MonoBehaviour
{
    readonly List<float> samples=new List<float>(2000000);
    readonly object gate=new object();
    volatile bool recording;
    int channels=2,rate;
    public float Seconds { get; private set; }
    public float Peak { get; private set; }
    public void Begin() { rate=AudioSettings.outputSampleRate;recording=true; }
    void OnAudioFilterRead(float[] data,int count)
    {
        if(!recording)return;
        lock(gate) { channels=count;samples.AddRange(data); }
    }
    public void End(string path)
    {
        recording=false;float[] data;lock(gate)data=samples.ToArray();
        foreach(float value in data)Peak=Mathf.Max(Peak,Mathf.Abs(value));
        Seconds=data.Length/(float)(Mathf.Max(1,rate)*channels);
        using(var writer=new BinaryWriter(File.Create(path)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+data.Length*2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);
            writer.Write((short)channels);writer.Write(rate);writer.Write(rate*channels*2);
            writer.Write((short)(channels*2));writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(data.Length*2);
            float gain=Peak>1?.95f/Peak:1;
            foreach(float value in data)writer.Write((short)(Mathf.Clamp(value*gain,-1,1)*32767));
        }
    }
}
#endif
