using UnityEngine;

public class MachineGun : BaseGun, IAimable
{
    public override bool IsAutomatic => true;

    private float lastShotTime = float.NegativeInfinity;

    [Header("ADS")]
    [SerializeField] private Transform weaponHolder;
    [SerializeField] private Transform hipTransform;
    [SerializeField] private Transform aimTransform;
    [SerializeField] private AimData aimData;
    [SerializeField] private Camera weaponCamera;
    [SerializeField] private Camera mainCamera;

    private bool isAiming;
    private float currentKickback;
    private float kickbackVelocity;
    private float normalWeaponFOV;

    private RecoilData Data => recoilData;
    public bool IsAiming => isAiming;

    public override void Awake()
    {
        base.Awake();

        if (mainCamera == null)
            mainCamera = GetAimCamera();

        if (weaponCamera != null)
            normalWeaponFOV = weaponCamera.fieldOfView;

        if (weaponHolder != null && hipTransform != null)
            weaponHolder.SetPositionAndRotation(hipTransform.position, hipTransform.rotation);
    }

    public override void Use()
    {
        if (!TryStartShot(ref lastShotTime))
            return;

        FireHitscan(isAiming, weaponHolder);
        ApplyRecoil();
    }

    public override void ApplyRecoil()
    {
        base.ApplyRecoil();

        if (Data == null)
            return;

        currentKickback = Mathf.Min(
            currentKickback + Data.kickbackDistance,
            Data.kickbackDistance * 1.35f);
        kickbackVelocity = 0f;
    }

    public override void Reload()
    {
        currentAmmo = ammo;
    }

    public void StartAiming() => isAiming = true;
    public void StopAiming() => isAiming = false;

    private void Update()
    {
        if (weaponHolder == null || hipTransform == null || aimTransform == null || aimData == null)
            return;

        BeginViewmodelRecoilFrame(weaponHolder);

        float returnTime = 1f / Mathf.Max(1f, Data != null ? Data.returnSpeed : 12f);
        currentKickback = Mathf.SmoothDamp(
            currentKickback,
            0f,
            ref kickbackVelocity,
            returnTime);

        Vector3 baseWorldPosition = isAiming ? aimTransform.position : hipTransform.position;
        Quaternion baseWorldRotation = isAiming ? aimTransform.rotation : hipTransform.rotation;
        Vector3 cameraForward = mainCamera != null ? mainCamera.transform.forward : transform.forward;
        Vector3 desiredWorldPosition = baseWorldPosition - cameraForward * currentKickback;
        float blend = 1f - Mathf.Exp(-aimData.transitionSpeed * Time.deltaTime);

        weaponHolder.position = Vector3.Lerp(weaponHolder.position, desiredWorldPosition, blend);
        weaponHolder.rotation = Quaternion.Slerp(weaponHolder.rotation, baseWorldRotation, blend);

        float aimedFOV = aimData.fov;
        if (weaponCamera != null)
        {
            float targetWeaponFOV = isAiming ? aimedFOV : normalWeaponFOV;
            weaponCamera.fieldOfView = Mathf.Lerp(weaponCamera.fieldOfView, targetWeaponFOV, blend);
        }

        if (mainCamera != null)
        {
            float targetMainFOV = isAiming ? aimedFOV : normalFOV;
            mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, targetMainFOV, blend);
        }

        EndViewmodelRecoilFrame(weaponHolder);
    }
}
