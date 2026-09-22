using UnityEngine;

/// <summary>Telegraphed local hazard, with solid cover and circulation routes outside its radius.</summary>
public class RootPulse : MonoBehaviour
{
    public Transform ring;
    public Renderer emitter;
    public float radius = 2.6f, interval = 15f, warning = 2f, active = 1.4f, phase;
    public float damagePerSecond = 10f;
    MaterialPropertyBlock properties;
    float hitTimer;
    void Awake() { properties = new MaterialPropertyBlock(); }
    void Update()
    {
        if (WeaponAction.CombatPaused || TowerSession.Instance == null || GameManager.Instance.CurrentWave == 0) return;
        float t = (Time.time + phase) % interval;
        bool warn = t > interval - warning - active;
        bool hot = t > interval - active;
        ring.gameObject.SetActive(warn);
        if (warn)
        {
            float pulse = hot ? 1 : .7f + .3f * Mathf.Sin(Time.time * 12);
            ring.localScale = new Vector3(radius * 2, .035f, radius * 2) * pulse;
        }
        properties.SetColor("_EmissionColor", hot ? new Color(3,.25f,.02f) : warn ? new Color(2,1,.02f) : new Color(.03f,.45f,.25f));
        emitter.SetPropertyBlock(properties);
        hitTimer -= Time.deltaTime;
        if (hot && hitTimer <= 0)
        {
            var player = TowerSession.Instance.player;
            var delta = player.transform.position - transform.position;
            if (new Vector2(delta.x, delta.z).magnitude < radius && Mathf.Abs(delta.y) < 2)
                player.TakeDamage(damagePerSecond * .25f,transform.position,"Pulso de una raíz energética");
            hitTimer = .25f;
        }
    }
}
