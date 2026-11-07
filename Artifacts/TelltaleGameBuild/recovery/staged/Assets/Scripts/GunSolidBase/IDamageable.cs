using UnityEngine;

public interface IDamageable
{
    /// <summary>
    /// Apply damage. The implementation handles reducing health, playing effects, etc.
    /// </summary>
    /// <param name="amount">How much damage to apply.</param>
    void TakeDamage(float amount);
}
