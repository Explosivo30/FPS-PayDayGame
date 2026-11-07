using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Enemy bullets travel in world space; a swept sphere resolves one impact.</summary>
public sealed class EnemyProjectile : MonoBehaviour
{
    public sealed class Volley
    {
        readonly CombatHazardBudget.Lease lease;
        int projectiles;
        bool emissionFinished, cancelled;
        public int ActiveProjectiles => projectiles;
        public bool IsValid => !cancelled && lease != null && lease.IsValid;
        public bool IsReleased => lease == null || !lease.IsValid;
        public Volley(CombatHazardBudget.Lease reservation) { lease = reservation; }
        internal void Add() { projectiles++; }
        internal void Complete() { projectiles = Mathf.Max(0, projectiles - 1); ReleaseIfFinished(); }
        public void FinishEmission() { emissionFinished = true; ReleaseIfFinished(); }
        public void Cancel() { cancelled = emissionFinished = true; lease?.Dispose(); }
        void ReleaseIfFinished() { if (emissionFinished && projectiles == 0) lease?.Dispose(); }
    }

    static readonly Stack<EnemyProjectile> pool = new Stack<EnemyProjectile>();
    static readonly HashSet<EnemyProjectile> active = new HashSet<EnemyProjectile>();
    static readonly List<EnemyProjectile> scratch = new List<EnemyProjectile>();
    static readonly RaycastHit[] sweepHits = new RaycastHit[64];
    static readonly Collider[] overlaps = new Collider[64];
    static Transform poolRoot;
    static Material fallbackMaterial;
    Renderer body;
    MaterialPropertyBlock properties;
    Volley volley;
    float damage, radius;
    int hitMask;
    Vector3 shotOrigin;
    public NormalEnemyStateMachine Owner { get; private set; }
    public Vector3 Velocity { get; private set; }
    public float RemainingLifetime { get; private set; }
    public float Travelled { get; private set; }
    public bool IsLive { get; private set; }
    public static int ActiveCount => active.Count;
    public static int PooledCount => pool.Count;

