using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class RunProgression : MonoBehaviour
{
    [Serializable] public class SaveData
    {
        public int version=1,cores,bestFloor;
        public List<string> unlocked=new List<string>(),discoveries=new List<string>(),awarded=new List<string>();
    }
    public SaveData Data { get; private set; }=new SaveData();
    public int EarnedThisRun { get; private set; }
    public string SavePath { get; private set; }
    public string SaveError { get; private set; }
    public static readonly string[] RecipeIds={"carbine_stability","shotgun_focus","quick_reload"};
    public static readonly string[] RecipeNames={"Estabilizador de carabina","Choke de escopeta","Recarga optimizada"};
    public static readonly int[] RecipeCosts={2,2,3};
    public void Awake()
    {
        SavePath=Path.Combine(Application.persistentDataPath,"player-progress-v1.json");
        var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-player-profile");
        if(index>=0&&index+1<args.Length)SavePath=Path.GetFullPath(args[index+1]);
        Load();
    }
    public void Load()
    {
        SaveError=null;
        if(!File.Exists(SavePath))return;
        try
        {
            var data=JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
            if(data==null||data.version!=1||data.cores<0)throw new IOException("Formato de progreso no válido.");
            data.unlocked=data.unlocked??new List<string>();data.discoveries=data.discoveries??new List<string>();
            data.awarded=data.awarded??new List<string>();Data=data;
        }
        catch(Exception e){SaveError=e.Message;}
    }
    bool Save()
    {
        // Never overwrite a profile that could not be read.
        if(SaveError!=null)return false;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
            string temporary=SavePath+".tmp";File.WriteAllText(temporary,JsonUtility.ToJson(Data,true));
            if(File.Exists(SavePath))File.Replace(temporary,SavePath,null);else File.Move(temporary,SavePath);
            return true;
        }
        catch(Exception e){SaveError=e.Message;return false;}
    }
    public bool IsUnlocked(string id)=>string.IsNullOrEmpty(id)||Data.unlocked.Contains(id);
    public bool BankFloor(string token,string discovery,int floor)
    {
        if(Data.awarded.Contains(token)||SaveError!=null)return false;
        var previous=JsonUtility.ToJson(Data);
        Data.cores++;Data.bestFloor=Mathf.Max(Data.bestFloor,floor);Data.awarded.Add(token);
        if(!Data.discoveries.Contains(discovery))Data.discoveries.Add(discovery);
        if(Data.awarded.Count>128)Data.awarded.RemoveAt(0);
        if(!Save()){Data=JsonUtility.FromJson<SaveData>(previous);return false;}
        EarnedThisRun++;return true;
    }
    public bool TryUnlock(int index)
    {
        if(index<0||index>=RecipeIds.Length||Data.cores<RecipeCosts[index]||IsUnlocked(RecipeIds[index])||SaveError!=null)return false;
        Data.cores-=RecipeCosts[index];Data.unlocked.Add(RecipeIds[index]);
        if(Save())return true;
        Data.cores+=RecipeCosts[index];Data.unlocked.Remove(RecipeIds[index]);return false;
    }
}
