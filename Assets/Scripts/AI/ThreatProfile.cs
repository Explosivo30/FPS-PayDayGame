using UnityEngine;

public enum WaveComposition { Balanced, Assault, Support }

[CreateAssetMenu(menuName="HELIX/Threat profile")]
public sealed class ThreatProfile : ScriptableObject
{
    [Header("Global progression")]
    public int initialCount=5,perWave=2,maximumCount=25,maximumAlive=12;
    public float healthPerWave=.06f,damagePerWave=.04f;
    public float baseSpeed=3.4f,speedPerWave=.08f,maximumRangedSpeed=4.4f,maximumMeleeSpeed=4.6f;
    public float baseAccuracy=.575f,accuracyPerWave=.015f,maximumAccuracy=.75f;
    [Header("Coordination")]
    public int firstAttackLimit=2,middleAttackLimit=3,lateAttackLimit=4,meleeAttackLimit=2;
    public float slotDistance=1.5f;
    [Header("Perception and navigation")]
    public float memorySeconds=3,hearingRange=24,decisionInterval=.25f;
    public float preferredMinimum=10,preferredMaximum=16,retreatDistance=6;
    [Header("Presentation and veterans")]
    public float spawnWarningSeconds=1,veteranHealth=1.2f;
    public float burstWarning=.45f,burstInterval=.22f;
    public float chargeWarning=.8f,chargeDistance=1.2f,chargeRecovery=1;
    public float minimumRangedWarning=.25f,minimumMeleeWarning=.62f,minimumMeleeRecovery=.85f;
    static ThreatProfile fallback;
    public static ThreatProfile Basic
    {
        get {if(fallback==null){fallback=CreateInstance<ThreatProfile>();fallback.hideFlags=HideFlags.HideAndDontSave;}return fallback;}
    }
}

/// <summary>Immutable snapshot, created once for a global wave.</summary>
public sealed class WaveThreat
{
    public readonly int Wave,Count,MeleeCount,Veterans,AttackLimit;
    public readonly float Health,Damage,Speed,Accuracy;
    public readonly WaveComposition Composition;
    public WaveThreat(ThreatProfile p,int wave,float health,float damage)
    {
        Wave=Mathf.Max(1,wave);Count=Mathf.Clamp(p.initialCount+p.perWave*(Wave-1),1,p.maximumCount);
        Composition=(WaveComposition)((Wave-1)%3);
        float share=Composition==WaveComposition.Assault?.6f:Composition==WaveComposition.Support?.25f:.4f;
        MeleeCount=Mathf.Clamp(Mathf.RoundToInt(Count*share),Count>1?1:0,Mathf.Max(0,Count-1));
        Veterans=Wave>=9?2:Wave>=6?1:0;
        AttackLimit=Wave>=6?p.lateAttackLimit:Wave>=3?p.middleAttackLimit:p.firstAttackLimit;
        Health=health*(1+p.healthPerWave*(Wave-1));Damage=damage*(1+p.damagePerWave*(Wave-1));
        Speed=Mathf.Min(p.maximumRangedSpeed,p.baseSpeed+p.speedPerWave*(Wave-1));
        Accuracy=Mathf.Min(p.maximumAccuracy,p.baseAccuracy+p.accuracyPerWave*(Wave-1));
    }
    public bool IsMelee(int index)=>(index+1)*MeleeCount/Count>index*MeleeCount/Count;
    public bool IsVeteran(int index)
    {
        if(Veterans==0)return false;
        // First veteran alternates roles by wave; two veterans always cover both roles.
        bool role=Veterans==1?Wave%2!=0:IsMelee(index);
        if(IsMelee(index)!=role)return false;
        for(int i=0;i<index;i++)if(IsMelee(i)==role)return false;
        return true;
    }
    public string Announcement=>Wave==6?"VETERANO DETECTADO":Wave>=9?"DOS VETERANOS ENTRANDO":
        Wave>=3?"ATAQUE DESDE DOS FLANCOS":Composition==WaveComposition.Assault?"GRUPO DE ASALTO":"PATRULLA DE CONTENCIÓN";
}
