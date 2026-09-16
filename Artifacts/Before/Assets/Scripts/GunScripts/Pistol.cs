using UnityEngine;

public class Pistol : BaseGun, IAimable
{
    public override bool IsAutomatic => false;

    private float lastShotTime = float.NegativeInfinity;
    private float currentKickback;
    private float kickbackVelocity;

    public bool IsAiming { get; private set; }

    [Header("ADS")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform aimPosition;
    [SerializeField] private Transform hipPosition;
    [SerializeField] private float aimSpeed = 10f;
    [SerializeField] private Transform weaponHolder;
    [SerializeField] private AimData aimData;

    public override void Awake()
    {
        base.Awake();
        if (mainCamera == null)
            mainCamera = GetAimCamera();
    }

    public override void Use()
    {
        if (!TryStartShot(ref lastShotTime))
            return;

        FireHitscan(IsAiming, weaponHolder);
        ApplyRecoil();
    }

    public override void ApplyRecoil()
    {
        base.ApplyRecoil();

        if (recoilData == null)
            return;

        currentKickback = Mathf.Min(
            currentKickback + recoilData.kickbackDistance,
            recoilData.kickbackDistance * 1.25f);
        kickbackVelocity = 0f;
    }

    public override void Reload()
    {
        currentAmmo = ammo;
    }

    public void StartAiming() => IsAiming = true;
    public void StopAiming() => IsAiming = false;

    private void Update()
    {
        if (weaponHolder == null || hipPosition == null || aimPosition == null || aimData == null)
            return;

        BeginViewmodelRecoilFrame(weaponHolder);

        float returnTime = 1f / Mathf.Max(1f, recoilData != null ? recoilData.returnSpeed : 12f);
        currentKickback = Mathf.SmoothDamp(
            currentKickback,
            0f,
            ref kickbackVelocity,
            returnTime);

        Transform target = IsAiming ? aimPosition : hipPosition;
        Vector3 cameraForward = mainCamera != null ? mainCamera.transform.forward : transform.forward;
        Vector3 desiredPosition = target.position - cameraForward * currentKickback;
        float blend = 1f - Mathf.Exp(-aimSpeed * Time.deltaTime);

        weaponHolder.position = Vector3.Lerp(weaponHolder.position, desiredPosition, blend);
        weaponHolder.rotation = Quaternion.Slerp(weaponHolder.rotation, target.rotation, blend);

        if (mainCamera != null)
        {
            float targetFOV = IsAiming ? aimData.fov : normalFOV;
            mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, targetFOV, blend);
        }

        EndViewmodelRecoilFrame(weaponHolder);
    }
}
