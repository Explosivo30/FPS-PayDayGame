using UnityEngine;
using TMPro;

/// <summary>Two sight-line states; a conservative full-sweep actor check prevents trapping.</summary>
[DisallowMultipleComponent]
public sealed class ArenaMechanicalPetal : MonoBehaviour
{
    public enum PetalState { Ready, Warning, Moving, Cooldown }
    public Transform panel;
    public Renderer conductor;
    public TMP_Text statusText;
    [Tooltip("Original upper decoration retained inactive by the installer; structural island is untouched.")]
    public GameObject[] preservedDecoration = new GameObject[0];
    public Vector3 safetyCenter = new Vector3(0, 1.85f, 0), safetyHalfExtents = new Vector3(2.35f, 1.9f, 2.35f);
    [Min(.2f)] public float warningSeconds = .9f, movementSeconds = 1.1f, cooldownSeconds = 7f;
    public float closedAngle, openAngle = 90f;
    public PetalState CurrentState { get; private set; }
    public bool IsOpen { get; private set; }
    public bool SafetyBlocked { get; private set; }
    public float CooldownRemaining => CurrentState == PetalState.Cooldown ? Mathf.Max(0, cooldownSeconds - timer) : 0;
    public int ActivationCount { get; private set; }
    public float MovementProgress => Mathf.Clamp01(progress);
    public string InteractionPrompt => SafetyBlocked ? "DESPEJA EL PÉTALO" : CurrentState == PetalState.Ready ?
        "E  /  DISPARA AL CONDUCTOR · CAMBIAR ÁNGULO" : CurrentState == PetalState.Cooldown ? "PÉTALO RECARGANDO" : "PÉTALO EN MOVIMIENTO";
    readonly Collider[] overlaps = new Collider[48];
    MaterialPropertyBlock properties;
    float timer, progress, startAngle, targetAngle;

    void Awake() { properties = new MaterialPropertyBlock(); ResetForVisit(); }
    public bool Activate()
    {
        if (!isActiveAndEnabled || WeaponAction.CombatPaused || CurrentState != PetalState.Ready || panel == null ||
            (TowerSession.Instance != null && TowerSession.Instance.IsTransitioning)) return false;
        timer = 0; progress = 0; startAngle = IsOpen ? openAngle : closedAngle; targetAngle = IsOpen ? closedAngle : openAngle;
        CurrentState = PetalState.Warning; SafetyBlocked = false; ActivationCount++; RefreshSignal(); return true;
    }
    public void ResetForVisit()
    {
        CurrentState = PetalState.Ready; IsOpen = false; SafetyBlocked = false; ActivationCount = 0; timer = progress = 0;
        if (panel != null) panel.localRotation = Quaternion.Euler(0, closedAngle, 0);
        RefreshSignal();
    }
    void FixedUpdate() { Tick(Time.fixedDeltaTime); }
    public void Tick(float seconds)
    {
        if (WeaponAction.CombatPaused || seconds <= 0 || panel == null) return;
        if (CurrentState == PetalState.Warning)
        {
            timer += seconds;
            if (timer >= warningSeconds) { timer = 0; CurrentState = PetalState.Moving; }
        }
        if (CurrentState == PetalState.Moving)
        {
            SafetyBlocked = IsSweepOccupied();
            if (!SafetyBlocked)
            {
                progress = Mathf.Min(1, progress + seconds / Mathf.Max(.2f, movementSeconds));
                float smooth = progress * progress * (3 - 2 * progress);
                panel.localRotation = Quaternion.Euler(0, Mathf.Lerp(startAngle, targetAngle, smooth), 0);
                if (progress >= 1) { IsOpen = !IsOpen; timer = 0; CurrentState = PetalState.Cooldown; }
            }
        }
        else if (CurrentState == PetalState.Cooldown)
        {
            timer += seconds;
            if (timer >= cooldownSeconds) { timer = 0; CurrentState = PetalState.Ready; }
        }
        RefreshSignal();
    }
    public bool IsSweepOccupied()
    {
        // Includes the entire rotation, not just the next pose. A jumping/teleported character
        // on the decorative island stops the motor and can leave without a reversing collider.
        Physics.SyncTransforms();
        Vector3 scale = transform.lossyScale;
        scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        int count = Physics.OverlapBoxNonAlloc(transform.TransformPoint(safetyCenter), Vector3.Scale(safetyHalfExtents, scale),
            overlaps, transform.rotation, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        if (count == overlaps.Length) return true; // fail closed if an unusually crowded scene fills the buffer
        for (int i = 0; i < count; i++)
        {
            var hit = overlaps[i];
            if (hit != null && (hit is CharacterController || hit.GetComponentInParent<PlayerStateMachine>() != null ||
                hit.GetComponentInParent<NormalEnemyStateMachine>() != null)) return true;
        }
        return false;
    }
    void RefreshSignal()
    {
        if (properties == null) properties = new MaterialPropertyBlock();
        if (conductor != null)
        {
            Color signal = SafetyBlocked ? new Color(2, .35f, .03f) : CurrentState == PetalState.Ready ? new Color(.05f, .6f, .3f) :
                CurrentState == PetalState.Cooldown ? new Color(.12f, .12f, .04f) : new Color(1.4f, .75f, .02f);
            properties.SetColor("_EmissionColor", signal); conductor.SetPropertyBlock(properties);
        }
        if (statusText != null) statusText.text = SafetyBlocked ? "DESPEJA EL PÉTALO" : CurrentState == PetalState.Ready ?
            "E / DISPARA\nCAMBIAR ÁNGULO" : CurrentState == PetalState.Cooldown ? "RECARGANDO" : "ATENCIÓN · PÉTALO";
    }
}
