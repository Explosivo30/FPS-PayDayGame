using UnityEngine;

public abstract class BaseGun : MonoBehaviour, IWeapon,IReloadable
{
    public float damage = 5f;

    public int ammo = 30;
    public float fireRate = 10f;

    public abstract void Use();

    public abstract void Reload();

}
