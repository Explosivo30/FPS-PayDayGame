using UnityEngine;
using UnityEngine.AI;

public class NormalEnemyStateMachine : StateMachine,IDamageable
{
    public NavMeshAgent agent;
    [Header("Health")]
    float currentHealth;
    public float maxHealth = 100f;

    [Header("Color Gradient")]
    [Tooltip("Color at full health (orange).")]
    [SerializeField] private Color fullHealthColor = new Color(1f, 0.5f, 0f);
    [Tooltip("Color at zero health (red).")]
    [SerializeField] private Color zeroHealthColor = Color.red;

    private Renderer rend;
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        currentHealth = maxHealth;
        //CHANGE RENDER
        rend = GetComponent<Renderer>();
        UpdateTint();

        SwitchState(new IdleNormalEnemyState(this));
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        //Debug.Log($"{name} took {amount} damage. Health now {currentHealth}");
        UpdateTint();
        if (currentHealth <= 0f)
            Die();
    }

    private void Die()
    {
        // Play death animation, drop loot, disable collider, etc.
        Destroy(gameObject);
    }

    private void UpdateTint()
    {
        // t = 1 at full health, 0 at zero health
        float t = currentHealth / maxHealth;
        // Lerp from red (0) to orange (1)
        Color current = Color.Lerp(zeroHealthColor, fullHealthColor, t);
        rend.material.color = current;
    }
}
