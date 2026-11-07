using UnityEngine;

/// <summary>Veteran identity and spatial warning sounds. Geometry is authored in the dedicated model prefabs.</summary>
[DefaultExecutionOrder(90)]
public sealed class EnemySignals : MonoBehaviour
{
    AudioSource source;
    AudioClip normalTone, veteranTone;
    NormalEnemyStateMachine owner;
    bool paused;
    public int VeteranParts { get; private set; }
    public bool HasVeteranModel { get; private set; }
    public void SetVeteran()
    {
        if (HasVeteranModel) return;
        var rig = VeteranRig.Install(owner);
        if (rig == null) return;
        HasVeteranModel = true;
        VeteranParts = rig.GetComponentsInChildren<MeshRenderer>().Length;
    }
    void Awake()
    {
        owner = GetComponent<NormalEnemyStateMachine>();
        source = gameObject.AddComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 1;
        source.minDistance = 3; source.maxDistance = 28; source.rolloffMode = AudioRolloffMode.Linear; source.volume = .3f;
        normalTone = Tone(480, .18f, false); veteranTone = Tone(owner.IsMelee ? 180 : 780, owner.IsMelee ? .65f : .4f, true);
    }
    public static AudioClip Tone(float hz, float duration, bool doubleTone)
    {
        const int rate = 22050; var data = new float[Mathf.CeilToInt(rate * duration)];
        for (int i = 0; i < data.Length; i++)
        {
            float t = (float)i / rate, envelope = Mathf.Sin(Mathf.PI * i / data.Length);
            float gate = doubleTone ? (.45f + .55f * Mathf.Sin(t * 45) * Mathf.Sin(t * 45)) : 1;
            data[i] = Mathf.Sin(2 * Mathf.PI * (hz * t + 160 * t * t)) * envelope * gate * .3f;
        }
        var clip = AudioClip.Create("Robot warning", data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
    }
    public void Attack(bool special) { if (!WeaponAction.CombatPaused) source.PlayOneShot(special ? veteranTone : normalTone); }
    void Update()
    {
        if (paused != WeaponAction.CombatPaused) { paused = WeaponAction.CombatPaused; if (paused) source.Pause(); else source.UnPause(); }
        if (owner.IsDead) source.Stop();
    }
    void OnDestroy()
    {
        if (normalTone != null) Destroy(normalTone); if (veteranTone != null) Destroy(veteranTone);
    }
}
