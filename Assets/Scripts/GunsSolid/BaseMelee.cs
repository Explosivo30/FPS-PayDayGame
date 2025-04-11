using UnityEngine;

public abstract class BaseMelee : MonoBehaviour, IWeapon
{
    public float attackCooldown = 2f;
    public float damage = 5f;

    public abstract void Use();
}
