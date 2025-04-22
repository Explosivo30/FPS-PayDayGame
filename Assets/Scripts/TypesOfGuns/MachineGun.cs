using UnityEngine;
using static UnityEngine.Analytics.IAnalytic;

public class MachineGun : BaseGun,IAimable
{
    public override bool IsAutomatic => true;

    public bool IsAiming { get; private set; }

    private float lastShotTime = 0.0f;

    //What IAimableShouldHave
    [SerializeField] private Transform aimPosition;
    [SerializeField] private Transform hipPosition;
    [SerializeField] private float aimSpeed = 10f;
    [SerializeField] private Transform weaponHolder;
    [SerializeField] private float aimFov = 40f;
    private float normalFov;
    private float currentFov;
    [SerializeField] private float speedToAim;

    private void Awake()
    {
        normalFov = Camera.main.fieldOfView;
        currentFov = Camera.main.fieldOfView;
        
    }

    public override void Use()
    {
        if (currentAmmo <= 0)
        {
            Debug.Log("No ammo!");
            //Other Sound of click
            return;
        }

        float secondsPerShot = 1f / fireRate;

        if (Time.time - lastShotTime >= secondsPerShot)
        {
            Debug.Log("Ratatatat!");
            currentAmmo--;
            lastShotTime = Time.time;

        }
    }

    public override void Reload() { currentAmmo = ammo; }

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

        // Smooth FOV transition
        //float targetFOV = IsAiming ? aimFov : normalFov;
        //Camera.main.fieldOfView = Mathf.Lerp(IsAiming? aimFov : normalFov, targetFOV, Time.deltaTime * speedToAim);
        //Camera.main.fieldOfView = currentFov;

        float targetFOV = IsAiming ? aimFov : normalFov;
        Camera.main.fieldOfView = Mathf.Lerp(Camera.main.fieldOfView, targetFOV, Time.deltaTime * speedToAim);
    }
}
