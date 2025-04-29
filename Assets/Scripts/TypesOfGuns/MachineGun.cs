using UnityEngine;
using static UnityEngine.Analytics.IAnalytic;

public class MachineGun : BaseGun,IAimable
{
    public override bool IsAutomatic => true;


    private float lastShotTime = 0.0f;

    //What IAimableShouldHave
    
   
    [SerializeField] private Transform weaponHolder;
    [SerializeField] private Transform hipTransform; // Place this where the gun sits normally
    [SerializeField] private Transform aimTransform; // Place this where it should go when aiming
    [SerializeField] private AimData aimData;
    private bool isAiming;
    public bool IsAiming => isAiming;
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
        isAiming = true;
        
    }

    public void StopAiming()
    {
        isAiming = false;
        
    }

    private void Update()
    {
        // Target position/rotation directly from the references
        Transform target = isAiming ? aimTransform : hipTransform;

        weaponHolder.position = Vector3.Lerp(weaponHolder.position, target.position, Time.deltaTime * aimData.transitionSpeed);
        weaponHolder.rotation = Quaternion.Lerp(weaponHolder.rotation, target.rotation, Time.deltaTime * aimData.transitionSpeed);

        float targetFOV = isAiming ? aimData.fov : 60f;
        Camera.main.fieldOfView = Mathf.Lerp(Camera.main.fieldOfView, targetFOV, Time.deltaTime * aimData.transitionSpeed);
    }
}
