using UnityEngine;

[RequireComponent(typeof(WeaponAction))]
public class Pistol : BaseGun, IAimable
{
    public override bool IsAutomatic => false;
    public bool IsAiming { get; private set; }
    private float lastShotTime = float.NegativeInfinity;
    bool charged;
    public void FireCharged(){charged=true;try{Use();}finally{charged=false;}}
    public override void Use()
    {
        if (!TryStartShot(ref lastShotTime,charged?3:1)) return;
        if(charged)FirePiercing(IsAiming,3,2);else FireHitscan(IsAiming, null);
        ApplyRecoil();
    }
    public override void Reload() { StopAiming(); Action.BeginReload(); }
    public void StartAiming() { IsAiming = !Action.IsBusy; }
    public void StopAiming() { IsAiming = false; }
    protected override void OnDisable() { IsAiming = false; base.OnDisable(); }
}
