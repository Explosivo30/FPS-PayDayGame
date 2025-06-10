using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class NormalEnemyStateMachine : StateMachine, IDamageable, IWeapon
{
    public NavMeshAgent agent;
    [Header("Health")]
    float currentHealth;
    public float maxHealth = 100f;

    [Header("References")]
    public float detectionRange = 20f;
    public float attackRange = 100f;


    [Header("Color Gradient")]
    [Tooltip("Color at full health (orange).")]
    [SerializeField] private Color fullHealthColor = new Color(1f, 0.5f, 0f);
    [Tooltip("Color at zero health (red).")]
    [SerializeField] private Color zeroHealthColor = Color.red;

    private Renderer rend;


    protected RaycastHit hit;

    public LayerMask layerMask;

    protected LineRenderer lr;
    [SerializeField] private float duration = 0.05f;
    private float shootCooldown = 3f;
    private float currentShootCooldown;

    public bool IsAutomatic => true;

    public SwayData swayData => null;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        lr = GetComponent<LineRenderer>();
        currentHealth = maxHealth;
        //CHANGE RENDER
        rend = GetComponent<Renderer>();
        UpdateTint();
        currentShootCooldown = shootCooldown;
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

    protected void Shoot()
    {
        //Shoot
    }

    private void UpdateTint()
    {
        // t = 1 at full health, 0 at zero health
        float t = currentHealth / maxHealth;
        // Lerp from red (0) to orange (1)
        Color current = Color.Lerp(zeroHealthColor, fullHealthColor, t);
        rend.material.color = current;
    }

    public void Use()
    {
        //RAYCAST TO PLAYER AND ACTIVATE TRAILING
        if(CheckDistance() && currentShootCooldown < 0)
        {
            
            if (Physics.Raycast(transform.position, GameManager.Instance.GetPlayerTransforms()[0].position, out hit, detectionRange , layerMask, QueryTriggerInteraction.Collide))
            {
                
                if (hit.collider.TryGetComponent<IDamageable>(out var damageable))
                {
                    Play(transform.position, hit.point);
                    damageable.TakeDamage(20);
                    Debug.Log("ON TARGET");
                }

                // (Optional) Spawn impact effects at hit.point…

                
            }


            currentShootCooldown = shootCooldown;
        }

        //Muzzle
        
    }
   
    public void ReduceShootCooldown()
    {
        currentShootCooldown -= Time.deltaTime;
    }

    //CHANGE THIS TO THE STATE IT DESERVES TO BE
    public bool CheckDistance()
    {
        // Check for player
        float dist = Vector3.Distance(transform.position, GameManager.Instance.GetPlayerTransforms()[0].position);
        if (dist <= detectionRange)
        {
            return true;
        }
        else
        {
            return false;
        }
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
}
