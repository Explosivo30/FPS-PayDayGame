using UnityEngine;

public abstract class BaseGun : MonoBehaviour, IWeapon,IReloadable
{
    [Tooltip("The damage this weapon deals per shot.")]
    public float damage = 5f;

    public int ammo = 30;
    public int currentAmmo = 30;
    [Tooltip("How many times can fire per second.")]
    public float fireRate = 10f;
    [Tooltip("Is this weapon shoooting automaticaly if fire button is being held")]
    public virtual bool IsAutomatic => false;

    SwayData IWeapon.swayData => swayData;

    public SwayData swayData;
    [SerializeField] protected RecoilData recoilData;
    public virtual RecoilData Recoil => recoilData;

    public abstract void Use();

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


}
