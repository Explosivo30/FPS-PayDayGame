using UnityEngine;

public class GunRecoil : MonoBehaviour
{
    public static GunRecoil Instance;

    private Vector2 targetRecoil;
    private Vector2 currentRecoil;
    private Vector2 recoilVelocity;
    private Vector2 frameRecoilDelta;
    private float lastShotTime = float.NegativeInfinity;
    private float snappiness = 22f;
    private float returnDelay = 0.075f;
    private float returnSpeed = 10f;
    public static GunRecoil EnsureExists()
    {
        if (Instance != null)
            return Instance;

        GunRecoil sceneInstance = FindAnyObjectByType<GunRecoil>();
        if (sceneInstance != null)
        {
            Instance = sceneInstance;
            return Instance;
        }

        GameObject runtimeObject = new GameObject("Gun Recoil Runtime");
        Object.DontDestroyOnLoad(runtimeObject);
        return runtimeObject.AddComponent<GunRecoil>();
    }


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ApplyRecoil(RecoilData data)
    {
        if (data == null)
            return;

        float variation = Mathf.Clamp01(data.verticalKickVariation);
        float up = data.recoilKickUp * Random.Range(1f - variation, 1f + variation);
        float side = Random.Range(-data.recoilKickSide, data.recoilKickSide);
        targetRecoil += new Vector2(side, up);

        // PAYDAY-style recoil still accumulates and must be compensated, but it
        // cannot grow forever during a long automatic burst.
        if (data.maxHorizontalRecoil > 0f)
            targetRecoil.x = Mathf.Clamp(targetRecoil.x, -data.maxHorizontalRecoil, data.maxHorizontalRecoil);
        if (data.maxVerticalRecoil > 0f)
            targetRecoil.y = Mathf.Min(targetRecoil.y, data.maxVerticalRecoil);

        lastShotTime = Time.time;
        snappiness = Mathf.Max(1f, data.recoilSnappiness);
        returnDelay = Mathf.Max(0f, data.recoilReturnDelay);
        returnSpeed = Mathf.Max(0.1f, data.recoilReturnSpeed);
    }

    private void Update()
    {
        if (Time.time >= lastShotTime + returnDelay)
            targetRecoil = Vector2.MoveTowards(targetRecoil, Vector2.zero, returnSpeed * Time.deltaTime);

        Vector2 previousRecoil = currentRecoil;
        currentRecoil = Vector2.SmoothDamp(
            currentRecoil,
            targetRecoil,
            ref recoilVelocity,
            1f / snappiness,
            Mathf.Infinity,
            Time.deltaTime);

        // This is an angular delta in degrees, not an input value. It must not be
        // multiplied by deltaTime again in the player look code.
        frameRecoilDelta = currentRecoil - previousRecoil;
    }

    public Vector2 GetRecoilOffset()
    {
        return frameRecoilDelta;
    }
}
