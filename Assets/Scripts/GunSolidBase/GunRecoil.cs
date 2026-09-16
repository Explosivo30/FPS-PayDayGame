using UnityEngine;

/// <summary>Single-consumer angular recoil. Mouse compensation consumes recovery debt.</summary>
public class GunRecoil : MonoBehaviour
{
    public static GunRecoil Instance;
    private Vector2 current, target;
    private float lastShot = float.NegativeInfinity;
    private float snap = 32f, delay = .14f, recovery = 9f;
    private int consumedFrame = -1;
    public Vector2 Offset => current;
    public static GunRecoil EnsureExists()
    {
        if (Instance != null) return Instance;
        Instance = FindAnyObjectByType<GunRecoil>();
        if (Instance == null) Instance = new GameObject("Gun Recoil Runtime").AddComponent<GunRecoil>();
        return Instance;
    }
    private void Awake() { if (Instance != null && Instance != this) { Destroy(this); return; } Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; }
    public void ResetRecoil() { current = target = Vector2.zero; lastShot = float.NegativeInfinity; consumedFrame = -1; }
    public void ApplyRecoil(RecoilData data) { ApplyRecoil(data, 1f, .1f); }
    public void ApplyRecoil(RecoilData data, float multiplier, float shotInterval)
    { ApplyRecoilAtTime(data, multiplier, shotInterval, Time.time); }
    public void ApplyRecoilAtTime(RecoilData data, float multiplier, float shotInterval, float now)
    {
        if (data == null || WeaponAction.CombatPaused) return;
        float variation = Random.Range(1f - data.verticalKickVariation, 1f + data.verticalKickVariation);
        target += new Vector2(Random.Range(-data.recoilKickSide, data.recoilKickSide), data.recoilKickUp * variation) * multiplier;
        target.x = Mathf.Clamp(target.x, -data.maxHorizontalRecoil, data.maxHorizontalRecoil);
        target.y = Mathf.Clamp(target.y, 0, data.maxVerticalRecoil);
        snap = data.recoilSnappiness;
        delay = Mathf.Max(data.recoilReturnDelay, shotInterval * 1.3f);
        recovery = data.recoilReturnSpeed; lastShot = now;
    }
    public Vector2 Consume(Vector2 inputDegrees)
    {
        if (consumedFrame == Time.frameCount || WeaponAction.CombatPaused) return Vector2.zero;
        consumedFrame = Time.frameCount;
        return Step(Time.deltaTime, Time.time, inputDegrees);
    }
    public Vector2 Step(float dt, float now, Vector2 inputDegrees)
    {
        // Forget the part of the kick already pulled down by the player.
        float compensated = Mathf.Min(current.y, Mathf.Max(0, -inputDegrees.y));
        current.y -= compensated; target.y = Mathf.Max(0, target.y - compensated);
        if (current.x * inputDegrees.x < 0)
        {
            float side = Mathf.Sign(current.x) * Mathf.Min(Mathf.Abs(current.x), Mathf.Abs(inputDegrees.x));
            current.x -= side; target.x -= side;
        }
        Vector2 previous = current;
        if (now >= lastShot + delay) target = Vector2.MoveTowards(target, Vector2.zero, recovery * dt);
        current = Vector2.Lerp(current, target, 1f - Mathf.Exp(-Mathf.Max(1f, snap) * dt));
        return current - previous;
    }
    public Vector2 GetRecoilOffset() { return Consume(Vector2.zero); }
}
