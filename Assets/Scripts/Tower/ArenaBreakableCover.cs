using System;
using UnityEngine;

/// <summary>A prepared, visit-local sight screen. Its structural plinth never changes.</summary>
[DisallowMultipleComponent]
public sealed class ArenaBreakableCover : MonoBehaviour, IRaycastHitHandler
{
    [Min(1)] public float structuralHealth = 60f;
    public GameObject intactObject, damageIndicator;
    public Transform[] fragments = new Transform[0];
    [Min(.1f)] public float fragmentLifetime = 2.5f;
    public const int MaximumActiveFragments = 24;
    public static int AliveFragmentCount { get; private set; }
    public bool IsBroken { get; private set; }
    public float CurrentHealth { get; private set; }
    public float StructuralHealth => structuralHealth;
    public int ActiveFragmentCount { get; private set; }
    public int StructuralImpacts { get; private set; }
    public event Action<ArenaBreakableCover> Broken;

    Vector3[] localPositions, localScales, velocities;
    Quaternion[] localRotations;
    float[] lifetimes;
    bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { AliveFragmentCount = 0; }

    void Awake() { Initialize(); ResetForVisit(); }
    void Initialize()
    {
        if (initialized && localPositions.Length == fragments.Length) return;
        if (initialized) ClearFragments();
        initialized = true;
        int count = fragments.Length;
        localPositions = new Vector3[count]; localScales = new Vector3[count];
        localRotations = new Quaternion[count]; velocities = new Vector3[count]; lifetimes = new float[count];
        for (int i = 0; i < count; i++)
        {
            if (fragments[i] == null) continue;
            localPositions[i] = fragments[i].localPosition; localScales[i] = fragments[i].localScale;
            localRotations[i] = fragments[i].localRotation; fragments[i].gameObject.SetActive(false);
        }
    }

    // Deliberately not IDamageable: BaseGun calls IDamageable and then IRaycastHitHandler.
    // Using only the latter prevents a single bullet reducing structural health twice.
    public void TakeDamage(float amount) { ApplyStructuralImpact(amount, transform.position, transform.forward); }
    public void HandleRaycastHit(RaycastHit hit, float damage) { ApplyStructuralImpact(damage, hit.point, -hit.normal); }
    public bool ApplyStructuralImpact(float amount, Vector3 point, Vector3 direction)
    {
        if (!isActiveAndEnabled || WeaponAction.CombatPaused || IsBroken || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return false;
        StructuralImpacts++;
        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        if (damageIndicator != null) damageIndicator.SetActive(CurrentHealth < structuralHealth);
        if (CurrentHealth > 0) return true;
        IsBroken = true;
        if (intactObject != null) intactObject.SetActive(false); // collider disappears synchronously
        if (damageIndicator != null) damageIndicator.SetActive(false);
        ReleaseFragments(direction);
        Broken?.Invoke(this);
        return true;
    }
    public bool TryBreakFromCharge(Vector3 point, Vector3 direction)
    {
        return !IsBroken && ApplyStructuralImpact(Mathf.Max(CurrentHealth, structuralHealth), point, direction);
    }
    public void ResetForVisit()
    {
        Initialize(); ClearFragments(); IsBroken = false; CurrentHealth = Mathf.Max(1, structuralHealth); StructuralImpacts = 0;
        if (intactObject != null) intactObject.SetActive(true);
        if (damageIndicator != null) damageIndicator.SetActive(false);
    }
    void ReleaseFragments(Vector3 direction)
    {
        Initialize();
        direction = direction.sqrMagnitude > .001f ? direction.normalized : transform.forward;
        for (int i = 0; i < fragments.Length && AliveFragmentCount < MaximumActiveFragments; i++)
        {
            var part = fragments[i]; if (part == null) continue;
            part.localPosition = localPositions[i]; part.localRotation = localRotations[i]; part.localScale = localScales[i];
            // Prepared cosmetic pieces use ballistic motion and ground raycasts, not actor colliders.
            // They cannot become navigation obstacles, hit targets, loot, or push a character.
            velocities[i] = direction * (2f + (i % 3) * .45f) + Vector3.up * (1.5f + (i % 4) * .4f)
                + transform.right * (Mathf.Sin(i * 2.4f) * 1.4f);
            lifetimes[i] = fragmentLifetime; part.gameObject.SetActive(true);
            ActiveFragmentCount++; AliveFragmentCount++;
        }
    }
    void Update() { Tick(Time.deltaTime); }
    public void Tick(float seconds)
    {
        if (WeaponAction.CombatPaused || ActiveFragmentCount == 0 || seconds <= 0) return;
        for (int i = 0; i < fragments.Length; i++)
        {
            if (lifetimes[i] <= 0) continue;
            if (fragments[i] == null) { Retire(i); continue; }
            lifetimes[i] -= seconds;
            if (lifetimes[i] <= 0) { Retire(i); continue; }
            var part = fragments[i];
            velocities[i] += Physics.gravity * seconds;
            Vector3 next = part.position + velocities[i] * seconds;
            if (Physics.Linecast(part.position, next, out var ground, 1 << 3, QueryTriggerInteraction.Ignore))
            { next = ground.point + ground.normal * .06f; velocities[i] = Vector3.zero; }
            part.position = next;
            part.Rotate(new Vector3(70 + i * 7, 110, 30) * seconds, Space.Self);
            part.localScale = localScales[i] * Mathf.Clamp01(lifetimes[i] / .45f);
        }
    }
    void Retire(int index)
    {
        lifetimes[index] = 0;
        if (fragments[index] != null) fragments[index].gameObject.SetActive(false);
        ActiveFragmentCount = Mathf.Max(0, ActiveFragmentCount - 1); AliveFragmentCount = Mathf.Max(0, AliveFragmentCount - 1);
    }
    void ClearFragments()
    {
        if (lifetimes == null) return;
        for (int i = 0; i < lifetimes.Length; i++) if (lifetimes[i] > 0) Retire(i);
    }
    void OnDisable() { ClearFragments(); }
}