    public static EnemyProjectile Launch(NormalEnemyStateMachine source, Vector3 position, Vector3 direction,
        float speed, float hitDamage, float lifetime, float collisionRadius, int collisionMask,
        Volley salvo = null, Material material = null)
    {
        if (source == null || source.IsDead || direction.sqrMagnitude < .0001f || speed <= 0 || lifetime <= 0
            || (salvo != null && !salvo.IsValid)) return null;
        EnsurePool();
        EnemyProjectile bullet = null;
        while (pool.Count > 0 && bullet == null) bullet = pool.Pop();
        if (bullet == null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Centinela projectile"; go.layer = 2; go.transform.SetParent(poolRoot, false);
            var collider = go.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            bullet = go.AddComponent<EnemyProjectile>(); bullet.body = go.GetComponent<Renderer>();
            bullet.properties = new MaterialPropertyBlock();
            bullet.body.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bullet.body.receiveShadows = false;
        }
        bullet.Owner = source; bullet.shotOrigin = position;
        bullet.Velocity = direction.normalized * speed; bullet.damage = Mathf.Max(0, hitDamage);
        bullet.RemainingLifetime = lifetime; bullet.radius = Mathf.Max(.025f, collisionRadius);
        bullet.hitMask = collisionMask; bullet.Travelled = 0; bullet.volley = salvo;
        bullet.transform.position = position; bullet.transform.localScale = Vector3.one * bullet.radius * 2;
        bullet.body.sharedMaterial = material != null ? material : fallbackMaterial;
        bullet.properties.Clear();
        bullet.properties.SetColor("_BaseColor", new Color(1, .18f, .025f));
        bullet.properties.SetColor("_Color", new Color(1, .18f, .025f));
        bullet.properties.SetColor("_EmissionColor", new Color(3, .3f, .025f));
        bullet.body.SetPropertyBlock(bullet.properties);
        bullet.IsLive = true; salvo?.Add(); active.Add(bullet); bullet.gameObject.SetActive(true);
        return bullet;
    }
    static void EnsurePool()
    {
        if (poolRoot != null) return;
        var root = new GameObject("Enemy projectile pool"); DontDestroyOnLoad(root); poolRoot = root.transform;
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader != null) fallbackMaterial = new Material(shader) { name = "Enemy projectile fallback" };
    }
    void Update()
    {
        if (!IsLive) return;
        if (Owner == null || Owner.IsDead || !Owner.gameObject.activeInHierarchy || (volley != null && !volley.IsValid))
        { Recycle(); return; }
        if (WeaponAction.CombatPaused) return;
        float dt = Mathf.Min(Time.deltaTime, RemainingLifetime);
        Vector3 start = transform.position;
        // SphereCast does not report a collider already overlapping its origin.
        int overlapCount = Physics.OverlapSphereNonAlloc(start, radius, overlaps, hitMask, QueryTriggerInteraction.Ignore);
        Collider overlappingPlayer = null;
        for (int i = 0; i < overlapCount; i++)
        {
            var collider = overlaps[i];
            if (Ignore(collider)) continue;
            // Solid cover wins a simultaneous initial overlap with a player at its edge.
            if (collider.GetComponentInParent<PlayerStateMachine>() != null) { overlappingPlayer = collider; continue; }
            Impact(collider, collider.ClosestPoint(start)); return;
        }
        if (overlappingPlayer != null) { Impact(overlappingPlayer, overlappingPlayer.ClosestPoint(start)); return; }
        float distance = Velocity.magnitude * dt;
        int count = Physics.SphereCastNonAlloc(start, radius, Velocity.normalized, sweepHits, distance, hitMask, QueryTriggerInteraction.Ignore);
        int nearest = -1; float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
            if (!Ignore(sweepHits[i].collider) && sweepHits[i].distance < nearestDistance)
            { nearest = i; nearestDistance = sweepHits[i].distance; }
        if (nearest >= 0)
        {
            var hit = sweepHits[nearest]; transform.position = start + Velocity.normalized * hit.distance;
            Travelled += hit.distance; Impact(hit.collider, hit.point); return;
        }
        transform.position = start + Velocity * dt; Travelled += distance; RemainingLifetime -= dt;
        if (RemainingLifetime <= 0) Recycle();
    }
    bool Ignore(Collider collider) => collider == null || collider.GetComponentInParent<NormalEnemyStateMachine>() != null || collider.GetComponentInParent<MutationObject>()?.Saturated==true;
    void Impact(Collider collider, Vector3 point)
    {
        var device=collider.GetComponentInParent<MutationObject>();
        if(device!=null){device.CaptureEnemyShot();Recycle();return;}
        var player = collider.GetComponentInParent<PlayerStateMachine>();
        if (player != null) player.TakeDamage(damage, shotOrigin, "Proyectil de Centinela");
        else
        {
            var screen=collider.GetComponentInParent<ArenaBreakableCover>();
            if(screen!=null)screen.ApplyStructuralImpact(damage,point,Velocity.normalized);
            else FoundryStructure.From(collider)?.ApplyImpact(point,Velocity.normalized,damage);
        }
        Recycle();
    }
    void Recycle()
    {
        if (!IsLive) return;
        IsLive = false; active.Remove(this); volley?.Complete(); volley = null;
        Owner = null; Velocity = Vector3.zero; RemainingLifetime = 0; damage = 0; hitMask = 0;
        gameObject.SetActive(false); pool.Push(this);
    }
    public static void CancelOwned(NormalEnemyStateMachine source)
    {
        scratch.Clear(); foreach (var bullet in active) if (bullet.Owner == source) scratch.Add(bullet);
        foreach (var bullet in scratch) if (bullet != null) bullet.Recycle(); scratch.Clear();
    }
    public static void ClearAll()
    {
        scratch.Clear(); scratch.AddRange(active);
        foreach (var bullet in scratch) if (bullet != null) bullet.Recycle(); scratch.Clear();
    }
    void OnDisable() { if (IsLive) Recycle(); }
    void OnDestroy() { if (IsLive) { IsLive = false; active.Remove(this); volley?.Complete(); } }
    static void SceneUnloaded(Scene scene)
    {
        scratch.Clear();
        foreach (var bullet in active) if (bullet.Owner == null || bullet.Owner.IsDead) scratch.Add(bullet);
        foreach (var bullet in scratch) if (bullet != null) bullet.Recycle(); scratch.Clear();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Initialize()
    {
        ClearAll(); pool.Clear();
        if (poolRoot != null) Destroy(poolRoot.gameObject);
        if (fallbackMaterial != null) Destroy(fallbackMaterial);
        poolRoot = null; fallbackMaterial = null;
        SceneManager.sceneUnloaded -= SceneUnloaded; SceneManager.sceneUnloaded += SceneUnloaded;
    }
}
