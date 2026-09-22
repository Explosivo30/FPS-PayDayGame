using System;
using UnityEngine;

public readonly struct CombatImpact
{
    public readonly int TargetId;
    public readonly Vector3 Point, Normal, Direction;
    public readonly float Damage;
    public readonly bool Fatal, Melee;
    public CombatImpact(int targetId, Vector3 point, Vector3 normal, Vector3 direction, float damage, bool fatal, bool melee)
    { TargetId=targetId;Point=point;Normal=normal;Direction=direction;Damage=damage;Fatal=fatal;Melee=melee; }
    public CombatImpact Merge(CombatImpact other)
    {
        float total=Damage+other.Damage;
        return new CombatImpact(TargetId,Vector3.Lerp(Point,other.Point,other.Damage/Mathf.Max(.01f,total)),
            Normal,Direction,total,Fatal||other.Fatal,Melee||other.Melee);
    }
}
public static class CombatFeedback
{
    public static event Action Hit;
    public static event Action<PlayerKill> PlayerEliminated;
    public static void ReportPlayerKill(NormalEnemyStateMachine enemy,Component weapon)
    {
        if(enemy!=null&&enemy.TryClaimPlayerKill())PlayerEliminated?.Invoke(new PlayerKill(enemy,weapon));
    }
    public static event Action Kill;
    public static event Action<CombatImpact> Impact;
    public static void ReportHit() { Hit?.Invoke(); }
    public static void ReportKill() { Kill?.Invoke(); }
    public static void ReportImpact(CombatImpact impact) { Impact?.Invoke(impact); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { Hit=null;Kill=null;Impact=null;PlayerEliminated=null; }
}
