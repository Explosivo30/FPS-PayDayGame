using UnityEngine;

[RequireComponent(typeof(WeaponAction))]
public class MachineGun : BaseGun, IAimable
{
    public override bool IsAutomatic => true;
    public bool IsAiming { get; private set; }
    private float lastShotTime = float.NegativeInfinity;
    public override void Use()
    {
        if (!TryStartShot(ref lastShotTime)) return;
        FireHitscan(IsAiming, null);
        ApplyRecoil();
    }
    public override void Reload() { StopAiming(); Action.BeginReload(); }
    public void StartAiming() { IsAiming = !Action.IsBusy; }
    public void StopAiming() { IsAiming = false; }
    protected override void OnDisable() { IsAiming = false; base.OnDisable(); }
}
