using UnityEngine;

public class Pistol : BaseGun, IAimable
{
    public override bool IsAutomatic => false; // not really needed, but explicit

    public bool IsAiming { get; private set; }

    [SerializeField] private Transform aimPosition;
    [SerializeField] private Transform hipPosition;
    [SerializeField] private float aimSpeed = 10f;
    [SerializeField] private Transform weaponHolder;

    public override void Use()
    {
        if (ammo <= 0)
        {
            Debug.Log("No ammo!");
            return;
        }

        ammo--;
        Debug.Log("Pistol fired! Damage: " + damage);
        // Add sound, muzzle flash, raycast etc.
    }

    public override void Reload()
    {
        ammo = 30;
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
