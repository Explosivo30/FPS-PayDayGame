using UnityEngine;

public class ShotGun : BaseGun
{
    public override bool IsAutomatic => false;

    private float lastShotTime = float.NegativeInfinity;

    [SerializeField] private Transform weaponHolder;

    public override void Reload()
    {
        currentAmmo = ammo;
    }

    public override void Use()
    {
        if (!TryStartShot(ref lastShotTime))
            return;

        FireHitscan(false, weaponHolder);
        ApplyRecoil();
        TriggerPhysicalKickback(weaponHolder);
    }

    private void Update()
    {
        BeginViewmodelRecoilFrame(weaponHolder);
        HandlePhysicalKickback(weaponHolder);
        EndViewmodelRecoilFrame(weaponHolder);
    }
}
