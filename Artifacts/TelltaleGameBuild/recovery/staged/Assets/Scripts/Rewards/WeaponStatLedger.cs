using System.Collections.Generic;
using UnityEngine;

/// <summary>Runtime-only stat sources. Shop flats precede additive card percentages.</summary>
[DisallowMultipleComponent]
public class WeaponStatLedger : MonoBehaviour
{
    struct Entry { public WeaponStat stat;public float value;public bool card; }
    public struct Values
    {
        public float damage,rate,reload,insert,start,end,kick,hip,ads;
        public int capacity;
        public float Read(WeaponStat stat)
        {
            switch(stat) {
                case WeaponStat.Damage:return damage;case WeaponStat.FireRate:return rate;
                case WeaponStat.AmmoCapacity:return capacity;case WeaponStat.ReloadTime:return reload;
                case WeaponStat.RecoilKickUp:return kick;default:return hip;
            }
        }
    }
    readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>();
    Values baseline;
    BaseGun gun;BaseMelee blade;WeaponAction action;
    bool initialized;int legacy;
    public static WeaponStatLedger For(Component weapon)
    {
        var ledger=weapon.GetComponent<WeaponStatLedger>();
        if(ledger==null)ledger=weapon.gameObject.AddComponent<WeaponStatLedger>();
        ledger.Initialize();return ledger;
    }
    void Initialize()
    {
        if(initialized)return;initialized=true;
        gun=GetComponent<BaseGun>();blade=GetComponent<BaseMelee>();action=GetComponent<WeaponAction>();
        baseline=new Values{damage=gun!=null?gun.damage:blade.damage};
        if(gun==null)return;
        baseline.rate=gun.fireRate;baseline.capacity=gun.ammo;
        baseline.reload=action.reloadDuration;baseline.insert=action.shellInsert;baseline.start=action.reloadStart;baseline.end=action.reloadEnd;
        if(gun.Recoil!=null) { baseline.kick=gun.Recoil.recoilKickUp;baseline.hip=gun.Recoil.spreadHip;baseline.ads=gun.Recoil.spreadADS; }
    }
    public Values Current=>Evaluate(null,default);
    public Values Preview(WeaponStat stat,float value,bool card,string source=null)
    { Initialize();return Evaluate(source??"preview",new Entry{stat=stat,value=value,card=card}); }
    public void SetCard(string id,WeaponStat stat,float value)=>Set("card:"+id,stat,value,true);
    public void AddShop(WeaponStat stat,float value)=>Set("shop:"+(legacy++),stat,value,false);
    public void SetShop(string id,WeaponStat stat,float value)=>Set("shop:"+id,stat,value,false);
    void Set(string id,WeaponStat stat,float value,bool card)
    {
        Initialize();entries[id]=new Entry{stat=stat,value=value,card=card};Apply(Evaluate(null,default));
    }
    Values Evaluate(string replacement,Entry proposed)
    {
        Initialize();
        float damage=0,rate=0,capacity=0,reload=0,shopReload=1;
        var v=baseline;
        foreach(var pair in entries)if(pair.Key!=replacement)Accumulate(pair.Value,ref v,ref damage,ref rate,ref capacity,ref reload,ref shopReload);
        if(replacement!=null)Accumulate(proposed,ref v,ref damage,ref rate,ref capacity,ref reload,ref shopReload);
        v.damage=Mathf.Max(0,v.damage*(1+damage));v.rate=Mathf.Max(.01f,v.rate*(1+rate));
        v.capacity=Mathf.Max(1,Mathf.CeilToInt(v.capacity*(1+capacity)-.0001f));
        v.reload=Mathf.Max(.6f,baseline.reload*shopReload*(1-reload));
        v.insert=Mathf.Max(.2f,baseline.insert*shopReload*(1-reload));
        v.start=Mathf.Max(.05f,baseline.start*(1-reload));v.end=Mathf.Max(.05f,baseline.end*(1-reload));
        v.kick=Mathf.Max(0,v.kick);v.hip=Mathf.Max(.3f,v.hip);v.ads=Mathf.Max(.1f,v.ads);return v;
    }
    static void Accumulate(Entry e,ref Values v,ref float damage,ref float rate,ref float capacity,ref float reload,ref float shopReload)
    {
        if(e.card)
        {
            switch(e.stat) { case WeaponStat.Damage:damage+=e.value;break;case WeaponStat.FireRate:rate+=e.value;break;
                case WeaponStat.AmmoCapacity:capacity+=e.value;break;case WeaponStat.ReloadTime:reload+=e.value;break; }
            return;
        }
        switch(e.stat) {
            case WeaponStat.Damage:v.damage+=e.value;break;case WeaponStat.FireRate:v.rate+=e.value;break;
            case WeaponStat.AmmoCapacity:v.capacity+=(int)e.value;break;case WeaponStat.ReloadTime:shopReload*=Mathf.Clamp01(1-e.value);break;
            case WeaponStat.RecoilKickUp:v.kick-=e.value;break;case WeaponStat.Spread:v.hip-=e.value;v.ads-=e.value;break;
        }
    }
    void Apply(Values v)
    {
        if(gun==null){blade.damage=v.damage;return;}
        gun.damage=v.damage;gun.fireRate=v.rate;gun.ammo=v.capacity;gun.currentAmmo=Mathf.Min(gun.currentAmmo,gun.ammo);
        action.reloadDuration=v.reload;action.shellInsert=v.insert;action.reloadStart=v.start;action.reloadEnd=v.end;
        if(gun.Recoil!=null) { gun.Recoil.recoilKickUp=v.kick;gun.Recoil.spreadHip=v.hip;gun.Recoil.spreadADS=v.ads; }
        action.NotifyAmmo();
    }
}
