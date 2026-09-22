using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class RewardBuild
{
    const string Folder="Assets/Art/Rewards";
    [MenuItem("Tools/HELIX/Configure reward cards")]
    public static void Configure()
    {
        Directory.CreateDirectory(Folder);Directory.CreateDirectory("Artifacts/Rewards");AssetDatabase.Refresh();
        var icons=new Dictionary<string,Sprite>();
        foreach(string icon in new[]{"damage","rate","capacity","reload","health","shield","repair","supply","killshield","autoload","chips"})icons[icon]=Icon(icon);
        var cards=new List<RewardCard>();
        foreach(string weapon in new[]{"Pistol","MachineGun","ShotGun","Knife"})
        {
            cards.Add(Card(weapon+"_damage","Potencia",weapon,RewardKind.WeaponStat,RewardGroup.Offense,WeaponStat.Damage,.2f,0,3,false,"Golpes más fuertes para esta arma.",icons["damage"]));
            if(weapon=="Knife")continue;
            cards.Add(Card(weapon+"_rate","Gatillo acelerado",weapon,RewardKind.WeaponStat,RewardGroup.Offense,WeaponStat.FireRate,.15f,0,3,false,"Menos espera entre disparos.",icons["rate"]));
            cards.Add(Card(weapon+"_capacity","Cargador ampliado",weapon,RewardKind.WeaponStat,RewardGroup.Utility,WeaponStat.AmmoCapacity,.25f,0,3,false,"Más capacidad. La munición se obtiene por separado.",icons["capacity"]));
            cards.Add(Card(weapon+"_reload","Recarga ágil",weapon,RewardKind.WeaponStat,RewardGroup.Utility,WeaponStat.ReloadTime,.12f,0,3,false,"Vuelve antes al combate después de recargar.",icons["reload"]));
            cards.Add(Card(weapon+"_supply","Reserva de combate",weapon,RewardKind.Supply,RewardGroup.Utility,0,2,0,0,false,"Dos cargadores de reserva, hasta el máximo del arma.",icons["supply"]));
        }
        cards.Add(Card("health","Blindaje","",RewardKind.Health,RewardGroup.Survival,0,20,0,3,false,"Aumenta tu salud máxima y recupera 20 de salud.",icons["health"]));
        cards.Add(Card("shield","Condensador","",RewardKind.Shield,RewardGroup.Survival,0,15,0,3,false,"Aumenta tu escudo máximo y recupera 15 de escudo.",icons["shield"]));
        cards.Add(Card("repair","Reparación","",RewardKind.Repair,RewardGroup.Survival,0,40,25,0,false,"Restaura salud y escudo al instante.",icons["repair"]));
        cards.Add(Card("killshield","Recuperador","",RewardKind.KillShield,RewardGroup.Survival,0,5,0,1,true,"Cada baja que causes recupera 5 de escudo, hasta tu máximo.",icons["killshield"]));
        cards.Add(Card("autoload","Autocargador","",RewardKind.AutoLoader,RewardGroup.Utility,0,.1f,0,1,true,"Cada baja con un arma de fuego transfiere un 10% del cargador desde su reserva.",icons["autoload"]));
        var fallback=Card("chips","Fondos de apoyo","",RewardKind.Chips,RewardGroup.Utility,0,40,0,0,false,"Recursos para tu siguiente compra.",icons["chips"]);
        var setup=EditorSceneManager.GetSceneManagerSetup();
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/TowerCore.unity");
        var session=Object.FindFirstObjectByType<TowerSession>();
        var rewards=session.GetComponent<RunRewards>();if(rewards==null)rewards=session.gameObject.AddComponent<RunRewards>();
        rewards.catalog=cards.ToArray();rewards.chipsFallback=fallback;
        var view=session.GetComponent<RewardCardView>();if(view==null)view=session.gameObject.AddComponent<RewardCardView>();
        view.revealSound=Audio("Reveal",.36f,0);view.selectSound=Audio("Select",.075f,1);view.confirmSound=Audio("Confirm",.42f,2);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        if(setup.Any(s=>s.isLoaded))EditorSceneManager.RestoreSceneManagerSetup(setup);
        Debug.Log("REWARD CONFIGURATION COMPLETE: "+cards.Count+" cards");
    }
    static RewardCard Card(string id,string title,string weapon,RewardKind kind,RewardGroup group,WeaponStat stat,float value,float secondary,int max,bool special,string description,Sprite icon)
    {
        string path=Folder+"/"+id+".asset";var card=AssetDatabase.LoadAssetAtPath<RewardCard>(path);
        if(card==null){card=ScriptableObject.CreateInstance<RewardCard>();AssetDatabase.CreateAsset(card,path);}
        card.id=id;card.title=title;card.weaponId=weapon;card.kind=kind;card.group=group;card.stat=stat;
        card.value=value;card.secondaryValue=secondary;card.maxLevel=max;card.special=special;card.description=description;card.icon=icon;
        EditorUtility.SetDirty(card);return card;
    }
    static Sprite Icon(string kind)
    {
        const int size=128;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);var pixels=new Color[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            Vector2 p=new Vector2((x-64)/48f,(y-64)/48f);bool on=false;
            bool shield=Mathf.Abs(p.x)<.68f&&p.y<.78f&&p.y>-.85f+Mathf.Abs(p.x)*.75f;
            bool cross=(Mathf.Abs(p.x)<.19f&&Mathf.Abs(p.y)<.72f)||(Mathf.Abs(p.y)<.19f&&Mathf.Abs(p.x)<.72f);
            if(kind=="damage")on=(Mathf.Abs(p.x)<.25f&&p.y>-.7f&&p.y<.3f)||(p.y>=.3f&&p.y<.8f&&Mathf.Abs(p.x)<(.8f-p.y)*.5f);
            if(kind=="capacity"||kind=="supply")
                for(int n=-1;n<=1;n++)on|=Mathf.Abs(p.x-n*.52f)<.17f&&p.y>-.65f&&p.y<.65f-Mathf.Abs(p.x-n*.52f);
            if(kind=="rate"||kind=="autoload")on=(p.y>.0f&&p.y<.82f&&p.x>-.38f&&p.x<.38f-p.y*.25f)||(p.y<.18f&&p.y>-.85f&&p.x<.4f&&p.x>-.1f-p.y*.4f);
            if(kind=="autoload")on|=p.magnitude>.82f&&p.magnitude<.96f;
            if(kind=="health"||kind=="repair")on=cross;
            if(kind=="shield"||kind=="killshield")on=shield&&(! (Mathf.Abs(p.x)<.43f&&p.y<.52f&&p.y>-.53f+Mathf.Abs(p.x)*.75f));
            if(kind=="killshield")on|=(Mathf.Abs(p.x)<.09f&&Mathf.Abs(p.y)<.29f)||(Mathf.Abs(p.y)<.09f&&Mathf.Abs(p.x)<.29f);
            if(kind=="reload")on=(p.magnitude>.49f&&p.magnitude<.72f&&!(p.x>.3f&&p.y>.25f))||(p.x>.27f&&p.x<.8f&&p.y>.10f&&p.y<.10f+(p.x-.27f));
            if(kind=="chips")on=Mathf.Abs(p.x)<.62f&&Mathf.Abs(p.y)<.62f&&!(Mathf.Abs(p.x)<.35f&&Mathf.Abs(p.y)<.35f);
            pixels[y*size+x]=on?Color.white:Color.clear;
        }
        texture.SetPixels(pixels);texture.Apply();string path=Folder+"/Icon-"+kind+".png";File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=128;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static AudioClip Audio(string name,float seconds,int kind)
    {
        const int rate=44100;int count=Mathf.CeilToInt(seconds*rate);
        string path=Folder+"/"+name+".wav";
        using(var w=new BinaryWriter(File.Create(path)))
        {
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+count*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(count*2);
            for(int i=0;i<count;i++)
            {
                float t=i/(float)rate;float freq=kind==1?980:kind==2?660:330;
                float sample=Mathf.Sin(2*Mathf.PI*freq*t)*Mathf.Exp(-t*(kind==1?55:15));
                if(kind!=1&&t>.075f)sample+=Mathf.Sin(2*Mathf.PI*freq*1.5f*(t-.075f))*Mathf.Exp(-(t-.075f)*18)*.65f;
                sample*=Mathf.Min(1,t*900)*Mathf.Min(1,(seconds-t)*150)*.45f;
                w.Write((short)(Mathf.Clamp(sample,-1,1)*32767));
            }
        }
        AssetDatabase.ImportAsset(path);return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
    public static void ConfigureAndQuit(){Configure();EditorApplication.Exit(0);}
    public static void Play(){TowerValidation.Play();}
    public static void ConfigureAndPlay(){Configure();Play();}
    public static void Build(){TowerValidation.Build();}
}
