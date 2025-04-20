using UnityEngine;

public class MachineGun : BaseGun,IAimable
{
    public override bool IsAutomatic => true;

    public bool IsAiming { get; private set; }

    private float lastShotTime = 0.0f;


    [SerializeField] private Transform aimPosition;
    [SerializeField] private Transform hipPosition;
    [SerializeField] private float aimSpeed = 10f;
    [SerializeField] private Transform weaponHolder;


    public override void Use()
{
    float secondsPerShot = 1f / fireRate;

    if (Time.time - lastShotTime >= secondsPerShot)
    {
        Debug.Log("Ratatatat!");
        lastShotTime = Time.time;
    }
}

    public override void Reload() { currentAmmo = ammo; }

    public void StartAiming()
    {
        IsAiming = true;
        Camera.main.fieldOfView = 40f; // for scoped view
    }

    public void StopAiming()
    {
        IsAiming = false;
        Camera.main.fieldOfView = 60f; // normal FOV
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
