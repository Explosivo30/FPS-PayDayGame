using UnityEngine;

/// <summary>Telegraphed local hazard, with solid cover and circulation routes outside its radius.</summary>
public class RootPulse : MonoBehaviour
{
    public Transform ring;
    public Renderer emitter;
    public float radius = 2.6f, interval = 15f, warning = 2f, active = 1.4f, phase;
    public float damagePerSecond = 10f;
    MaterialPropertyBlock properties;
    float hitTimer;
    float clock;
    enum PulsePhase { Cooling, Warning, Active }
    PulsePhase state;
    CombatHazardBudget.Lease primaryLease;
    public bool IsWarning => state == PulsePhase.Warning;
    public bool IsActive => state == PulsePhase.Active;
    void Awake() { properties = new MaterialPropertyBlock(); clock = Mathf.Repeat(phase, Mathf.Max(.1f, interval)); }
    void Update() { Tick(Time.deltaTime); }
    public void Tick(float seconds)
    {
        if (primaryLease != null && !primaryLease.IsValid) CancelCycle();
        if (WeaponAction.CombatPaused || seconds <= 0) return;
        var session = TowerSession.Instance;
        if (session == null || session.IsTransitioning || GameManager.Instance == null || GameManager.Instance.CurrentWave == 0 ||
            session.player == null || session.player.IsDead) { CancelCycle(); return; }
        clock += seconds;
        if (state == PulsePhase.Cooling)
        {
            if (clock >= Mathf.Max(.1f, interval - warning - active) && CombatHazardBudget.TryReservePrimary(this, out primaryLease))
            { state = PulsePhase.Warning; clock = 0; }
        }
        else if (state == PulsePhase.Warning && clock >= warning)
        { state = PulsePhase.Active; clock = 0; hitTimer = 0; }
        else if (state == PulsePhase.Active && clock >= active) { CancelCycle(); }
        RefreshSignals();
        hitTimer -= seconds;
        if (IsActive && hitTimer <= 0)
        {
            var player = session.player;
            var delta = player.transform.position - transform.position;
            if (new Vector2(delta.x, delta.z).magnitude < radius && Mathf.Abs(delta.y) < 2)
                player.TakeDamage(damagePerSecond * .25f,transform.position,"Pulso de una raíz energética");
            hitTimer = .25f;
        }
    }
    void RefreshSignals()
    {
        bool shown = IsWarning || IsActive;
        if (ring != null)
        {
            ring.gameObject.SetActive(shown);
            // The generated six bars have apothem .43. Circumscribe the damaging circle;
            // never shrink the warning boundary into a zone which still deals damage.
            if (shown) ring.localScale = new Vector3(radius / .43f, .035f, radius / .43f);
        }
        if (emitter == null) return;
        if (properties == null) properties = new MaterialPropertyBlock();
        properties.SetColor("_EmissionColor", IsActive ? new Color(3,.25f,.02f) : IsWarning ?
            new Color(2,1,.02f) * (.8f + .2f * Mathf.Sin(clock * 12)) : new Color(.03f,.45f,.25f));
        emitter.SetPropertyBlock(properties);
    }
    void CancelCycle()
    {
        primaryLease?.Dispose(); primaryLease = null; state = PulsePhase.Cooling; clock = hitTimer = 0; RefreshSignals();
    }
    void OnDisable() { CancelCycle(); }
}
