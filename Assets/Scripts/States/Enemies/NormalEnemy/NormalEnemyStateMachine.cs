using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class NormalEnemyStateMachine : StateMachine, IDamageable, IWeapon, ISquadMember
{
    public NavMeshAgent agent;
    [NonSerialized]
    public Vector3 _currentFormationPosition;

    [SerializeField] private LayerMask enemyLayer;

    public float separationDistance = 1f;
    public float separationStrength = 2f;

    [Header("Health")]
    float currentHealth;
    public float maxHealth = 100f;

    [Header("References")]
    public float detectionRange = 20f;
    public float attackRange = 100f;

    [Header("Accuracy")]
    [Range(0f, 1f)]
    [SerializeField] private float accuracy = 0.75f;         // 0 = never hits, 1 = always on-target
    [SerializeField] private float maxSpreadAngle = 10f;     // in degrees


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
    [SerializeField] private float shootCooldown = 3f;
    private float currentShootCooldown;

    public bool IsAutomatic => true;

    public SwayData swayData => null;

    public Transform Transform => transform;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        lr = GetComponent<LineRenderer>();
        SquadManager.Instance.Register(this);
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
        // fire the death event
        EnemyEvents.OnDeath?.Invoke(this);
        Destroy(gameObject);
    }


    private void UpdateTint()
    {
        // t = 1 at full health, 0 at zero health
        float t = currentHealth / maxHealth;
        // Lerp from red (0) to orange (1)
        Color current = Color.Lerp(zeroHealthColor, fullHealthColor, t);
        if(rend == null)
        {
            Debug.Log("IS NULL");
        }
        rend.material.SetColor("_BaseColor", current);
    }

    public void Use()
    {
        if (!CheckDistance() || currentShootCooldown > 0f)
            return;

        // 1) Compute perfect “to‐player” direction
        Vector3 playerPos = GameManager.Instance.GetPlayerTransforms()[0].position;
        Vector3 baseDir = (playerPos - transform.position).normalized;

        // 2) Compute current spread angle based on accuracy
        //    when accuracy=1 => spread=0; when accuracy=0 => spread=maxSpreadAngle
        float spread = (1f - accuracy) * maxSpreadAngle;

        // 3) Random yaw & pitch offsets in [-spread, +spread]
        float yaw = UnityEngine.Random.Range(-spread, spread);
        float pitch = UnityEngine.Random.Range(-spread, spread);

        // 4) Rotate the baseDir by those offsets
        Vector3 shotDir = Quaternion.Euler(pitch, yaw, 0f) * baseDir;

        
        // 5) Fire the raycast along shotDir
        if (Physics.Raycast(
              transform.position,
              shotDir,
              out hit,
              detectionRange,
              layerMask,
              QueryTriggerInteraction.Collide))
        {
            if (hit.collider.TryGetComponent<IDamageable>(out var damageable))
            {
                
                damageable.TakeDamage(20);
                Debug.Log("ON TARGET");
            }

            if (hit.collider != null)
            {
                Play(transform.position, hit.point);
            }
            else
            {
                Play(transform.position, shotDir);
            }


        }



        currentShootCooldown = shootCooldown;
        


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

    public void MoveToFormationPosition(Vector3 formationPosition)
    {
        _currentFormationPosition = formationPosition;
    }

    public Vector3 SeparationForce()
    {
        // 1) Guard against invalid data:
        if (separationDistance <= 0f || float.IsNaN(separationDistance) || float.IsInfinity(separationDistance))
            return Vector3.zero;

        Vector3 pos = transform.position;
        if (float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z))
            return Vector3.zero;

        // 2) Use a layer‐filtered OverlapSphere:
        Collider[] hits = Physics.OverlapSphere(pos, separationDistance, enemyLayer , QueryTriggerInteraction.Ignore);

        Vector3 steer = Vector3.zero;
        int count = 0;
        foreach (var c in hits)
        {
            // skip self
            if (c.gameObject == gameObject)
                continue;

            // only consider other enemies
            if (c.TryGetComponent<NormalEnemyStateMachine>(out var other))
            {
                Vector3 diff = pos - other.transform.position;
                float distSqr = diff.sqrMagnitude;
                if (distSqr > 0f)
                {
                    steer += diff.normalized / Mathf.Sqrt(distSqr);
                    count++;
                }
            }
        }

        if (count > 0)
        {
            steer /= count;
            steer = steer.normalized * separationStrength;
        }

        return steer;
    }
}
