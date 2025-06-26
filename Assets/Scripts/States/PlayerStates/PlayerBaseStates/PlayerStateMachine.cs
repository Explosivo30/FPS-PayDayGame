using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public class PlayerStateMachine : StateMachine, IDamageable, IUpgradeable
{
    //Control Start
    InputReader controls;
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

    //Variables 
    [SerializeField] float _acceleration = 12f;
    [SerializeField] float _targetVelocity = 10f;
    
    [SerializeField, Range(0f, 1f)] float _turnaroundStrength;
    [SerializeField] float rotationSpeed = 50f;
   
    //---- END PLAYER OWN MOVEMENT


    //----- START PLAYER GRAVITY


    [SerializeField] float _gravityForce = 9.81f;
    public Vector3 GravityDir { get { return _gravityDir; } set { _gravityDir = value.normalized; } }


    [Header("Upgradeable Stats")]
    [Tooltip("Base horizontal acceleration")]
    [SerializeField] private float baseAcceleration = 200f;
    [Tooltip("Additional acceleration per level")]
    [SerializeField] private float accelerationIncrement = 500f;
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
    private CameraTilt cameraTilt;
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
        
    }

    private void Start()
    {
        SwitchState(new PlayerIdleState(this));
    }


    public void GroundDetection()
    {
        if (Physics.SphereCast(transform.position + transform.up * _height, _castRadius, _downDir, out RaycastHit hitInfo, _castLength, _groundMask))
        {
            Debug.Log(hitInfo.transform.up);
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
        }
        else
        {
            _grounded = false;
            _groundNormal = Vector3.up;
            _sliding = false;
        }

        Debug.Log($"Grounded: {Grounded}, Sliding: {Sliding}");
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

    public void PlayerHorizontalMovement(Vector2 input)
    {
        float vVel = Vector3.Dot(cc.velocity, GroundNormal);

        Vector2 currentVel2d = Get2dOrientation(Vector3.ProjectOnPlane(cc.velocity, GroundNormal), GroundNormal);


        currentVel2d = Movement(currentVel2d, input);


        Debug.DrawRay(transform.position + transform.up * 0.8f, new Vector3(currentVel2d.x, 0f, currentVel2d.y), Color.green);
        Vector3 outputHorizontal = MultiplyByPlane(currentVel2d, GroundNormal);
        Debug.DrawRay(transform.position + transform.up * 0.8f, outputHorizontal, Color.blue);
        //Debug.Log($"Input: {input}");
        
       
        cc.Move((outputHorizontal + (GroundNormal * vVel)) * Time.deltaTime/**/ );

        

        
    }


    public Vector2 CameraOritentedMovement(Vector2 input)
    {
        Transform cameraTransform = Camera.main.transform;
        Vector3 camRight = cameraTransform.right;
        Vector3 camForward = cameraTransform.forward;

        camForward = input.y * Vector3.ProjectOnPlane(camForward, Vector3.up).normalized;
        camRight = input.x * Vector3.ProjectOnPlane(camRight, Vector3.up).normalized;

        Vector3 camForwardAxis = Vector3.ProjectOnPlane(Vector3.forward, GroundNormal).normalized;
        Vector3 camRightAxis = Vector3.ProjectOnPlane(Vector3.right, GroundNormal).normalized;

        Vector2 output;

        output.x = (Vector3.Dot(camRight + camForward, camRightAxis));
        output.y = (Vector3.Dot(camForward + camRight, camForwardAxis));

        Debug.DrawRay(transform.position + (transform.up * 2.5f), new Vector3(output.x, 0f, output.y), Color.cyan);

        return output;
    }
    /// <summary>
    /// change a vector from a 3d vector to a 2d vector 
    /// </summary>
    public Vector2 Get2dOrientation(Vector3 value, Vector3 normal) // value is any vector, normal always comes in normalized
    {
        // project the value so we are on the plane defined by the normal
        Vector3 modifiedValue = Vector3.ProjectOnPlane(value, normal).normalized;
        // get the right direction 
        Vector3 right = Vector3.Cross(normal, modifiedValue).normalized;
        // get the forward direction translated to the xz plane
        Vector3 inputDir = Vector3.Cross(right, Vector3.up).normalized;
        //add the magnitude back to make it as long as at the start
        Vector2 output = new Vector2(inputDir.x, inputDir.z).normalized * value.magnitude;
        worldInputDir = output;
        return output;
    }

    /// <summary>
    /// transforms a 2d plane into a 3d one based on a normal  
    /// </summary>
    public Vector3 MultiplyByPlane(Vector2 plane, Vector3 planeNormal)
    {
        Vector3 planeDir = new Vector3(plane.x, 0f, plane.y);
        Vector3 translatedRight = Vector3.Cross(planeNormal, planeDir.normalized).normalized;
        Vector3 translatedForward = Vector3.Cross(translatedRight, planeNormal).normalized;
        Vector3 output = translatedForward * plane.magnitude;
        return output;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position + transform.up * _height, _castRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere((transform.position + (transform.up * _height)) + -transform.up * _castLength, _castRadius);
    }

    /// <summary>
    /// Logic for the horizontal movment
    /// </summary>
    /// <param name="currentVelocity">Velocity from the player before beeing modified</param>
    /// <param name="input">Target direction for the player</param>
    /// <returns>new Velocity for the player</returns>
    public Vector2 Movement(Vector2 currentVelocity, Vector2 input)
    {
        //Example

        //reduction of velocity by change of direction
        currentVelocity *= VelocityRemaining(currentVelocity.normalized, input);

        //acceleration
        float targetVelSq = _targetVelocity * _targetVelocity;
        if (currentVelocity.sqrMagnitude < targetVelSq)
        {
            currentVelocity += (input * _acceleration );
            if (currentVelocity.sqrMagnitude > targetVelSq)
            {
                float mag = Mathf.Clamp(currentVelocity.magnitude, 0f, _targetVelocity);
                currentVelocity = mag * currentVelocity.normalized;
            }
        }

        //slide off
        if (Sliding)
        {
            Vector3 slideDirection = Vector3.ProjectOnPlane(Vector3.down, SlideNormal).normalized;
            Vector3 slideVelocity = Vector3.ProjectOnPlane(new Vector3(currentVelocity.x, 0f, currentVelocity.y), SlideNormal);

            // Combine sliding direction with input
            currentVelocity = new Vector2(slideVelocity.x, slideVelocity.z);

        }

        return currentVelocity;
    }

    //can be deleted only used for the example
    public float VelocityRemaining(Vector3 currentVelocity, Vector3 input)
    {
        float dot = Vector2.Dot(currentVelocity.normalized, input.normalized);

        dot += 1f + _turnaroundStrength;
        dot /= 2f + _turnaroundStrength;
        dot = Mathf.Pow(dot, 1f - _turnaroundStrength);
        return dot;
    }


    private Vector3 _horizontalVelocity; // Stores horizontal velocity during sliding
    [SerializeField] float scalarHVelocity = 50f;
    private Vector3 currentForceGravity = Vector3.zero;
    public void ApplyGravity()
    {
        if (Grounded)
        {
            currentForceGravity = (_gravityDir * _gravityForce) * Time.deltaTime;
            // Project gravity onto the ground normal to prevent horizontal movement
            Vector3 groundedGravity = Vector3.ProjectOnPlane(_gravityDir * _gravityForce, GroundNormal.normalized);
            cc.Move((GravityDir * _gravityForce) * Time.deltaTime);

            
        }
        else
        {
            if (Sliding)
            {
                currentForceGravity = Vector3.zero;
                // Calculate sliding direction along the slope
                Vector3 slideDirection = Vector3.ProjectOnPlane(Vector3.down, SlideNormal).normalized;
                _horizontalVelocity += Vector3.ProjectOnPlane(cc.velocity, SlideNormal);
                _horizontalVelocity.y = 0f;
                cc.Move(slideDirection * _gravityForce * Time.deltaTime);
            }
            else
            {
                currentForceGravity +=  (_gravityDir * _gravityForce) * Time.deltaTime;
                // Regular airborne gravity
                cc.Move(( currentForceGravity+ (_horizontalVelocity.normalized*scalarHVelocity)) * Time.deltaTime);

                //cc.Move(((GravityDir * _gravityForce)+ (resetVelo + _horizontalVelocity.normalized)) * Time.deltaTime);
            }
        }
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
        currentHPPlayer -= amount;

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

       
        // Start a fresh fade in→out
       

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
        _acceleration = baseAcceleration + upgradeLevel * accelerationIncrement;
        Debug.Log($"Player acceleration upgraded to level {upgradeLevel}. New acc: {_acceleration}");
    }
}

