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

    public abstract void Use();

    public abstract void Reload();

}
