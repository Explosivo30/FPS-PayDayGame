using System;
using UnityEngine;
public static class CombatFeedback
{
    public static event Action Hit;
    public static event Action Kill;
    public static void ReportHit() { Hit?.Invoke(); }
    public static void ReportKill() { Kill?.Invoke(); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { Hit = null; Kill = null; }
}
