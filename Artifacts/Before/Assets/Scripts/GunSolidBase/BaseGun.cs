using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseGun : MonoBehaviour, IWeapon, IReloadable, IBulletTracer
{
    public string GunTypeID;

    [Tooltip("Damage dealt by each shot.")]
    public float damage = 5f;

    public int ammo = 30;
    public int currentAmmo = 30;

    [Tooltip("Shots per second.")]
    public float fireRate = 10f;

    public virtual bool IsAutomatic => false;

    [Tooltip("Maximum hitscan distance.")]
    public float maxRangeGun = 100f;

    protected float normalFOV;
    SwayData IWeapon.swayData => swayData;

    public SwayData swayData;
    [SerializeField] protected RecoilData recoilData;
    public virtual RecoilData Recoil => recoilData;

    public LayerMask layerMask;

    [Header("Shot origin")]
    [Tooltip("Optional exact muzzle transform. If omitted, the front of the weapon renderers is used.")]
    [SerializeField] private Transform muzzleTransform;

    [Header("Visual feedback")]
    [SerializeField] private GameObject muzzleFlashPrefab;
    [SerializeField] private GameObject stoneImpactPrefab;
    [SerializeField] private GameObject metalImpactPrefab;
    [SerializeField] private GameObject woodImpactPrefab;
    [SerializeField] private GameObject fleshImpactPrefab;

    [Header("Audio feedback")]
    [SerializeField] private AudioSource shotAudioSource;
    [SerializeField] private AudioClip[] shotClips;
    [SerializeField, Range(0f, 1f)] private float shotVolume = 0.8f;
    [SerializeField] private Vector2 shotPitchRange = new Vector2(0.96f, 1.04f);
    [Tooltip("Creates a temporary synthesized shot when no gunshot clips are assigned.")]
    [SerializeField] private bool useProceduralAudioFallback = true;

    protected RaycastHit hit;
    protected LineRenderer lr;

    [Tooltip("How long the tracer remains visible.")]
    [SerializeField] private float duration = 0.045f;

    protected Vector3 _kickbackOffset;
    protected Vector3 _kickbackVelocity;
    private Vector3 appliedPhysicalKickback;
    private Vector3 viewmodelKickEuler;
    private Vector3 viewmodelKickVelocity;
    private Quaternion appliedViewmodelKick = Quaternion.identity;

    [field: SerializeField] public virtual float wallDamage { get; set; } = 300f;
    [field: SerializeField] public virtual float shakeDuration { get; set; } = 0.05f;
    [field: SerializeField] public virtual float shakeMagnitude { get; set; } = 0.01f;

    private readonly Dictionary<GameObject, EffectPool> effectPools = new Dictionary<GameObject, EffectPool>();
    private Transform feedbackPoolRoot;
    private AudioClip proceduralShotClip;
    private AudioClip dryFireClip;
    private CameraShake cameraShake;
    private Camera aimCamera;
    private Coroutine traceRoutine;
    private readonly Dictionary<GameObject, Coroutine> pooledEffectRoutines = new Dictionary<GameObject, Coroutine>();
    private float nextDryFireTime;

    public abstract void Use();

    public virtual void Awake()
    {
        Camera activeAimCamera = GetAimCamera();
        normalFOV = activeAimCamera != null ? activeAimCamera.fieldOfView : 60f;
        lr = GetComponent<LineRenderer>();
        GunRecoil.EnsureExists();

        if (activeAimCamera != null)
        {
            cameraShake = activeAimCamera.GetComponent<CameraShake>();
            if (cameraShake == null)
                cameraShake = activeAimCamera.gameObject.AddComponent<CameraShake>();
        }

        EnsureAudioSource();
    }

    protected virtual void OnDisable()
    {
        StopAllCoroutines();

        if (lr != null)
            lr.enabled = false;

        foreach (EffectPool pool in effectPools.Values)
            pool.DeactivateAll();

        pooledEffectRoutines.Clear();
    }

    protected virtual void OnDestroy()
    {
        if (feedbackPoolRoot != null)
            Destroy(feedbackPoolRoot.gameObject);

        if (proceduralShotClip != null)
            Destroy(proceduralShotClip);

        if (dryFireClip != null)
            Destroy(dryFireClip);
    }

    protected bool TryStartShot(ref float lastShotTime)
    {
        if (currentAmmo <= 0)
        {
            PlayDryFire();
            return false;
        }

        float secondsPerShot = 1f / Mathf.Max(0.01f, fireRate);
        if (Time.time < lastShotTime + secondsPerShot)
            return false;

        lastShotTime = Time.time;
        currentAmmo--;
        return true;
    }

    /// <summary>
    /// Aims from the centre of the camera and checks the path from the physical
    /// muzzle to the target, so the crosshair is truthful and nearby cover blocks shots.
    /// </summary>
    protected bool FireHitscan(bool isAiming, Transform visualMuzzle)
    {
        Camera activeAimCamera = GetAimCamera();
        Transform aimTransform = activeAimCamera != null ? activeAimCamera.transform : transform;
        Vector3 shotDirection = GetShootDirection(isAiming, aimTransform);
        Vector3 muzzlePosition = ResolveMuzzlePosition(visualMuzzle, shotDirection, aimTransform.position);

        bool cameraHasHit = Physics.Raycast(
            aimTransform.position,
            shotDirection,
            out RaycastHit cameraHit,
            maxRangeGun,
            layerMask,
            QueryTriggerInteraction.Collide);

        Vector3 aimPoint = cameraHasHit
            ? cameraHit.point
            : aimTransform.position + shotDirection * maxRangeGun;

        Vector3 muzzleToTarget = aimPoint - muzzlePosition;
        float muzzleDistance = muzzleToTarget.magnitude;
        RaycastHit muzzleHit = default;
        bool muzzleHasHit = false;
        if (muzzleDistance > 0.001f)
        {
            muzzleHasHit = Physics.Raycast(
                muzzlePosition,
                muzzleToTarget / muzzleDistance,
                out muzzleHit,
                muzzleDistance + 0.05f,
                layerMask,
                QueryTriggerInteraction.Collide);
        }

        bool hasHit = muzzleHasHit || cameraHasHit;
        if (hasHit)
        {
            hit = muzzleHasHit ? muzzleHit : cameraHit;
            aimPoint = hit.point;
            shotDirection = (aimPoint - muzzlePosition).normalized;
            HandleHit(hit, damage, shotDirection);
        }

        Play(muzzlePosition, aimPoint);
        PlayShotFeedback(muzzlePosition, shotDirection);
        return hasHit;
    }

    public virtual void ApplyRecoil()
    {
        if (recoilData == null)
            return;

        GunRecoil recoilController = GunRecoil.EnsureExists();
        recoilController.ApplyRecoil(recoilData);
        TriggerViewmodelKick();

        if (recoilData.playerImpulseForce <= 0f)
            return;

        IImpulse impulseReceiver = GetComponentInParent<IImpulse>();
        Camera activeAimCamera = GetAimCamera();
        if (impulseReceiver != null && activeAimCamera != null)
            impulseReceiver.ApplyImpulse(-activeAimCamera.transform.forward * recoilData.playerImpulseForce);
    }

    protected Vector3 GetShootDirection(bool isAiming, Transform aimTransform)
    {
        float spreadDegrees = recoilData == null
            ? 0f
            : (isAiming ? recoilData.spreadADS : recoilData.spreadHip);

        Vector2 spread = Random.insideUnitCircle * Mathf.Tan(spreadDegrees * Mathf.Deg2Rad);
        return (aimTransform.forward + aimTransform.right * spread.x + aimTransform.up * spread.y).normalized;
    }

    public abstract void Reload();

    protected void TriggerPhysicalKickback(Transform holder)
    {
        if (holder == null || recoilData == null)
            return;

        _kickbackOffset = Vector3.back * recoilData.kickbackDistance;
        _kickbackVelocity = Vector3.zero;
    }

    protected void HandlePhysicalKickback(Transform holder)
    {
        if (holder == null || recoilData == null)
            return;

        // Remove the previous frame offset first so the shotgun cannot drift.
        holder.position -= appliedPhysicalKickback;
        _kickbackOffset = Vector3.SmoothDamp(
            _kickbackOffset,
            Vector3.zero,
            ref _kickbackVelocity,
            1f / Mathf.Max(1f, recoilData.returnSpeed));

        appliedPhysicalKickback = holder.TransformDirection(_kickbackOffset);
        holder.position += appliedPhysicalKickback;
    }

    protected void BeginViewmodelRecoilFrame(Transform holder)
    {
        if (holder == null)
            return;

        holder.rotation *= Quaternion.Inverse(appliedViewmodelKick);
        appliedViewmodelKick = Quaternion.identity;
    }

    protected void EndViewmodelRecoilFrame(Transform holder)
    {
        if (holder == null || recoilData == null)
            return;

        appliedViewmodelKick = Quaternion.Euler(viewmodelKickEuler);
        holder.rotation *= appliedViewmodelKick;
        viewmodelKickEuler = Vector3.SmoothDamp(
            viewmodelKickEuler,
            Vector3.zero,
            ref viewmodelKickVelocity,
            1f / Mathf.Max(1f, recoilData.returnSpeed),
            Mathf.Infinity,
            Time.deltaTime);
    }

    private void TriggerViewmodelKick()
    {
        float side = Random.Range(-recoilData.weaponKickSide, recoilData.weaponKickSide);
        float roll = recoilData.weaponKickSide > 0.001f
            ? -side / recoilData.weaponKickSide * recoilData.weaponKickRoll
            : Random.Range(-recoilData.weaponKickRoll, recoilData.weaponKickRoll);

        viewmodelKickEuler.x = Mathf.Max(
            viewmodelKickEuler.x - recoilData.weaponKickUp,
            -recoilData.weaponKickUp * 1.4f);
        viewmodelKickEuler.y = Mathf.Clamp(
            viewmodelKickEuler.y + side,
            -recoilData.weaponKickSide * 1.5f,
            recoilData.weaponKickSide * 1.5f);
        viewmodelKickEuler.z = Mathf.Clamp(
            viewmodelKickEuler.z + roll,
            -recoilData.weaponKickRoll * 1.5f,
            recoilData.weaponKickRoll * 1.5f);
        viewmodelKickVelocity = Vector3.zero;
    }

    private IEnumerator DoTrace(Vector3 origin, Vector3 destination)
    {
        if (lr == null)
            yield break;

        lr.SetPosition(0, origin);
        lr.SetPosition(1, destination);
        lr.enabled = true;
        yield return new WaitForSeconds(duration);
        lr.enabled = false;
        traceRoutine = null;
    }

    public void Play(Vector3 origin, Vector3 destination)
    {
        if (lr == null)
            return;

        if (traceRoutine != null)
            StopCoroutine(traceRoutine);

        traceRoutine = StartCoroutine(DoTrace(origin, destination));
    }

    public void ApplyWeaponStat(WeaponStat stat, float value)
    {
        switch (stat)
        {
            case WeaponStat.AmmoCapacity:
                ammo += (int)value;
                currentAmmo = Mathf.Min(currentAmmo, ammo);
                break;
            case WeaponStat.FireRate:
                fireRate += value;
                break;
            case WeaponStat.Damage:
                damage += value;
                break;
            case WeaponStat.RecoilKickUp:
                if (recoilData != null)
                    recoilData.recoilKickUp = Mathf.Max(0f, recoilData.recoilKickUp - value);
                break;
        }
    }

    protected void HandleHit(RaycastHit raycastHit, float dealtDamage, Vector3 shotDirection)
    {
        IDamageable damageable = raycastHit.collider.GetComponent<IDamageable>();
        if (damageable == null)
            damageable = raycastHit.collider.GetComponentInParent<IDamageable>();

        PlayImpactFeedback(raycastHit, damageable != null);

        if (damageable != null)
            damageable.TakeDamage(dealtDamage);
        IHitReactable hitReactable = raycastHit.collider.GetComponent<IHitReactable>();
        if (hitReactable == null)
            hitReactable = raycastHit.collider.GetComponentInParent<IHitReactable>();

        if (hitReactable != null)
            hitReactable.ReactToHit(raycastHit, shotDirection, dealtDamage);

        IRaycastHitHandler hitHandler = raycastHit.collider.GetComponent<IRaycastHitHandler>();
        if (hitHandler == null)
            hitHandler = raycastHit.collider.GetComponentInParent<IRaycastHitHandler>();

        if (hitHandler != null)
        {
            hitHandler.HandleRaycastHit(raycastHit, dealtDamage);
            return;
        }

        Rigidbody body = raycastHit.collider.attachedRigidbody;
        if (body != null && !body.isKinematic)
            body.AddForceAtPosition(shotDirection * wallDamage, raycastHit.point, ForceMode.Force);
    }

    private Vector3 ResolveMuzzlePosition(Transform fallback, Vector3 direction, Vector3 cameraPosition)
    {
        if (muzzleTransform != null)
            return muzzleTransform.position;

        Vector3 candidate = fallback != null ? fallback.position : transform.position;
        float bestProjection = Vector3.Dot(candidate, direction);
        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        foreach (Renderer weaponRenderer in renderers)
        {
            if (weaponRenderer is LineRenderer || weaponRenderer is ParticleSystemRenderer)
                continue;

            if (!weaponRenderer.enabled)
                continue;

            Bounds bounds = weaponRenderer.bounds;
            float projectedExtent =
                Mathf.Abs(direction.x) * bounds.extents.x +
                Mathf.Abs(direction.y) * bounds.extents.y +
                Mathf.Abs(direction.z) * bounds.extents.z;
            float projection = Vector3.Dot(bounds.center, direction) + projectedExtent;

            if (projection > bestProjection)
            {
                bestProjection = projection;
                candidate = bounds.center + direction * projectedExtent * 0.9f;
            }
        }

        return Vector3.Distance(candidate, cameraPosition) <= 3f
            ? candidate
            : (fallback != null ? fallback.position : transform.position);
    }

    protected Camera GetAimCamera()
    {
        if (aimCamera != null)
            return aimCamera;

        int weaponLayer = LayerMask.NameToLayer("Weapon");
        Camera[] playerCameras = transform.root.GetComponentsInChildren<Camera>(true);

        foreach (Camera candidate in playerCameras)
        {
            if (candidate.enabled && candidate.gameObject.activeInHierarchy && candidate.gameObject.layer != weaponLayer)
            {
                aimCamera = candidate;
                return aimCamera;
            }
        }

        foreach (Camera candidate in playerCameras)
        {
            if (candidate.gameObject.layer != weaponLayer)
            {
                aimCamera = candidate;
                return aimCamera;
            }
        }

        aimCamera = Camera.main;
        return aimCamera;
    }

    private void PlayShotFeedback(Vector3 muzzlePosition, Vector3 direction)
    {
        PlayPooledEffect(muzzleFlashPrefab, muzzlePosition, Quaternion.LookRotation(direction), 4, 0.06f);
        PlayShotAudio();

        if (cameraShake == null)
        {
            Camera activeAimCamera = GetAimCamera();
            if (activeAimCamera != null)
            {
                cameraShake = activeAimCamera.GetComponent<CameraShake>();
                if (cameraShake == null)
                    cameraShake = activeAimCamera.gameObject.AddComponent<CameraShake>();
            }
        }

        if (cameraShake != null)
            cameraShake.AddImpulse(shakeDuration, shakeMagnitude);
    }

    private void PlayImpactFeedback(RaycastHit raycastHit, bool hitDamageable)
    {
        GameObject prefab = SelectImpactPrefab(raycastHit.collider, hitDamageable);
        if (prefab == null)
            return;

        Quaternion rotation = Quaternion.LookRotation(raycastHit.normal);
        PlayPooledEffect(prefab, raycastHit.point + raycastHit.normal * 0.01f, rotation, 10, 0.3f);
    }

    private GameObject SelectImpactPrefab(Collider hitCollider, bool hitDamageable)
    {
        string hint = hitCollider.name;
        if (hitCollider.sharedMaterial != null)
            hint += " " + hitCollider.sharedMaterial.name;

        Renderer surfaceRenderer = hitCollider.GetComponent<Renderer>();
        if (surfaceRenderer == null)
            surfaceRenderer = hitCollider.GetComponentInParent<Renderer>();
        if (surfaceRenderer != null && surfaceRenderer.sharedMaterial != null)
            hint += " " + surfaceRenderer.sharedMaterial.name;

        hint = hint.ToLowerInvariant();

        if (ContainsAny(hint, "metal", "steel", "iron", "robot", "machine"))
            return metalImpactPrefab != null ? metalImpactPrefab : stoneImpactPrefab;
        if (ContainsAny(hint, "wood", "timber", "madera"))
            return woodImpactPrefab != null ? woodImpactPrefab : stoneImpactPrefab;
        if (hitDamageable || ContainsAny(hint, "flesh", "skin", "enemy"))
            return fleshImpactPrefab != null ? fleshImpactPrefab : stoneImpactPrefab;

        return stoneImpactPrefab;
    }

    private static bool ContainsAny(string source, params string[] values)
    {
        foreach (string value in values)
        {
            if (source.Contains(value))
                return true;
        }

        return false;
    }

    private void PlayPooledEffect(GameObject prefab, Vector3 position, Quaternion rotation, int poolSize, float maximumLifetime)
    {
        if (prefab == null)
            return;

        if (!effectPools.TryGetValue(prefab, out EffectPool pool))
        {
            if (feedbackPoolRoot == null)
            {
                feedbackPoolRoot = new GameObject(name + " Feedback Pool").transform;
                feedbackPoolRoot.position = Vector3.zero;
            }

            pool = new EffectPool(prefab, poolSize, feedbackPoolRoot);
            effectPools.Add(prefab, pool);
        }

        GameObject instance = pool.Play(position, rotation, maximumLifetime);
        if (pooledEffectRoutines.TryGetValue(instance, out Coroutine previousRoutine) && previousRoutine != null)
            StopCoroutine(previousRoutine);

        pooledEffectRoutines[instance] = StartCoroutine(DisablePooledEffect(instance, maximumLifetime));
    }

    private IEnumerator DisablePooledEffect(GameObject instance, float lifetime)
    {
        yield return new WaitForSeconds(lifetime);

        if (instance != null)
            instance.SetActive(false);
        pooledEffectRoutines.Remove(instance);
    }

    private void EnsureAudioSource()
    {
        if (shotAudioSource == null)
            shotAudioSource = GetComponent<AudioSource>();

        if (shotAudioSource == null)
            shotAudioSource = gameObject.AddComponent<AudioSource>();

        shotAudioSource.playOnAwake = false;
        shotAudioSource.loop = false;
        shotAudioSource.spatialBlend = 0f;
        shotAudioSource.dopplerLevel = 0f;
    }

    private void PlayShotAudio()
    {
        if (shotAudioSource == null)
            return;

        AudioClip clip = null;
        if (shotClips != null && shotClips.Length > 0)
            clip = shotClips[Random.Range(0, shotClips.Length)];

        if (clip == null && useProceduralAudioFallback)
        {
            if (proceduralShotClip == null)
                proceduralShotClip = CreateProceduralShotClip(GunTypeID, IsAutomatic);
            clip = proceduralShotClip;
        }

        if (clip == null)
            return;

        shotAudioSource.pitch = Random.Range(shotPitchRange.x, shotPitchRange.y);
        shotAudioSource.PlayOneShot(clip, shotVolume);
    }

    private void PlayDryFire()
    {
        if (Time.time < nextDryFireTime || shotAudioSource == null)
            return;

        nextDryFireTime = Time.time + 0.2f;
        if (dryFireClip == null)
            dryFireClip = CreateDryFireClip();

        shotAudioSource.pitch = Random.Range(0.96f, 1.04f);
        shotAudioSource.PlayOneShot(dryFireClip, 0.45f);
    }

    private static AudioClip CreateProceduralShotClip(string weaponId, bool automatic)
    {
        const int sampleRate = 44100;
        float clipLength = automatic ? 0.14f : 0.22f;
        int sampleCount = Mathf.CeilToInt(sampleRate * clipLength);
        float[] samples = new float[sampleCount];
        int seed = string.IsNullOrEmpty(weaponId) ? 9137 : weaponId.GetHashCode();
        System.Random random = new System.Random(seed);
        float filteredNoise = 0f;
        float peak = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float rawNoise = (float)(random.NextDouble() * 2.0 - 1.0);
            filteredNoise += (rawNoise - filteredNoise) * 0.18f;

            float crack = rawNoise * Mathf.Exp(-t * 75f);
            float bodyFrequency = Mathf.Lerp(125f, 62f, t / clipLength);
            float body = Mathf.Sin(2f * Mathf.PI * bodyFrequency * t) * Mathf.Exp(-t * 18f);
            float tail = filteredNoise * Mathf.Exp(-t * (automatic ? 28f : 20f));
            float mechanical = Mathf.Sin(2f * Mathf.PI * 850f * t) * Mathf.Exp(-t * 60f);

            float sample = crack * 0.72f + body * (automatic ? 0.52f : 0.72f) + tail * 0.56f + mechanical * 0.1f;
            samples[i] = sample;
            peak = Mathf.Max(peak, Mathf.Abs(sample));
        }

        if (peak > 0f)
        {
            float normalization = 0.88f / peak;
            for (int i = 0; i < samples.Length; i++)
                samples[i] *= normalization;
        }

        AudioClip clip = AudioClip.Create("Procedural " + weaponId + " Shot", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreateDryFireClip()
    {
        const int sampleRate = 44100;
        const float clipLength = 0.045f;
        int sampleCount = Mathf.CeilToInt(sampleRate * clipLength);
        float[] samples = new float[sampleCount];
        System.Random random = new System.Random(7719);

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            float click = noise * Mathf.Exp(-t * 135f);
            float metal = Mathf.Sin(2f * Mathf.PI * 1550f * t) * Mathf.Exp(-t * 95f);
            samples[i] = (click * 0.6f + metal * 0.4f) * 0.5f;
        }

        AudioClip clip = AudioClip.Create("Procedural Dry Fire", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private sealed class EffectPool
    {
        private readonly GameObject[] instances;
        private int nextIndex;

        public EffectPool(GameObject prefab, int size, Transform root)
        {
            instances = new GameObject[Mathf.Max(1, size)];
            for (int i = 0; i < instances.Length; i++)
            {
                GameObject instance = Object.Instantiate(prefab, root);
                instance.name = prefab.name + " (Pooled)";

                // Unity's Particle Pack impact prefabs include a visible demo
                // surface. Only particle renderers belong to the runtime VFX.
                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer renderer in renderers)
                {
                    if (!(renderer is ParticleSystemRenderer))
                        renderer.enabled = false;
                }

                // Impact VFX must never create invisible physical obstacles.
                Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
                foreach (Collider collider in colliders)
                    collider.enabled = false;

                Rigidbody[] rigidbodies = instance.GetComponentsInChildren<Rigidbody>(true);
                foreach (Rigidbody body in rigidbodies)
                {
                    body.detectCollisions = false;
                    body.isKinematic = true;
                }

                ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
                foreach (ParticleSystem particle in particles)
                {
                    ParticleSystem.MainModule main = particle.main;
                    main.loop = false;
                    main.playOnAwake = false;
                }

                instance.SetActive(false);
                instances[i] = instance;
            }
        }

        public GameObject Play(Vector3 position, Quaternion rotation, float maximumLifetime)
        {
            GameObject instance = instances[nextIndex];
            nextIndex = (nextIndex + 1) % instances.Length;

            instance.SetActive(false);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);

            ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particle in particles)
            {
                ParticleSystem.MainModule main = particle.main;
                main.loop = false;
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Play(true);
            }

            return instance;
        }

        public void DeactivateAll()
        {
            foreach (GameObject instance in instances)
            {
                if (instance == null)
                    continue;

                ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
                foreach (ParticleSystem particle in particles)
                    particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                instance.SetActive(false);
            }
        }
    }
}
