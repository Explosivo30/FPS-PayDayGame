using UnityEngine;

public class ShotGun : BaseGun
{
    public override bool IsAutomatic => false; // not really needed, but explicit
    private float lastShotTime = 0f;
    public bool IsAiming { get; private set; }
    [SerializeField] private Transform hipPosition;

    [SerializeField] private Transform weaponHolder;
    private RecoilData data => recoilData; // hereda de BaseGun

    public override void Reload()
    {
        currentAmmo = ammo;
        Debug.Log("Pistol reloaded.");
    }




    public override void Use()
    {
        if (currentAmmo <= 0)
        {
            Debug.Log("No ammo!");
            return;
        }

        float secondsPerShot = 1f / fireRate;
        if (Time.time - lastShotTime >= secondsPerShot)
        {
            if (Physics.Raycast(weaponHolder.position, transform.forward, out hit, maxRangeGun, layerMask, QueryTriggerInteraction.Collide))
            {
                Debug.Log(hit.transform.name);
                if (hit.collider.TryGetComponent<IDamageable>(out var damageable))
                {
                    damageable.TakeDamage(damage);
                }
                Play(weaponHolder.position, hit.point);

                // (Optional) Spawn impact effects at hit.point

                Debug.Log("ON TARGET");
            }
            else
            {
                Play(weaponHolder.position, weaponHolder.position + weaponHolder.forward * maxRangeGun);
            }
            currentAmmo--;
            ApplyRecoil();
            TriggerPhysicalKickback(weaponHolder);
            Debug.Log("Pistol fired! Damage: " + damage);
            //TODO:  Add sound, muzzle flash

        }


    }

}
