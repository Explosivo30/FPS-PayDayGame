using UnityEngine;
using static UnityEngine.Analytics.IAnalytic;
using UnityEngine.UIElements;

public class Pistol : BaseGun, IAimable
{
    public override bool IsAutomatic => false; // not really needed, but explicit
    private float lastShotTime = 0f;
    public bool IsAiming { get; private set; }

    [SerializeField] private Transform aimPosition;
    [SerializeField] private Transform hipPosition;
    [SerializeField] private float aimSpeed = 10f;
    [SerializeField] private Transform weaponHolder;
    [SerializeField] private AimData aimData;

    private RecoilData data => recoilData; // hereda de BaseGun

    public override void Awake()
    {
        base.Awake();
        lr = GetComponent<LineRenderer>();
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

                // (Optional) Spawn impact effects at hit.point…

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

    public override void ApplyRecoil()
    {
        base.ApplyRecoil();
    }

    public override void Reload()
    {
        currentAmmo = ammo;
        Debug.Log("Pistol reloaded.");
    }

    public void StartAiming()
    {
        IsAiming = true;
    }

    public void StopAiming()
    {
        IsAiming = false;
    }

    private void Update()
    {
        HandlePhysicalKickback(weaponHolder);

        // Smooth transition (optional but juicy)
        
        if (weaponHolder != null)
        {
            Transform target = IsAiming ? aimPosition : hipPosition;
            weaponHolder.position = Vector3.Lerp(weaponHolder.position, target.position, Time.deltaTime * aimSpeed);
            weaponHolder.rotation = Quaternion.Lerp(weaponHolder.rotation, target.rotation, Time.deltaTime * aimSpeed);
        }
        float targetFOV = IsAiming ? aimData.fov : normalFOV;
        Camera.main.fieldOfView = Mathf.Lerp(
            Camera.main.fieldOfView,
            targetFOV,
            Time.deltaTime * aimData.transitionSpeed
        );


    }
}
