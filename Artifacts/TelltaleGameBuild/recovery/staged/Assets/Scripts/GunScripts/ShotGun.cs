using UnityEngine;

[RequireComponent(typeof(WeaponAction))]
public class ShotGun : BaseGun, IAimable
{
    public override bool IsAutomatic => false;
    public bool IsAiming { get; private set; }
    [Min(1)] public int pelletCount = 8;
    private float lastShotTime = float.NegativeInfinity;
    public override void Use()
    {
        if (!TryStartShot(ref lastShotTime)) return;
        for (int i = 0; i < pelletCount; i++) FireHitscan(IsAiming, null, i == 0);
        ApplyRecoil();
    }
    public override void Reload() { StopAiming(); Action.BeginReload(); }
    public void StartAiming() { IsAiming = !Action.IsBusy; }
    public void StopAiming() { IsAiming = false; }
    protected override void OnDisable() { IsAiming = false; base.OnDisable(); }
}
