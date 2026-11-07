using System.Linq;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Fracture releases a prepared gantry. Stable geometry, rather than debris, defines its new route.</summary>
public sealed class AlphaDistrictCollapse : MonoBehaviour
{
    public enum CollapsePhase { Armed, Warning, Falling, Settled }
    public FoundryStructure[] supports;
    public Transform movingLoad;
    public Vector3 initialLoadOffset = Vector3.up * 8;
    public GameObject finalRamp, oldNest, lowBlocker;
    public NavMeshObstacle rampGate, oldNestGate;
    public Bounds impactBounds, nestBounds;
    public Transform warningZone;
    public bool Ready => supports != null && supports.Length > 0 && supports.All(s => s && s.Ready);
    public CollapsePhase State { get; private set; }
    public bool IsSettled => State == CollapsePhase.Settled;
    public int ImpactKills { get; private set; }
    public int TransitionCount { get; private set; }
    public bool SafetyBlocked { get; private set; }
    Vector3 landing;
    float timer;
    Collider[] rampColliders;
    Bounds rampBounds;
    CombatHazardBudget.Lease lease;
    AudioSource audioSource;
    AudioClip sound;

    void Start()
    {
        landing = movingLoad ? movingLoad.position : Vector3.zero;
        rampColliders = finalRamp ? finalRamp.GetComponentsInChildren<Collider>(true) : new Collider[0];
        rampBounds = new Bounds();
        for (int i = 0; i < rampColliders.Length; i++)
        {
            if (i == 0) rampBounds = rampColliders[i].bounds; else rampBounds.Encapsulate(rampColliders[i].bounds);
            rampColliders[i].enabled = false;
        }
        if (movingLoad) movingLoad.position = landing + initialLoadOffset;
        if (rampGate) rampGate.enabled = true;
        if (oldNestGate) oldNestGate.enabled = false;
        if (lowBlocker) lowBlocker.SetActive(false);
        if (warningZone) warningZone.gameObject.SetActive(false);
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = .8f; audioSource.maxDistance = 70;
        sound = ImpactFeedbackPlayer.MakeSound("Gantry collapse", 1);
    }
    void Update()
    {
        if (WeaponAction.CombatPaused || IsSettled || !Ready) return;
        if (State == CollapsePhase.Armed)
        {
            if (!supports.All(s => s.IsOpen)) return;
            State = CollapsePhase.Warning; TransitionCount++; timer = 0;
            if (warningZone) warningZone.gameObject.SetActive(true);
            audioSource.PlayOneShot(sound, .4f);
        }
        var player = TowerSession.Instance ? TowerSession.Instance.player : null;
        SafetyBlocked = player && (Expanded(rampBounds, .65f).Contains(player.transform.position) ||
            Expanded(nestBounds, .65f).Contains(player.transform.position) || Expanded(impactBounds, .6f).Contains(player.transform.position));
        // The visible mechanical brake holds the load while the operator occupies its final structure.
        // Enemies and their projectiles continue; this does not pause the encounter or heal the player.
        if (SafetyBlocked)
        {
            if (State == CollapsePhase.Warning) timer = 0;
            lease?.Dispose(); lease = null;
            return;
        }
        if (lease == null || !lease.IsValid)
        {
            if (!CombatHazardBudget.TryReservePrimary(this, out lease)) return;
        }
        timer += Time.deltaTime;
        if (State == CollapsePhase.Warning && timer >= 2.0f)
        {
            State = CollapsePhase.Falling; timer = 0;
            DestroyOccupiedNest();
            if (oldNest) oldNest.SetActive(false);
            if (oldNestGate) oldNestGate.enabled = true;
        }
        if (State != CollapsePhase.Falling) return;
        float t = Mathf.Clamp01(timer / 1.1f);
        if (movingLoad) movingLoad.position = Vector3.Lerp(landing + initialLoadOffset, landing, t * t);
        CrushMovingLoad();
        if (t < 1) return;
        foreach (var collider in rampColliders) if (collider) collider.enabled = true;
        if (rampGate) rampGate.gameObject.SetActive(false);
        if (lowBlocker) lowBlocker.SetActive(true);
        State = CollapsePhase.Settled; lease?.Dispose(); lease = null;
        if (warningZone) warningZone.gameObject.SetActive(false);
        audioSource.PlayOneShot(sound, .9f);
    }
    static Bounds Expanded(Bounds value, float distance) { value.Expand(distance * 2); return value; }
    void DestroyOccupiedNest()
    {
        foreach (var enemy in FindObjectsByType<NormalEnemyStateMachine>(FindObjectsSortMode.None))
        {
            if (enemy.IsDead || !Expanded(nestBounds, .2f).Contains(enemy.transform.position + Vector3.up * .8f)) continue;
            enemy.Ranged?.Cancel(); enemy.Navigation.Stop();
            var member = enemy.GetComponent<FoundrySectorMember>(); if (member) member.sector = null;
            enemy.TakeDamage(500); if (enemy.IsDead) ImpactKills++;
        }
    }
    void CrushMovingLoad()
    {
        if (!movingLoad) return;
        var renderers = movingLoad.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        var volume = renderers[0].bounds;
        foreach (var item in renderers) volume.Encapsulate(item.bounds);
        volume.Expand(.3f);
        foreach (var enemy in FindObjectsByType<NormalEnemyStateMachine>(FindObjectsSortMode.None))
            if (!enemy.IsDead && volume.Contains(enemy.transform.position + Vector3.up * .8f))
            { enemy.TakeDamage(500); if (enemy.IsDead) ImpactKills++; }
    }
    void OnDisable() { lease?.Dispose(); lease = null; }
    void OnDestroy() { if (sound) Destroy(sound); }
}
