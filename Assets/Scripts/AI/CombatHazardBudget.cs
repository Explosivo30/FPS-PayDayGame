using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Persistent salvos and major attacks share a small admission budget.</summary>
public static class CombatHazardBudget
{
    public const int MaximumSalvos = 2, MaximumPrimary = 1;
    enum Kind { Salvo, Primary }
    static readonly List<Lease> leases = new List<Lease>();
    public static int ActiveSalvos { get { Prune(); return Count(Kind.Salvo); } }
    public static int ActivePrimary { get { Prune(); return Count(Kind.Primary); } }

    public sealed class Lease : IDisposable
    {
        readonly UnityEngine.Object owner;
        readonly Kind kind;
        bool released;
        public bool IsValid => !released && owner != null;
        public UnityEngine.Object Owner => owner;
        internal Lease(UnityEngine.Object value, bool primary) { owner = value; kind = primary ? Kind.Primary : Kind.Salvo; }
        internal bool IsPrimary => kind == Kind.Primary;
        public void Dispose() { if (released) return; released = true; leases.Remove(this); }
        internal void Invalidate() { released = true; }
    }

    public static bool TryReserveSalvo(UnityEngine.Object owner, out Lease lease) => TryReserve(owner, false, out lease);
    public static bool TryReservePrimary(UnityEngine.Object owner, out Lease lease) => TryReserve(owner, true, out lease);
    static bool TryReserve(UnityEngine.Object owner, bool primary, out Lease lease)
    {
        lease = null;
        if (owner == null || WeaponAction.CombatPaused) return false;
        Prune();
        var kind = primary ? Kind.Primary : Kind.Salvo;
        if (Count(kind) >= (primary ? MaximumPrimary : MaximumSalvos)) return false;
        lease = new Lease(owner, primary); leases.Add(lease); return true;
    }
    static int Count(Kind kind)
    {
        int count = 0;
        foreach (var lease in leases) if (lease.IsPrimary == (kind == Kind.Primary)) count++;
        return count;
    }
    static void Prune()
    {
        for (int i = leases.Count - 1; i >= 0; i--)
            if (!leases[i].IsValid) { leases[i].Invalidate(); leases.RemoveAt(i); }
    }
    public static void ResetAll()
    {
        foreach (var lease in leases) lease.Invalidate();
        leases.Clear();
    }
    // An additive floor can unload after the next one has entered. Preserve its reservations.
    static void SceneUnloaded(Scene scene) { Prune(); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Initialize()
    {
        ResetAll();
        SceneManager.sceneUnloaded -= SceneUnloaded;
        SceneManager.sceneUnloaded += SceneUnloaded;
    }
}
