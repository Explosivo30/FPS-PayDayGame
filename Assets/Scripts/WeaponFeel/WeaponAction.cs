using System;
using UnityEngine;

public enum WeaponActionState { Ready, Drawing, Holstering, Reloading, Attacking }

/// <summary>One clock owns mechanical actions; visual clips follow its normalized progress.</summary>
[DisallowMultipleComponent]
public class WeaponAction : MonoBehaviour
{
    public float reloadDuration = 1.3f;
    public bool shellReload;
    public float reloadStart = .35f, shellInsert = .45f, reloadEnd = .3f;
    public float drawDuration = .22f, holsterDuration = .16f;
    public WeaponActionState State { get; private set; } = WeaponActionState.Ready;
    public float Progress => duration > 0 ? Mathf.Clamp01(elapsed / duration) : 1f;
    public int ReloadPhase { get; private set; }
    public bool IsBusy => State != WeaponActionState.Ready;
    public event Action<WeaponActionState> StateChanged;
    public event Action ShotFired;
    public event Action AmmoChanged;
    public event Action ReloadStep;
    private float elapsed, duration;
    private bool queuedShot;
    private BaseGun gun;

    public static bool CombatPaused => Time.timeScale <= 0f || (ShopManager.Instance != null && ShopManager.Instance.IsOpen);
    private void Awake() { gun = GetComponent<BaseGun>(); }
    private void OnDisable() { Cancel(); }
    public void BeginDraw() { Cancel(); Set(WeaponActionState.Drawing, drawDuration); }
    public void BeginHolster() { Cancel(); Set(WeaponActionState.Holstering, holsterDuration); }
    public void BeginAttack(float seconds) { Set(WeaponActionState.Attacking, seconds); }
    public void NotifyShot() { ShotFired?.Invoke(); AmmoChanged?.Invoke(); }
    public void NotifyAmmo() { AmmoChanged?.Invoke(); }
    public void Cancel()
    {
        queuedShot = false; ReloadPhase = 0;
        Set(WeaponActionState.Ready, 0f);
    }
    private void Set(WeaponActionState state, float seconds)
    {
        State = state; elapsed = 0; duration = Mathf.Max(0, seconds);
        StateChanged?.Invoke(state);
    }
    public bool RequestFire()
    {
        if (CombatPaused || !isActiveAndEnabled) return false;
        if (State == WeaponActionState.Reloading && shellReload && gun != null && gun.currentAmmo > 0)
            queuedShot = true;
        return State == WeaponActionState.Ready;
    }
    public void BeginReload()
    {
        if (CombatPaused || IsBusy || gun == null || gun.currentAmmo >= gun.ammo) return;
        queuedShot = false; ReloadPhase = 0;
        Set(WeaponActionState.Reloading, shellReload ? reloadStart : reloadDuration);
        ReloadStep?.Invoke();
    }
    private void Update() { Tick(Time.deltaTime); }
    public void Tick(float dt)
    {
        if (CombatPaused || State == WeaponActionState.Ready) return;
        elapsed += Mathf.Max(0, dt);
        if (elapsed < duration) return;
        // Carry over excess frame time to avoid frame-rate-dependent shell durations.
        float carry = elapsed - duration;
        if (State == WeaponActionState.Reloading)
        {
            if (!shellReload) { gun.currentAmmo = gun.ammo; NotifyAmmo(); }
            else if (ReloadPhase < 2)
            {
                if (ReloadPhase == 1) { gun.currentAmmo = Mathf.Min(gun.ammo, gun.currentAmmo + 1); NotifyAmmo(); ReloadStep?.Invoke(); }
                ReloadPhase = queuedShot || gun.currentAmmo >= gun.ammo ? 2 : 1;
                elapsed = carry; duration = ReloadPhase == 2 ? reloadEnd : shellInsert;
                return;
            }
        }
        bool fire = queuedShot; queuedShot = false;
        Set(WeaponActionState.Ready, 0);
        if (fire && gun != null) gun.Use();
    }
}
