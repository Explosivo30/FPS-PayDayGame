using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public class PlayerStateMachine : StateMachine, IDamageable, IUpgradeable, IImpulse
{
    //Control Start
    [NonSerialized] public InputReader controls;
    //Control End

    //----- START GROUND DETECTION
    [SerializeField] float _height;
    [SerializeField] float _castRadius = 0.49f;
    [SerializeField] float _castLength = 0.1f;
    [SerializeField] float _maxAngle = 45f;
    Vector3 _downDir = Vector3.down;

    [SerializeField] LayerMask _groundMask = int.MinValue;
    public LayerMask GroundMask => _groundMask;

    public Vector3 GroundNormal => _groundNormal;
    Vector3 _groundNormal;
    public bool Grounded => _grounded;
    bool _grounded;

    public Vector3 SlideNormal => _slideNormal;
    Vector3 _slideNormal;
    public bool Sliding => _sliding;
    bool _sliding;
    //----- END GROUND DETECTION


    //----START Character Controller
    [SerializeField] CharacterController cc;

    Vector2 worldInputDir;

    //---- END Character Controller

    //---- START PLAYER OWN MOVEMENT

    [Header("Movement Physics Base")]
    public float maxGroundSpeed = 10f;
    public float maxCrouchSpeed = 5f;
    public float maxAirSpeed = 2f; 
    
    [Tooltip("Base horizontal acceleration")]
    public float groundAcceleration = 100f;
    public float airAcceleration = 12f;

    public float groundFriction = 8f;
    public float airFriction = 0.5f;

    [Header("Jump & Slide")]
    public float jumpForce = 6f;
    public float slideBoost = 15f;
    public float slideFriction = 2f;
    
    [Header("Crouch / Slide Config")]
    public float crouchHeightMultiplier = 0.5f;
    public float slideDuration = 1.0f;
    public float heightTransitionSpeed = 10f;
    private float originalHeight;
    private float originalCamLocalY;
    private float targetHeight;
    [SerializeField] float rotationSpeed = 50f;

    [HideInInspector] public Vector3 PlayerVelocity;
    //---- END PLAYER OWN MOVEMENT

    //----- START PLAYER GRAVITY


    [SerializeField] float _gravityForce = 20f;
    public float GravityForce => _gravityForce;
    public Vector3 GravityDir { get { return _gravityDir; } set { _gravityDir = value.normalized; } }


    [Header("Upgradeable Stats")]
    [Tooltip("Additional speed per level")]
    [SerializeField] private float accelerationIncrement = 2f;
    [Tooltip("Maximum upgrade levels")]
    [SerializeField] private int maxUpgradeLevel = 5;

    private int upgradeLevel = 0;

    public string Id => "player";

    public int Level => upgradeLevel;

    public int MaxLevel => maxUpgradeLevel;

    Vector3 _gravityDir = Vector3.down;

    //----- END PLAYER GRAVITY

    //----- START CAMERA

    public Transform headCam;
    [NonSerialized] public CameraTilt cameraTilt;
    [SerializeField] private float minVerticalAngle = -70f;
    [SerializeField] private float maxVerticalAngle = 70f;

    //----- END CAMERA

    // ------ HP PLAYER
    protected float maxHPPlayer = 100f;
    protected float currentHPPlayer;
    [SerializeField] private Volume damageVolume;
    private Coroutine fadeCoroutine;

    [Header("Timing")]
    [Tooltip("Seconds to fade weight from current (or 0) up to 1")]
    [SerializeField] private float fadeInTime = 0.2f;
    [Tooltip("Seconds to fade weight from 1 down to 0")]
    [SerializeField] private float fadeOutTime = 1f;

    

    // ------ HP PLAYER END

    private void Awake()
    {
        GameManager.Instance.Register(this);    // 'this' implementa IUpgradeable y tiene Id
        cameraTilt = GetComponentInChildren<CameraTilt>();
        if (cameraTilt == null) Debug.LogWarning("NO CAMERA TILT");
        _downDir = _downDir.normalized;
        currentHPPlayer = maxHPPlayer;
        damageVolume.weight = 0f;
        GameManager.Instance.AddPlayerTransforms(transform);
        controls = GetComponent<InputReader>();
        
        originalHeight = cc.height;
        targetHeight = originalHeight;
        originalCamLocalY = headCam.localPosition.y;
    }

    private void Start()
    {
        SwitchState(new PlayerIdleState(this));
    }


    public void GroundDetection()
    {
        if (Physics.SphereCast(transform.position + transform.up * _height, _castRadius, _downDir, out RaycastHit hitInfo, _castLength, _groundMask))
        {
           //TODEBUG Debug.Log(hitInfo.transform.up);
            Debug.DrawRay(hitInfo.point, hitInfo.normal, Color.red, 50f);
            
            if (Vector3.Dot(hitInfo.normal, -_downDir) > Mathf.Sin((90f - _maxAngle) * Mathf.PI / 180f))
            {
                _groundNormal = hitInfo.normal;
                _grounded = true;
                _sliding = false;
                Debug.DrawRay(hitInfo.point, hitInfo.normal, Color.blue, Time.fixedDeltaTime);
                _slideNormal = hitInfo.normal;
            }
            else
            {
                _grounded = false;
                _sliding = true;
                _slideNormal = hitInfo.normal;
            }
            
            // Push player out of slope or floor if clipping slightly
            // We use PlayerVelocity.y for gravity logic naturally
        }
        else
        {
            _grounded = false;
            _groundNormal = Vector3.up;
            _sliding = false;
        }

        //TODEBUG Debug.Log($"Grounded: {Grounded}, Sliding: {Sliding}");
    }

    public Vector2 GetInput()
    {
        Vector2 moveInput;

        moveInput = controls.MovementValue;
        moveInput.Normalize();

        if(moveInput.x > 0.01f)
        {
            cameraTilt.DoTilt(-1f);
        } else if(moveInput.x < -0.01f)
        {
            cameraTilt.DoTilt(1f);
        }
        else
        {
            cameraTilt.DoTilt(0f);
        }

        return moveInput;
    }

    public Vector3 GetCameraRight() 
    {
        Vector3 right = Camera.main.transform.right;
        right.y = 0;
        return right.normalized;
    }
    
    public Vector3 GetCameraForward() 
    {
        Vector3 fore = Camera.main.transform.forward;
        fore.y = 0;
        return fore.normalized;
    }

    public void ApplyFriction(float frictionAmount)
    {
        Vector3 vel = new Vector3(PlayerVelocity.x, 0, PlayerVelocity.z);
        float speed = vel.magnitude;
        if (speed != 0) 
        {
            float drop = speed * frictionAmount * Time.deltaTime;
            float newSpeed = speed - drop;
            if (newSpeed < 0) newSpeed = 0;
            newSpeed /= speed;

            PlayerVelocity.x *= newSpeed;
            PlayerVelocity.z *= newSpeed;
        }
    }

    public void Accelerate(Vector3 targetDirection, float targetMaxSpeed, float acceleration)
    {
        float currentSpeed = Vector3.Dot(PlayerVelocity, targetDirection);
        float addSpeed = targetMaxSpeed - currentSpeed;
        if (addSpeed <= 0) return;

        float accelSpeed = acceleration * Time.deltaTime;
        if (accelSpeed > addSpeed) accelSpeed = addSpeed;

        PlayerVelocity.x += accelSpeed * targetDirection.x;
        PlayerVelocity.z += accelSpeed * targetDirection.z;
    }

    public void ApplyGravityCustom()
    {
        PlayerVelocity.y -= _gravityForce * Time.deltaTime;
    }
    
    public void Jump()
    {
        if(Grounded)
        {
            PlayerVelocity.y = jumpForce;
        }
    }

    public void SetCrouchedScale(bool isCrouched)
    {
        targetHeight = isCrouched ? originalHeight * crouchHeightMultiplier : originalHeight;
    }

    private void UpdateHeight()
    {
        if (cc.height != targetHeight)
        {
            float lastHeight = cc.height;
            cc.height = Mathf.Lerp(cc.height, targetHeight, heightTransitionSpeed * Time.deltaTime);
            cc.radius = cc.height / 2f; // Ensure radius scales safely if needed, or keep radius same if it's small enough

            // Adjust position so we don't fall off or fly
            float heightDiff = lastHeight - cc.height;
            transform.position += new Vector3(0, heightDiff / 2, 0);
            
            // Adjust camera Y smoothly
            float curCamY = headCam.localPosition.y;
            float targetCamY = targetHeight == originalHeight ? originalCamLocalY : (originalCamLocalY * crouchHeightMultiplier * 0.8f);
            headCam.localPosition = new Vector3(headCam.localPosition.x, Mathf.Lerp(curCamY, targetCamY, heightTransitionSpeed * Time.deltaTime), headCam.localPosition.z);
        }
    }

    public bool CanStandUp()
    {
        if (targetHeight == originalHeight) return true; // Already standing or trying to
        // If we are crouched, check upwards if there's roof
        RaycastHit hit;
        float distance = originalHeight - cc.height;
        Vector3 origin = transform.position + Vector3.up * (cc.height / 2f);
        if (Physics.SphereCast(origin, cc.radius * 0.9f, Vector3.up, out hit, distance, GroundMask))
        {
            return false;
        }
        return true;
    }

    public void MovePlayer()
    {
        UpdateHeight();
        
        cc.Move(PlayerVelocity * Time.deltaTime);

        // Ground sticking / reset Y velocity if colliding with ground
        if (Grounded && PlayerVelocity.y <= 0)
        {
            // Pequeña fuerza descendente extra para mantenerse pegado al suelo al bajar pendientes
            PlayerVelocity.y = -2f;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position + transform.up * _height, _castRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere((transform.position + (transform.up * _height)) + -transform.up * _castLength, _castRadius);
    }

    private float verticalRotation = 0f;
    public void PlayerLook()
    {
        Vector2 rotateVector = controls.LookValue;

        float horizontalInput = rotateVector.x; // For character rotation (Y-axis)

        float verticalInput = rotateVector.y;

        Vector2 recoil = GunRecoil.Instance?.GetRecoilOffset() ?? Vector2.zero;

        // Rotate the character around the Y-axis (horizontal input)
        //if (horizontalInput != 0)
        //{
            // Calculate the desired rotation angle
            float rotationAngle = (horizontalInput + recoil.x) * rotationSpeed * Time.deltaTime;
            
            // Apply the rotation around the Y-axis
            transform.Rotate(0f, rotationAngle, 0f);
       // }

        // --- PITCH (rotate camera vertically, clamped) ---
        
        verticalRotation -= (verticalInput + recoil.y) * rotationSpeed * Time.deltaTime;
        verticalRotation = Mathf.Clamp(verticalRotation, minVerticalAngle, maxVerticalAngle);
        headCam.localEulerAngles = new Vector3(verticalRotation, 0f, 0f);

    }

    public void TakeDamage(float amount)
    {

        // Try shield first
        if (TryGetComponent<IShield>(out var shield) && shield.Current > 0f)
        {
            shield.Absorb(amount);
            return;
        }

        currentHPPlayer -= amount;

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

       
        // Start a fresh fade in → out
       

        if (currentHPPlayer <= 0) 
        {
            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);
            damageVolume.weight = 0f;
            //DIE
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
        else
        {
            fadeCoroutine = StartCoroutine(FadeRoutine());
        }
    }

    private IEnumerator FadeRoutine()
    {
        // 1) Fade IN: from current weight to 1
        float start = damageVolume.weight;
        float elapsed = 0f;
        while (elapsed < fadeInTime)
        {
            elapsed += Time.deltaTime;
            damageVolume.weight = Mathf.Lerp(start, 1f, elapsed / fadeInTime);
            yield return null;
        }
        damageVolume.weight = 1f;

        // 2) Fade OUT: from 1 back to 0
        elapsed = 0f;
        while (elapsed < fadeOutTime)
        {
            elapsed += Time.deltaTime;
            damageVolume.weight = Mathf.Lerp(1f, 0f, elapsed / fadeOutTime);
            yield return null;
        }
        damageVolume.weight = 0f;
        fadeCoroutine = null;
    }

    public int GetUpgradeCost()
    {
        return 2 * (Level + 1);
    }

    public void ApplyUpgrade()
    {
        if (upgradeLevel >= maxUpgradeLevel) return;
        upgradeLevel++;
        // Apply the new acceleration value directly to your state machine field
        maxGroundSpeed += accelerationIncrement;
        groundAcceleration += accelerationIncrement * 5f;
        Debug.Log($"Player acceleration upgraded to level {upgradeLevel}. New max speed: {maxGroundSpeed}");
    }

    public void ApplyPlayerStat(PlayerStat stat, float value, bool isPercent)
    {
        switch (stat)
        {
            case PlayerStat.Acceleration:
                maxGroundSpeed = isPercent
                  ? maxGroundSpeed * (1 + value / 100f)
                  : maxGroundSpeed + value;

                groundAcceleration = maxGroundSpeed * 10f; // rough equivalent
                break;
            case PlayerStat.JumpHeight:
                jumpForce = isPercent
                  ? jumpForce * (1 + value / 100f)
                  : jumpForce + value;
                break;
            case PlayerStat.MaxHealth:
                maxHPPlayer = isPercent
                  ? maxHPPlayer * (1 + value / 100f)
                  : maxHPPlayer + value;
                currentHPPlayer = Mathf.Min(currentHPPlayer, maxHPPlayer);
                break;

            case PlayerStat.Shield:
                // Find your Shield component and bump its level
                if (TryGetComponent<Shield>(out var shield))
                    shield.GetNewUpgrade(value,isPercent);  // your IUpgradeable logic
                break;
        }
    }

    public void ApplyImpulse(Vector3 force)
    {
        PlayerVelocity += force;
        
        // Si estamos aplicando una fuerza vertical hacia arriba y actualmente caemos mucho
        // Ayudamos a frenar esa caída para que el impulso sí se note.
        if (force.y > 0 && Grounded == false)
        {
            if (PlayerVelocity.y < 0)
            {
               PlayerVelocity.y += force.y * 0.5f; // extra help to fight negative gravity velocity
            }
        }
    }
}

