using UnityEngine;

public class MachineGun : BaseGun, IAimable
{
    public override bool IsAutomatic => true;

    private float lastShotTime = 0f;

    // === ADS (Aim Down Sights) ===
    [SerializeField] private Transform weaponHolder;
    [SerializeField] private Transform hipTransform;   // Posición global hipfire
    [SerializeField] private Transform aimTransform;   // Posición global ADS
    [SerializeField] private AimData aimData;
    private bool isAiming;
    public bool IsAiming => isAiming;
    private float normalFOV;

    // === Kickback físico ===
    private Vector3 currentKickbackLocal = Vector3.zero; // offset en local del arma
    private Vector3 kickbackVelocity = Vector3.zero;     // ref para SmoothDamp
    private float kickbackReturnSpeed;                   // viene de recoilData.returnSpeed
    
    private RecoilData data => recoilData; // hereda de BaseGun

    private void Awake()
    {
        normalFOV = Camera.main.fieldOfView;

        // Colocamos inicialmente el arma en hipfire:
        weaponHolder.position = hipTransform.position;
        weaponHolder.rotation = hipTransform.rotation;

        // Preparamos la velocidad de retorno para el kickback
        kickbackReturnSpeed = data.returnSpeed;
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
            currentAmmo--;
            lastShotTime = Time.time;
            
            if (Physics.Raycast(weaponHolder.position,transform.right,out hit,maxRangeGun,layerMask, QueryTriggerInteraction.Collide))
            {
                Debug.Log("ON TARGET");
            }
            // 1) Aplica recoil de cámara
            ApplyRecoil();
           
        }
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
    }

    public void StartAiming() => isAiming = true;
    public void StopAiming() => isAiming = false;

    private void Update()
    {
        // 1) Suaviza el offset local hacia cero
        currentKickbackLocal = Vector3.SmoothDamp(
            currentKickbackLocal,
            Vector3.zero,
            ref kickbackVelocity,
            1f / kickbackReturnSpeed
        );

        // 2) Posición global base según hipfire o ADS
        Vector3 baseWorldPos = isAiming ? aimTransform.position : hipTransform.position;
        Quaternion baseWorldRot = isAiming ? aimTransform.rotation : hipTransform.rotation;

        // 3) Convierte el offset local a espacio WORLD
        Vector3 kickOffsetWorld = weaponHolder.TransformDirection(currentKickbackLocal);

        // 4) Posición world deseada = posición ADS/Hip + kickback
        Vector3 desiredWorldPos = baseWorldPos + kickOffsetWorld;

        // 5) Lerp posición y rotación en global
        weaponHolder.position = Vector3.Lerp(
            weaponHolder.position,
            desiredWorldPos,
            Time.deltaTime * aimData.transitionSpeed
        );
        weaponHolder.rotation = Quaternion.Lerp(
            weaponHolder.rotation,
            baseWorldRot,
            Time.deltaTime * aimData.transitionSpeed
        );

        // 6) Lerp FOV de la cámara
        float targetFOV = isAiming ? aimData.fov : normalFOV;
        Camera.main.fieldOfView = Mathf.Lerp(
            Camera.main.fieldOfView,
            targetFOV,
            Time.deltaTime * aimData.transitionSpeed
        );
    }
}
