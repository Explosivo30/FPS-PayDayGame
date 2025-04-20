using UnityEngine;

public abstract class BaseMelee : MonoBehaviour, IWeapon
{
    public float attackCooldown = 2f;
    public float damage = 5f;

    public bool IsAutomatic => false;

    public abstract void Use();
}
