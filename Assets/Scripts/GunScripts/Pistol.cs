using UnityEngine;
using static UnityEngine.Analytics.IAnalytic;
using UnityEngine.UIElements;

public class Pistol : BaseGun, IAimable
{
    public override bool IsAutomatic => false; // not really needed, but explicit

    public bool IsAiming { get; private set; }

    [SerializeField] private Transform aimPosition;
    [SerializeField] private Transform hipPosition;
    [SerializeField] private float aimSpeed = 10f;
    [SerializeField] private Transform weaponHolder;
    [SerializeField] private AimData aimData;

    // === Kickback físico ===
    private Vector3 currentKickbackLocal = Vector3.zero; // offset en local del arma
    private Vector3 kickbackVelocity = Vector3.zero;     // ref para SmoothDamp
    private float kickbackReturnSpeed;                   // viene de recoilData.returnSpeed
    float normalFov = 80f;
    private RecoilData data => recoilData; // hereda de BaseGun
    

    public override void Use()
    {
        if (currentAmmo <= 0)
        {
            Debug.Log("No ammo!");
            return;
        }

        if (Physics.Raycast(weaponHolder.position, transform.forward, out hit, maxRangeGun, layerMask, QueryTriggerInteraction.Collide))
        {
            if (hit.collider.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.TakeDamage(damage);
            }

            // (Optional) Spawn impact effects at hit.point…

            Debug.Log("ON TARGET");
        }


        currentAmmo--;
        ApplyRecoil();
        Debug.Log("Pistol fired! Damage: " + damage);
        // Add sound, muzzle flash, raycast etc.
    }

    public override void ApplyRecoil()
    {
        base.ApplyRecoil();
        // 2) Inicializa kickback físico: –Z local del arma
        currentKickbackLocal = -Vector3.right * data.kickbackDistance; //TODO: CAMBIAR POR FORWARD
        kickbackVelocity = Vector3.zero;
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

      
        // Smooth transition (optional but juicy)

        if (weaponHolder != null)
        {
            Transform target = IsAiming ? aimPosition : hipPosition;
            weaponHolder.position = Vector3.Lerp(weaponHolder.position, target.position, Time.deltaTime * aimSpeed);
            weaponHolder.rotation = Quaternion.Lerp(weaponHolder.rotation, target.rotation, Time.deltaTime * aimSpeed);
        }
        
    }
}
