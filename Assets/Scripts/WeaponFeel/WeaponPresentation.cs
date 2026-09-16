using UnityEngine;

[DefaultExecutionOrder(80)]
[RequireComponent(typeof(WeaponAction))]
public class WeaponPresentation : MonoBehaviour
{
    public Vector3 hipPosition = new Vector3(.19f, -.19f, .45f);
    public Vector3 aimPosition = new Vector3(0f, -.065f, .43f);
    public Vector3 hipEuler;
    public Transform magazine, bolt;
    public Transform rightGrip, leftGrip;
    public float movementScale = 1f;
    public float kickScale = 1f;
    public float AimBlend { get; private set; }
    public float ReloadGesture { get; private set; }
    public float ShotPulse { get; private set; }
    private WeaponAction action;
    private BaseGun gun;
    private InputReader input;
    private PlayerStateMachine player;
    private Vector3 magazineHome, boltHome, sway, recoilEuler;
    private float kick, stepTime, landing, motion;
    private bool grounded;
    private AudioSource mechanicalAudio;
    private AudioClip click;

    private void Awake()
    {
        action = GetComponent<WeaponAction>(); gun = GetComponent<BaseGun>();
        input = GetComponentInParent<InputReader>(); player = GetComponentInParent<PlayerStateMachine>();
        if (magazine != null) magazineHome = magazine.localPosition;
        if (bolt != null) boltHome = bolt.localPosition;
        mechanicalAudio = gameObject.AddComponent<AudioSource>(); mechanicalAudio.playOnAwake = false; mechanicalAudio.spatialBlend = 0;
        click = CreateClick();
    }
    private void OnEnable()
    {
        if (action == null) action = GetComponent<WeaponAction>();
        action.ShotFired += OnShot;
        action.StateChanged += OnState;
        action.ReloadStep += OnReloadStep;
        kick = ShotPulse = AimBlend = landing = 0; recoilEuler = sway = Vector3.zero;
        transform.localPosition = hipPosition; transform.localRotation = Quaternion.Euler(hipEuler);
    }
    private void OnDisable()
    {
        action.ShotFired -= OnShot; action.StateChanged -= OnState; action.ReloadStep -= OnReloadStep;
        if (magazine != null) magazine.localPosition = magazineHome;
        if (bolt != null) bolt.localPosition = boltHome;
        mechanicalAudio?.Stop();
    }
    private void OnDestroy() { if (click != null) Destroy(click); }
    private void OnState(WeaponActionState state)
    {
        if (state == WeaponActionState.Drawing || state == WeaponActionState.Holstering) OnReloadStep();
    }
    private void OnReloadStep()
    {
        if (mechanicalAudio == null) return;
        mechanicalAudio.pitch = gun is Pistol ? 1.45f : gun is ShotGun ? .78f : 1;
        mechanicalAudio.PlayOneShot(click, .26f);
    }
    private void OnShot()
    {
        var data = gun != null ? gun.Recoil : null;
        float aimScale = Mathf.Lerp(1f, .65f, AimBlend) * kickScale;
        kick = Mathf.Min(kick + (data != null ? data.kickbackDistance : .025f) * aimScale, .1f);
        recoilEuler += new Vector3(-(data != null ? data.weaponKickUp : 1.5f), Random.Range(-.25f,.25f), Random.Range(-.45f,.45f)) * aimScale;
        recoilEuler.x = Mathf.Max(recoilEuler.x, -10f); ShotPulse = 1;
        if (gun == null) OnReloadStep();
    }
    private void LateUpdate()
    {
        if (WeaponAction.CombatPaused) return;
        float dt = Time.deltaTime;
        bool aiming = gun is IAimable a && a.IsAiming && !action.IsBusy;
        AimBlend = Mathf.Lerp(AimBlend, aiming ? 1 : 0, 1f - Mathf.Exp(-16f * dt));
        float speed = player != null ? Vector3.ProjectOnPlane(player.PlayerVelocity, Vector3.up).magnitude : 0;
        float amount = player != null && player.Grounded ? Mathf.Clamp01(speed / 8) : 0;
        motion = Mathf.Lerp(motion, amount, 1f - Mathf.Exp(-10 * dt));
        stepTime += dt * Mathf.Lerp(1.3f, 10f, motion);
        if (player != null)
        {
            if (player.Grounded && !grounded) landing = .032f;
            grounded = player.Grounded;
        }
        Vector2 look = input != null ? input.LookValue : Vector2.zero;
        var desiredSway = new Vector3(Mathf.Clamp(look.y * .08f,-2,2),Mathf.Clamp(-look.x * .08f,-2,2),0);
        sway = Vector3.Lerp(sway, desiredSway, 1f - Mathf.Exp(-12f * dt));
        float motionWeight = Mathf.Lerp(1,.18f,AimBlend) * movementScale;
        Vector3 bob = new Vector3(Mathf.Sin(stepTime) * .009f, Mathf.Cos(stepTime * 2) * .006f, 0) * motion * motionWeight;
        bob.y += Mathf.Sin(Time.time * 1.8f) * .0015f * (1-AimBlend) - landing;
        if (player != null && !player.Grounded) bob.y += Mathf.Clamp(-player.PlayerVelocity.y * .002f,-.025f,.025f);
        float t = action.Progress;
        float lower = action.State == WeaponActionState.Drawing ? 1 - Ease(t) : action.State == WeaponActionState.Holstering ? Ease(t) : 0;
        ReloadGesture = action.State == WeaponActionState.Reloading ? (action.shellReload ? .8f : Mathf.Sin(Mathf.PI * t)) : 0;
        Vector3 pos = Vector3.Lerp(hipPosition, aimPosition, AimBlend) + bob;
        pos += new Vector3(-.045f * ReloadGesture, -.2f * lower - .065f * ReloadGesture, -kick);
        Vector3 rot = Vector3.Lerp(hipEuler, Vector3.zero, AimBlend) + sway * motionWeight + recoilEuler;
        rot += new Vector3(30f * lower + 12f * ReloadGesture, -8f * ReloadGesture, -25f * ReloadGesture);
        if (action.State == WeaponActionState.Attacking)
        {
            float slash = Mathf.Sin(Mathf.Clamp01(t / .6f) * Mathf.PI);
            pos += new Vector3(-.22f * slash, .06f * slash, .15f * slash);
            rot += new Vector3(-25f * slash, -40f * slash, 65f * slash);
        }
        transform.localPosition = pos; transform.localRotation = Quaternion.Euler(rot);
        if (magazine != null) magazine.localPosition = magazineHome + magazine.parent.InverseTransformVector(transform.up * -.24f * ReloadGesture);
        if (bolt != null) bolt.localPosition = boltHome + bolt.parent.InverseTransformVector(-transform.forward * .035f * ShotPulse);
        kick *= Mathf.Exp(-19f * dt); recoilEuler *= Mathf.Exp(-17f * dt); ShotPulse *= Mathf.Exp(-28f * dt); landing *= Mathf.Exp(-13f * dt);
    }
    private static float Ease(float t) { return t*t*(3-2*t); }
    private static AudioClip CreateClick()
    {
        const int rate=22050; var samples=new float[2205]; var rng=new System.Random(47);
        for(int i=0;i<samples.Length;i++) { float t=i/(float)rate; samples[i]=((float)rng.NextDouble()*2-1)*Mathf.Exp(-t*100)*.5f+Mathf.Sin(t*8500)*Mathf.Exp(-t*65)*.25f; }
        var clip=AudioClip.Create("Weapon mechanism",samples.Length,1,rate,false); clip.SetData(samples,0); return clip;
    }
}
