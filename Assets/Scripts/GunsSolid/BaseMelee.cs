using UnityEngine;

public abstract class BaseMelee : MonoBehaviour, IWeapon
{
    public SwayData swayData;
    public float attackCooldown = 2f;
    public float damage = 5f;

    public bool IsAutomatic => false;

    SwayData IWeapon.swayData =>swayData;

    public abstract void Use();
}
