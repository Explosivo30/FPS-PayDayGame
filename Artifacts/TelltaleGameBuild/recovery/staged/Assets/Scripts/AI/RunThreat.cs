using UnityEngine;
[DisallowMultipleComponent]
public sealed class RunThreat : MonoBehaviour
{
    public ThreatProfile profile;
    public WaveThreat Current { get; private set; }
    public float BaseHealth { get; private set; }
    public float BaseDamage { get; private set; }
    bool initialized;
    public ThreatProfile Profile=>profile!=null?profile:ThreatProfile.Basic;
    public void EnterFloor(TowerFloor floor)
    {
        if(initialized)return;
        initialized=true;BaseHealth=floor.enemyHealth;BaseDamage=floor.enemyDamage;
    }
    public WaveThreat BeginWave()
    {
        Current=new WaveThreat(Profile,(Current?.Wave??0)+1,BaseHealth,BaseDamage);
        return Current;
    }
}
