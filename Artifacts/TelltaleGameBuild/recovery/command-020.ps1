$taskFile = 'D:\Unity\FPS-PayDayGame\Assets\Scripts\AI\EnemySignals.cs'
@'
using System.Collections.Generic;
using UnityEngine;

/// <summary>Cosmetic overload assemblies and spatial warnings; no collision, damage or navigation.</summary>
[DefaultExecutionOrder(90)]
public sealed class EnemySignals : MonoBehaviour
{
    AudioSource source;
    AudioClip normalTone, veteranTone;
    Material plate, energy;
    Mesh coil;
    NormalEnemyStateMachine owner;
    bool paused, veteran;
    float chargeUntil;
    readonly List<Cable> cables = new List<Cable>();
    struct Cable { public Transform shape, start, end; }
    public int VeteranParts { get; private set; }
    public bool IsOverloaded => veteran;

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
    public void Attack(bool special)
    {
        if (WeaponAction.CombatPaused) return;
        source.PlayOneShot(special ? veteranTone : normalTone);
        if (special) chargeUntil = Time.time + (owner.IsMelee ? owner.GetComponent<MeleeRobotCombat>().windupSeconds : owner.Perception.Profile.burstWarning);
    }
    public void SetVeteran()
    {
        if (veteran) return;
        var visual = GetComponent<RobotPresentation>()?.visual; if (visual == null) return;
        veteran = true;
        var template = visual.GetComponentInChildren<MeshRenderer>()?.sharedMaterial;
        plate = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        plate.name = "Overload graphite"; plate.SetColor("_BaseColor", new Color(.025f, .055f, .075f));
        if (plate.HasProperty("_EmissionColor")) plate.SetColor("_EmissionColor", Color.black);
        energy = new Material(plate); energy.name = "Overload cyan";
        energy.SetColor("_BaseColor", new Color(.04f, .85f, .95f)); energy.EnableKeyword("_EMISSION");
        energy.SetColor("_EmissionColor", new Color(.015f, .75f, 1f));
        coil = CoilMesh();
        if (owner.IsMelee) BuildBrawler(visual); else BuildSentinel(visual);
    }
    Transform Anchor(Transform parent, string name, Vector3 position)
    {
        var t = new GameObject(name).transform; t.SetParent(parent, false);
        t.localPosition = Metres(parent, position);
        // Assemblies are authored in metres even under the sentinel's scaled imported visual.
        t.localScale = Metres(parent, Vector3.one); return t;
    }
    void BuildBrawler(Transform visual)
    {
        var pose = GetComponent<MeleeRobotPresentation>();
        var torso = Anchor(pose != null && pose.torso != null ? pose.torso : visual, "Overload chest", new Vector3(0, .22f, .25f));
        Part(torso, "Reactor housing", Vector3.zero, new Vector3(.48f, .40f, .16f), plate);
        Ring(torso, "Overload core", new Vector3(0, 0, .10f), .145f, Quaternion.Euler(90, 0, 0));
        Part(torso, "Reactor lens", new Vector3(0, 0, .105f), new Vector3(.17f, .17f, .045f), energy);
        foreach (bool left in new[] { true, false })
        {
            var joint = pose != null ? (left ? pose.leftForearm : pose.rightForearm) : null;
            var arm = Anchor(joint != null ? joint : visual, left ? "Overload forearm L" : "Overload forearm R", Vector3.zero);
            Part(arm, "Coil casing", new Vector3(0, -.17f, 0), new Vector3(.27f, .32f, .28f), plate);
            for (int i = 0; i < 4; i++) Ring(arm, "Forearm coil " + i, new Vector3(0, -.045f - i * .085f, 0), .20f, Quaternion.identity);
            Part(arm, "Reinforced fist", new Vector3(0, -.36f, .02f), new Vector3(.34f, .17f, .34f), plate);
            Part(arm, "Fist conductor", new Vector3(0, -.36f, .20f), new Vector3(.24f, .07f, .035f), energy);
            var upper = pose != null ? (left ? pose.leftArm : pose.rightArm) : null;
            if (upper != null)
            {
                var feed = Anchor(upper, "Arm power feed", Vector3.zero);
                Part(feed, "Power conduit", new Vector3(0, -.16f, -.15f), new Vector3(.055f, .30f, .055f), energy);
                var socket = Anchor(feed, "Shoulder socket", new Vector3(0, -.02f, -.14f));
                var coreSocket = Anchor(torso, "Reactor socket", new Vector3(left ? -.20f : .20f, .10f, -.05f));
                CableBetween(visual, coreSocket, socket);
            }
        }
    }
    void BuildSentinel(Transform visual)
    {
        var assembly = Anchor(visual, "Overload dorsal capacitors", Vector3.zero);
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * .32f;
            Part(assembly, "Capacitor shell", new Vector3(x, 1.35f, -.32f), new Vector3(.25f, .88f, .25f), plate, PrimitiveType.Cube);
            Part(assembly, "Capacitor spine", new Vector3(x, 1.35f, -.46f), new Vector3(.08f, .70f, .06f), energy);
            for (int i = 0; i < 3; i++) Ring(assembly, "Capacitor coil " + i, new Vector3(x, 1.13f + i * .23f, -.32f), .18f, Quaternion.identity);
            Part(assembly, "Capacitor crown", new Vector3(x, 1.84f, -.32f), new Vector3(.12f, .16f, .12f), energy);
            var start = Anchor(assembly, "Capacitor outlet", new Vector3(x, 1.04f, -.30f));
            var presentation = GetComponent<RobotPresentation>();
            var end = Anchor(presentation.turret != null ? presentation.turret : assembly, "Weapon power inlet", new Vector3(side * .16f, 0, -.08f));
            CableBetween(visual, start, end);
        }
        Part(assembly, "Overload visor housing", new Vector3(0, 1.28f, .35f), new Vector3(.60f, .19f, .13f), plate);
        Part(assembly, "Overload visor", new Vector3(0, 1.28f, .43f), new Vector3(.48f, .065f, .025f), energy);
    }
    void CableBetween(Transform parent, Transform start, Transform end)
    {
        var shape = Part(Anchor(parent, "Power cable frame", Vector3.zero), "Power cable", Vector3.zero, Vector3.one, energy, PrimitiveType.Cylinder);
        cables.Add(new Cable { shape = shape, start = start, end = end });
    }
    Transform Part(Transform parent, string name, Vector3 position, Vector3 size, Material material, PrimitiveType type = PrimitiveType.Cube)
    {
        var go = GameObject.CreatePrimitive(type); go.name = name; go.layer = 2;
        go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = size;
        var collider = go.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
        go.GetComponent<Renderer>().sharedMaterial = material; VeteranParts++; return go.transform;
    }
    void Ring(Transform parent, string name, Vector3 position, float radius, Quaternion rotation)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.layer = 2;
        go.transform.SetParent(parent, false); go.transform.localPosition = position;
        go.transform.localRotation = rotation; go.transform.localScale = Vector3.one * radius;
        go.GetComponent<MeshFilter>().sharedMesh = coil; go.GetComponent<MeshRenderer>().sharedMaterial = energy; VeteranParts++;
    }
    static Vector3 Metres(Transform parent, Vector3 value) => new Vector3(value.x / parent.lossyScale.x, value.y / parent.lossyScale.y, value.z / parent.lossyScale.z);
    static Mesh CoilMesh()
    {
        const int segments = 24, tube = 6;
        var vertices = new Vector3[(segments + 1) * (tube + 1)]; var triangles = new int[segments * tube * 6];
        for (int i = 0; i <= segments; i++) for (int j = 0; j <= tube; j++)
        {
            float a = i * Mathf.PI * 2 / segments, b = j * Mathf.PI * 2 / tube;
            float r = 1 + .13f * Mathf.Cos(b);
            vertices[i * (tube + 1) + j] = new Vector3(Mathf.Cos(a) * r, .13f * Mathf.Sin(b), Mathf.Sin(a) * r);
        }
        int index = 0;
        for (int i = 0; i < segments; i++) for (int j = 0; j < tube; j++)
        {
            int a = i * (tube + 1) + j, b = a + tube + 1;
            triangles[index++] = a; triangles[index++] = a + 1; triangles[index++] = b;
            triangles[index++] = a + 1; triangles[index++] = b + 1; triangles[index++] = b;
        }
        var mesh = new Mesh { name = "Overload coil", vertices = vertices, triangles = triangles }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }
    void LateUpdate()
    {
        if (paused != WeaponAction.CombatPaused) { paused = WeaponAction.CombatPaused; if (paused) source.Pause(); else source.UnPause(); }
        if (owner.IsDead) source.Stop();
        if (paused) return;
        if (energy != null) energy.SetColor("_EmissionColor", owner.IsDead ? Color.black : new Color(.015f, .75f, 1f) * (Time.time < chargeUntil ? 3.2f : 1));
        foreach (var cable in cables)
        {
            Vector3 a = cable.start.position, b = cable.end.position, delta = b - a;
            cable.shape.position = (a + b) * .5f;
            if (delta.sqrMagnitude > .0001f) cable.shape.rotation = Quaternion.FromToRotation(Vector3.up, delta);
            cable.shape.localScale = new Vector3(.035f, delta.magnitude * .5f, .035f);
        }
    }
    void OnDestroy()
    {
        if (normalTone != null) Destroy(normalTone); if (veteranTone != null) Destroy(veteranTone);
        if (plate != null) Destroy(plate); if (energy != null) Destroy(energy); if (coil != null) Destroy(coil);
    }
}
'@ | Set-Content -LiteralPath $taskFile -Encoding utf8
Write-Output 'Veteran overload assemblies updated.'