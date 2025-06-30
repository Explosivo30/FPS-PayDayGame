using System.Collections;
using UnityEngine;

public abstract class BaseGun : MonoBehaviour, IWeapon,IReloadable, IBulletTracer
{
    public string GunTypeID; // pon “Pistol”, “Rifle”… en prefab
    [Tooltip("The damage this weapon deals per shot.")]
    public float damage = 5f;

    public int ammo = 30;
    public int currentAmmo = 30;
    [Tooltip("How many times can fire per second.")]
    public float fireRate = 10f;
    [Tooltip("Is this weapon shoooting automaticaly if fire button is being held")]
    public virtual bool IsAutomatic => false;

    [Tooltip("Max Range where the damage is the minimum anything beyond that is minimum damage of the gun")]
    public float maxRangeGun = 10f;

    protected float normalFOV;
    SwayData IWeapon.swayData => swayData;

    public SwayData swayData;
    [SerializeField] protected RecoilData recoilData;
    public virtual RecoilData Recoil => recoilData;

    protected RaycastHit hit;

    public LayerMask layerMask;

    protected LineRenderer lr;
    [Tooltip("Duration of the traversal of the raycast Line")]
    [SerializeField] private float duration = 0.05f;

    // ── PHYSICAL KICKBACK (shared) ───────────────────────────────────
    // Holds how far “back” we are right now, in local space:
    protected Vector3 _kickbackOffset;
    // Velocity ref for SmoothDamp:
    protected Vector3 _kickbackVelocity;

    public abstract void Use();

    public virtual void Awake()
    {
        normalFOV = Camera.main.fieldOfView;
    }

    public virtual void ApplyRecoil()
    {
        GunRecoil.Instance.ApplyRecoil(recoilData);
    }

    protected Vector3 GetShootDirection(bool isAiming)
    {
        float spread = isAiming ? recoilData.spreadADS : recoilData.spreadHip;
        Vector3 dir = Camera.main.transform.forward;
        dir += Random.insideUnitSphere * spread * 0.01f;
        return dir.normalized;
    }

    public abstract void Reload();

    /// <summary>
    /// Call this in your Use() override *after* applying camera recoil.
    /// It sets up the initial “jerk” backwards in the gun’s own local Z- axis.
    /// </summary>
    /// <param name="holder">The transform you want to push back (usually your weaponHolder).</param>
    protected void TriggerPhysicalKickback(Transform holder)
    {
        // -Z local is “backwards” in Unity
        _kickbackOffset = Vector3.back * recoilData.kickbackDistance;
        _kickbackVelocity = Vector3.zero;
    }

    /// <summary>
    /// Call this every frame in Update() *before* you do any other
    /// position Lerp (ADS or hip). It smooths out that offset back to zero
    /// and moves 'holder' by the current offset.
    /// </summary>
    protected void HandlePhysicalKickback(Transform holder)
    {
        // 1) Smooth our local-space offset back to zero:
        _kickbackOffset = Vector3.SmoothDamp(
            _kickbackOffset,
            Vector3.zero,
            ref _kickbackVelocity,
            1f / recoilData.returnSpeed
        );

        // 2) Apply it: TransformDirection converts that local offset into world-space
        Vector3 worldOffset = holder.TransformDirection(_kickbackOffset);
        holder.position += worldOffset;
    }


    private IEnumerator DoTrace(Vector3 origin, Vector3 destination)
    {
        lr.SetPosition(0, origin);
        lr.SetPosition(1, destination);
        lr.enabled = true;
        yield return new WaitForSeconds(duration);
        lr.enabled = false;
    }

    public void Play(Vector3 origin, Vector3 destination)
    {
        StartCoroutine(DoTrace(origin, destination));
    }

    public void ApplyWeaponStat(WeaponStat stat, float value)
    {
        switch (stat)
        {
            case WeaponStat.AmmoCapacity:
                ammo += (int)value;
                currentAmmo = Mathf.Min(currentAmmo, ammo);
                break;
            case WeaponStat.FireRate:
                fireRate = fireRate + value; // o * (1+v)
                break;
            case WeaponStat.Damage:
                damage = damage + value;
                break;
            case WeaponStat.RecoilKickUp:
                recoilData.recoilKickUp -= value;
                break;
        }
    }
}
