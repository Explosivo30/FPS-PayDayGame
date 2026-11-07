#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>Real motor/weapon/opponent integration; controlled traces do not measure human fun.</summary>
public sealed class AlphaDistrictPlaytest : MonoBehaviour
{
    [Serializable]
    public sealed class Sample
    {
        public string phase, collapse;
        public float seconds, x, y, z, health, shield, damage, healed, distance, extraction, nearestEnemy;
        public int zone, alive, pending, spawned, specialists, shots, structuralShots, projectiles, lasers, meleeAttacks, impactKills, within15m, visibleEnemies;
        public bool powerA, powerB, alarm, completed;
    }
    [Serializable]
    public sealed class Trace
    {
        public string name, limitation;
        public bool completed, died, powerA, powerB, alarm, containmentOpened, collapsed;
        public float seconds, distance, shieldDamage, healthDamage, gameHealing;
        public int shots, structuralShots, impactKills, aliveAtExit, maxAlive, maxSpecialists;
        public List<string> damageCauses = new List<string>();
        public List<Sample> samples = new List<Sample>();
    }
    [Serializable]
    sealed class Evidence
    {
        public string status = "Physical contracts and controlled observations; no claim about human fun or difficulty.";
        public string controller = "Real StepMovement, weapons, reload and E-handler; perfect target acquisition and authored route knowledge. No QA healing, freeze or internal teleport.";
        public List<Trace> traces = new List<Trace>();
    }
    readonly List<string> checks = new List<string>(), errors = new List<string>();
    readonly Evidence evidence = new Evidence();
    readonly Dictionary<int, int[]> attacks = new Dictionary<int, int[]>();
    readonly HashSet<int> photographedZones = new HashSet<int>();
    AlphaDistrictDirector director;
    PlayerStateMachine player;
    AlphaDistrictVisuals visuals;
    Camera view;
    Trace active;
    string output, phase = "setup";
    float began, traceBegan, nextSample, lastProgressAt = -1, nextStallReport;
    Vector3 previousPosition, progressPosition, lastDestination;
    bool smoke, finished, legReached;
    int observedZone = -1;
    const int AimMask = ~(1 << 7 | 1 << 8 | 1 << 2);
    const int WorldMask = (1 << 0) | (1 << 3) | (1 << 10);
    static readonly MethodInfo InteractMethod = typeof(PlayerInteraction).GetMethod("Interact", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo RendererIndex = typeof(UniversalAdditionalCameraData).GetField("m_RendererIndex", BindingFlags.Instance | BindingFlags.NonPublic);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var args = Environment.GetCommandLineArgs();
        if ((!args.Contains("-district-qa") && !args.Contains("-district-smoke")) || Object.FindFirstObjectByType<AlphaDistrictPlaytest>() != null) return;
        var go = new GameObject("Alpha district integration evidence");
        DontDestroyOnLoad(go); go.AddComponent<AlphaDistrictPlaytest>();
    }
    void Awake()
    {
        var args = Environment.GetCommandLineArgs(); smoke = args.Contains("-district-smoke");
        int index = Array.IndexOf(args, "-district-output");
        output = Path.GetFullPath(index >= 0 && index + 1 < args.Length ? args[index + 1] :
            smoke ? "Artifacts/AlphaDistrict/Smoke" : "Artifacts/AlphaDistrict/QA");
        Directory.CreateDirectory(output); Application.runInBackground = true;
        Application.logMessageReceived += Log; began = Time.realtimeSinceStartup;
    }
    void Update()
    {
        if (!finished && Time.realtimeSinceStartup - began > 900) { Check(false, "District watchdog (900 seconds)"); Finish(); }
    }
    void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack);
    }
    void Check(bool ok, string label)
    {
        checks.Add((ok ? "PASS " : "FAIL ") + label); Debug.Log(checks.Last()); if (!ok) errors.Add(label);
    }
    void Info(string message) { checks.Add("INFO " + message); Debug.Log(checks.Last()); }
    IEnumerator Until(Func<bool> condition, float seconds, string label)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (!finished && !condition() && Time.realtimeSinceStartup < end) yield return null;
        if (!finished) Check(condition(), label);
    }
    IEnumerator Start()
    {
        yield return Until(() => Object.FindFirstObjectByType<AlphaDistrictDirector>() != null &&
            Object.FindFirstObjectByType<AlphaDistrictDirector>().Ready, 120, "The alpha district loads its real playable core");
        director = Object.FindFirstObjectByType<AlphaDistrictDirector>(); player = director != null ? director.Player : null;
        if (director == null || player == null) { Finish(); yield break; }
        if (smoke) { yield return NormalSmoke(); yield break; }
        Bind(); yield return Prepared();
        Check(director.zones != null && director.zones.Length == 6, "The level exposes six authored districts");
        Check(Object.FindObjectsByType<PlayerStateMachine>(FindObjectsSortMode.None).Length == 1, "Exactly one real player is loaded");
        Check(TowerSession.Instance.CurrentFloor == null && !GameManager.Instance.IsSpawning, "The district does not run tower wave progression");
        Check(!director.PowerA && !director.PowerB && !director.AlarmCalled && !director.Completed, "The three spatial milestones start incomplete");
        VisualContracts();
        if (Environment.GetCommandLineArgs().Contains("-district-collapse-view"))
        {
            yield return CollapseView(); Finish(); yield break;
        }
        yield return RunRoute(false); yield return Restart(); yield return RunRoute(true); yield return Restart();
        Check(!director.PowerA && !director.PowerB && !director.AlarmCalled && !director.Completed &&
            !director.containment.IsOpen && !director.collapse.IsSettled, "Restart restores both structures and all milestones");
        Check(EnemyProjectile.ActiveCount == 0 && CombatHazardBudget.ActiveSalvos == 0 && CombatHazardBudget.ActivePrimary == 0,
            "QA restart leaves no projectile or danger lease before the encounter starts");
        Finish();
    }
    void Bind()
    {
        if (player != null) { player.Damaged -= Damaged; player.Healed -= Healed; }
        director = Object.FindFirstObjectByType<AlphaDistrictDirector>(); player = director != null ? director.Player : null;
        if (player == null) return;
        player.enabled = false; TowerSession.Instance.weapons.AutomatedInput = true;
        player.Damaged += Damaged; player.Healed += Healed;
        view = player.GetComponentsInChildren<Camera>().First(c => c.gameObject.layer != 8);
        visuals = Object.FindFirstObjectByType<AlphaDistrictVisuals>(); lastProgressAt = -1;
    }
    IEnumerator Prepared()
    {
        yield return Until(() => director.collapse != null && director.collapse.Ready && director.containment != null && director.containment.Ready,
            90, "Both real fracture systems are prepared before the encounter");
        yield return null;
    }
    IEnumerator Restart()
    {
        active = null; var previous = director; previous.RestartLevel(); yield return null;
        yield return Until(() => Object.FindFirstObjectByType<AlphaDistrictDirector>() != null &&
            Object.FindFirstObjectByType<AlphaDistrictDirector>() != previous && Object.FindFirstObjectByType<AlphaDistrictDirector>().Ready,
            120, "Restart reconstructs the actual district");
        Bind(); if (director == null || player == null) yield break;
        yield return Prepared(); Check(!player.IsDead && !WeaponAction.CombatPaused, "Restart restores a living, unpaused player");
    }
    void Damaged(PlayerDamage damage)
    {
        if (active == null) return;
        active.shieldDamage += damage.ShieldLoss; active.healthDamage += damage.HealthLoss; active.damageCauses.Add(damage.Cause);
    }
    void Healed(float amount) { if (active != null) active.gameHealing += amount; }
    Transform Marker(string name)
    {
        var value = GameObject.Find(name); Check(value != null, "Authored motor marker exists: " + name);
        return value != null ? value.transform : director.arrival;
    }
    AlphaDistrictTerminal Terminal(int kind)
    {
        var terminal = director.terminals.FirstOrDefault(t => t != null && t.kind == kind);
        Check(terminal != null, "The real interaction terminal exists for kind " + kind); return terminal;
    }
    float RootOffset => player.GetComponent<CharacterController>().height * .5f - player.GetComponent<CharacterController>().center.y + .05f;
    bool Ground(Vector3 root, out Vector3 ground)
    {
        bool ok = NavMesh.SamplePosition(root - Vector3.up * RootOffset, out var sample, .9f, NavMesh.AllAreas);
        ground = sample.position; return ok;
    }
    bool CompletePath(Vector3 from, Vector3 to, out NavMeshPath path, out float length)
    {
        path = new NavMeshPath(); length = 0;
        if (!Ground(from, out var a) || !Ground(to, out var b)) return false;
        bool complete = NavMesh.CalculatePath(a, b, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
        for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        return complete;
    }
    static float Flat(Vector3 a, Vector3 b) => Vector3.ProjectOnPlane(a - b, Vector3.up).magnitude;
    void Move(Vector3 destination)
    {
        bool found = CompletePath(player.transform.position, destination, out var path, out _);
        Vector3 direction = Vector3.zero;
        if (found && path.corners.Length > 0)
        {
            Vector3 next = path.corners[path.corners.Length - 1];
            for (int i = 1; i < path.corners.Length; i++) if (Flat(player.transform.position, path.corners[i]) > .2f) { next = path.corners[i]; break; }
            Vector3 delta = Vector3.ProjectOnPlane(next - player.transform.position, Vector3.up);
            if (delta.sqrMagnitude > .0025f) direction = delta.normalized * Mathf.Min(1, delta.magnitude);
        }
        player.StepMovement(Time.deltaTime, new Vector2(Vector3.Dot(direction, player.transform.right), Vector3.Dot(direction, player.transform.forward)), false);
        if (lastProgressAt < 0 || Vector3.Distance(destination, lastDestination) > 1 || Vector3.Distance(player.transform.position, progressPosition) > .15f)
        { lastProgressAt = Time.time; nextStallReport = Time.time + 2; progressPosition = player.transform.position; lastDestination = destination; }
        else if (Time.time >= nextStallReport && Vector3.Distance(player.transform.position, destination) > 2)
        {
            nextStallReport = Time.time + 8;
            Info("MOTOR STALL " + phase + " position=" + V(player.transform.position) + " destination=" + V(destination) +
                " grounded=" + player.Grounded + " path=" + path.status + " corners=" + string.Join(" > ", path.corners.Select(V)));
        }
    }
    bool Visible(NormalEnemyStateMachine enemy)
    {
        if (enemy == null || enemy.IsDead) return false;
        Vector3 delta = enemy.AimPoint - view.transform.position;
        return Physics.Raycast(view.transform.position, delta.normalized, out var hit, delta.magnitude + .2f, AimMask, QueryTriggerInteraction.Ignore) &&
            hit.collider.GetComponentInParent<NormalEnemyStateMachine>() == enemy;
    }
    void Fire()
    {
        var target = director.Enemies.Where(Visible).OrderBy(e => Vector3.Distance(player.transform.position, e.transform.position)).FirstOrDefault();
        if (target != null) { Aim(target.AimPoint); Shoot(false); }
    }
    void Shoot(bool structure)
    {
        var gun = TowerSession.Instance.weapons.currentWeapon as BaseGun; if (gun == null) return;
        if (gun.currentAmmo == 0) { gun.Reload(); return; }
        int before = gun.currentAmmo; gun.Use();
        if (active != null && gun.currentAmmo < before) { active.shots++; if (structure) active.structuralShots++; }
    }
    bool Interact(AlphaDistrictTerminal terminal)
    {
        if (terminal == null || Vector3.Distance(player.headCam.position, terminal.transform.position) > 3) return false;
        Aim(terminal.transform.position);
        if (!Physics.Raycast(player.headCam.position, player.headCam.forward, out var hit, 3, AimMask, QueryTriggerInteraction.Ignore) ||
            hit.collider.GetComponentInParent<AlphaDistrictTerminal>() != terminal) return false;
        InteractMethod.Invoke(player.GetComponent<PlayerInteraction>(), null);
        Info("Real E-handler reached terminal " + terminal.kind + " at " + V(player.transform.position)); return true;
    }
    Vector3 Approach(AlphaDistrictTerminal terminal, int zone)
    {
        Vector3 center = Marker("QA." + (char)('A' + zone)).position;
        Vector3 away = Vector3.ProjectOnPlane(center - terminal.transform.position, Vector3.up);
        if (away.sqrMagnitude < .1f) away = Vector3.ProjectOnPlane(player.transform.position - terminal.transform.position, Vector3.up);
        if (away.sqrMagnitude < .1f) away = Vector3.forward;
        var desired = terminal.transform.position + away.normalized * 1.6f; desired.y = center.y;
        return Ground(desired, out var point) ? point + Vector3.up * RootOffset : center;
    }
    IEnumerator Leg(Vector3 destination, string label, float limit = 60)
    {
        phase = label; lastProgressAt = -1; float end = Time.time + limit;
        while (!finished && !player.IsDead && Vector3.Distance(player.transform.position, destination) > .8f && Time.time < end)
        { Fire(); Move(destination); TickEvidence(); yield return null; }
        legReached = Vector3.Distance(player.transform.position, destination) <= .8f;
        Check(legReached, "Real player motor reaches " + label + " without an internal teleport");
        if (legReached)
            for (char zone = 'A'; zone <= 'F'; zone++)
            {
                var anchor = GameObject.Find("QA." + zone); var target = GameObject.Find("QA.View." + zone);
                if (anchor != null && target != null && Vector3.Distance(destination, anchor.transform.position) < .01f)
                    CaptureLooking("overview-" + zone + ".png", target.transform.position);
            }
    }
    Vector3 OrbitPoint(int zone, int turn)
    {
        Vector3 center = GameObject.Find("QA." + (char)('A' + zone)).transform.position;
        for (int i = 0; i < 8; i++)
        {
            Vector3 desired = center + Quaternion.Euler(0, (turn + i) * 45, 0) * Vector3.forward * 7;
            if (!Ground(desired, out var ground)) continue;
            Vector3 root = ground + Vector3.up * RootOffset;
            if (director.zones[zone].bounds.Contains(root) && CompletePath(player.transform.position, root, out _, out _)) return root;
        }
        return center;
    }
    IEnumerator Power(int kind, int zone)
    {
        var terminal = Terminal(kind); if (terminal == null) yield break;
        yield return Leg(Approach(terminal, zone), "approach-power-" + kind); if (!legReached || player.IsDead) yield break;
        Check(Interact(terminal), "Actual three-metre E-ray starts power objective " + kind); Capture("power-" + kind + "-started.png");
        phase = "charge-power-" + kind; float start = Time.time, end = Time.time + 55;
        int turn = 0; Vector3 orbit = OrbitPoint(zone, turn);
        while (!finished && !player.IsDead && !(kind == 0 ? director.PowerA : director.PowerB) && Time.time < end)
        {
            if (Flat(player.transform.position, orbit) < 1) orbit = OrbitPoint(zone, ++turn);
            Fire(); Move(orbit); TickEvidence(); yield return null;
        }
        Check(kind == 0 ? director.PowerA : director.PowerB, "Power objective " + kind + " completes under live pressure in its authored district");
        Info("Power " + kind + " observed duration=" + F(Time.time - start) + "s; authored bot orbit is not human pacing.");
        Capture("power-" + kind + "-complete.png");
    }
    IEnumerator RunRoute(bool reactive)
    {
        active = new Trace { name = reactive ? "F-first-reactive" : "B-first-intact",
            limitation = "Authored motor route and perfect aiming. No freeze, QA healing, fabricated pickups or internal teleport. Objective healing and shield regeneration remain." };
        evidence.traces.Add(active); attacks.Clear(); photographedZones.Clear(); observedZone = -1;
        traceBegan = Time.time; nextSample = Time.time; previousPosition = player.transform.position;
        UnityEngine.Random.InitState(251025); director.StartEncounter(); Capture("arrival.png");
        if (!reactive)
        {
            yield return Leg(Marker("QA.B").position, "B");
            if (!player.IsDead) yield return Power(0, 1);
            if (!player.IsDead) yield return Leg(Marker("QA.E").position, "E");
            if (!player.IsDead) yield return Leg(Marker("QA.F").position, "F");
            if (!player.IsDead) yield return Power(1, 5);
        }
        else
        {
            yield return Leg(Marker("QA.D").position, "D");
            if (!player.IsDead) yield return Leg(Marker("QA.F").position, "F");
            if (!player.IsDead) yield return Power(1, 5);
            if (!player.IsDead) yield return Containment();
            if (!player.IsDead) yield return Collapse();
            if (!player.IsDead) yield return Leg(Marker("QA.E").position, "E");
            if (!player.IsDead) yield return Leg(Marker("QA.B").position, "B");
            if (!player.IsDead) yield return Power(0, 1);
        }
        if (!player.IsDead && director.PowerA && director.PowerB)
        {
            yield return Leg(Marker("QA.C").position, "C-radio");
            var radio = Terminal(2);
            if (radio != null)
            {
                yield return Leg(Approach(radio, 2), "radio-E-ray");
                Check(Interact(radio), "The radio is reached by the actual E-handler");
                yield return Until(() => director.AlarmCalled, 2, "Both powers enable the evacuation alarm");
                Capture("evacuation-called.png");
            }
            if (!player.IsDead) yield return Leg(Marker("QA.A").position, "A-return");
            phase = "evacuation-approach";
            var exit = Terminal(3);
            if (exit != null && !player.IsDead)
            {
                float end = Time.time + 70; int turn = 0; Vector3 orbit = OrbitPoint(0, turn);
                Vector3 exitApproach = Approach(exit, 0);
                while (!director.Completed && !player.IsDead && Time.time < end)
                {
                    if (director.SecondsToExtraction > 0)
                    {
                        if (Flat(player.transform.position, orbit) < 1) orbit = OrbitPoint(0, ++turn);
                        Fire(); Move(orbit);
                    }
                    else
                    {
                        Fire(); Move(exitApproach);
                        if (Flat(player.transform.position, exitApproach) < 1) Interact(exit);
                    }
                    TickEvidence(); yield return null;
                }
            }
        }
        Record();
        active.seconds = Time.time - traceBegan; active.died = player.IsDead; active.completed = director.Completed;
        active.powerA = director.PowerA; active.powerB = director.PowerB; active.alarm = director.AlarmCalled;
        active.containmentOpened = director.containment.IsOpen; active.collapsed = director.collapse.IsSettled;
        active.impactKills = director.collapse.ImpactKills; active.aliveAtExit = director.AliveCount;
        Check(active.completed && active.powerA && active.powerB && active.alarm, active.name + ": spatial milestones and actual extraction complete");
        Check(active.maxAlive <= 12 && active.maxSpecialists <= 3, active.name + ": observed population respects declared caps");
        if (reactive) Check(active.containmentOpened && active.collapsed, "The reactive route transforms both connections");
        else Info("Initially intact trace ends containmentOpen=" + active.containmentOpened + ", collapseSettled=" + active.collapsed + "; enemy-caused changes are reported, not silently attributed to a player decision.");
        Info(active.name + ": seconds=" + F(active.seconds) + ", metres=" + F(active.distance) + ", damage=" + F(active.healthDamage + active.shieldDamage) +
            ", gameHealing=" + F(active.gameHealing) + ", aliveAtExit=" + active.aliveAtExit + ", completed=" + active.completed + "; not a human fun/difficulty score.");
        Capture("extraction.png"); active = null;
    }
    Vector3 StructureTarget(FoundryStructure structure)
    {
        var colliders = structure.Source.GetComponentsInChildren<Collider>().Concat(structure.Source.GeneratedGraph.GetComponentsInChildren<Collider>())
            .Where(c => c.enabled && c.gameObject.activeInHierarchy).OrderBy(c => Vector3.Distance(c.bounds.center, structure.transform.position));
        foreach (var collider in colliders)
        {
            var bounds = collider.bounds;
            // Tall supports can be partly hidden by the maintenance shutter. Aim at an
            // actually visible surface, rather than repeatedly shooting its blocked centre.
            foreach (Vector3 point in new[] { bounds.center, bounds.center + Vector3.up * bounds.extents.y * .8f,
                bounds.center - Vector3.up * bounds.extents.y * .6f, bounds.center + Vector3.right * bounds.extents.x * .6f,
                bounds.center - Vector3.right * bounds.extents.x * .6f })
            {
                Vector3 delta = point - view.transform.position;
                if (Physics.Raycast(view.transform.position, delta.normalized, out var hit, delta.magnitude + .3f, AimMask, QueryTriggerInteraction.Ignore) &&
                    FoundryStructure.From(hit.collider) == structure) return point;
            }
        }
        return structure.transform.position;
    }
    IEnumerator Break(FoundryStructure structure, string label)
    {
        phase = "fire-" + label; float end = Time.time + 18; int before = active.structuralShots;
        while (!structure.IsOpen && !player.IsDead && Time.time < end)
        {
            Aim(StructureTarget(structure)); Shoot(true); player.StepMovement(Time.deltaTime, Vector2.zero, false); TickEvidence(); yield return null;
        }
        Check(structure.IsOpen, "Actual weapon fire severs " + label);
        Info(label + " structural shots=" + (active.structuralShots - before) + "; robots and hazards remain active.");
    }
    bool PhysicalLine(Vector3 a, Vector3 b) => !Physics.Linecast(a + Vector3.up * .35f, b + Vector3.up * .35f, WorldMask, QueryTriggerInteraction.Ignore);
    IEnumerator Containment()
    {
        Vector3 far = Marker("QA.ContainmentFar").position, near = Marker("QA.ContainmentNear").position;
        yield return Leg(far, "F-side-containment"); if (!legReached || player.IsDead) yield break;
        bool pathBefore = CompletePath(far, near, out _, out float before), lineBefore = PhysicalLine(far, near);
        CaptureLooking("containment-before.png", director.containment.transform.position);
        yield return Break(director.containment, "C-F-containment"); yield return new WaitForSeconds(.3f);
        bool pathAfter = CompletePath(far, near, out _, out float after);
        Check(pathBefore && pathAfter, "Both containment states preserve a real district route");
        Check(!lineBefore && PhysicalLine(far, near) && PhysicalLine(near, far), "Containment destruction opens reciprocal firing lines");
        Info("Containment path metres before/after=" + F(before) + "/" + F(after) + "; distance alone does not validate the choice.");
        CaptureLooking("containment-open.png", director.containment.transform.position);
        if (!player.IsDead) yield return Leg(near, "C-F-new-crossing");
    }
    Vector3 SafeCollapsePoint()
    {
        var danger = director.collapse.impactBounds; var nest = director.collapse.nestBounds;
        foreach (var collider in director.collapse.finalRamp.GetComponentsInChildren<Collider>(true).Where(c => c.enabled)) danger.Encapsulate(collider.bounds);
        danger.Expand(2); nest.Expand(2);
        for (int radius = 4; radius <= 16; radius += 4) for (int i = 0; i < 12; i++)
        {
            Vector3 desired = player.transform.position + Quaternion.Euler(0, i * 30, 0) * Vector3.forward * radius;
            if (!Ground(desired, out var point)) continue;
            Vector3 root = point + Vector3.up * RootOffset;
            if (!danger.Contains(root) && !nest.Contains(root) && CompletePath(player.transform.position, root, out _, out _)) return root;
        }
        return Marker("QA.C").position;
    }
    Vector3 RampCenter()
    {
        var marker = GameObject.Find("QA.CollapseRamp"); if (marker != null) return marker.transform.position;
        var colliders = director.collapse.finalRamp.GetComponentsInChildren<Collider>().Where(c => c.enabled).ToArray();
        if (colliders.Length == 0) return Marker("QA.CollapseFar").position;
        var bounds = colliders[0].bounds; foreach (var collider in colliders.Skip(1)) bounds.Encapsulate(collider.bounds);
        Vector3 origin = new Vector3(bounds.center.x, bounds.max.y + 3, bounds.center.z);
        var hit = Physics.RaycastAll(origin, Vector3.down, bounds.size.y + 6, WorldMask, QueryTriggerInteraction.Ignore)
            .FirstOrDefault(h => h.collider.transform.IsChildOf(director.collapse.finalRamp.transform));
        return hit.collider != null ? hit.point + Vector3.up * RootOffset : bounds.center + Vector3.up * RootOffset;
    }
    IEnumerator Collapse()
    {
        var collapse = director.collapse; Vector3 near = Marker("QA.CollapseNear").position, far = Marker("QA.CollapseFar").position;
        yield return Leg(near, "collapse-safe-approach"); if (!legReached || player.IsDead) yield break;
        bool beforePath = CompletePath(near, far, out _, out float before);
        CaptureLooking("gantry-before.png", LoadCenter());
        foreach (var support in collapse.supports) { yield return Break(support, "gantry-support"); if (player.IsDead) yield break; }
        phase = "collapse-warning-and-settle"; float end = Time.time + 30, fallingAt = -1;
        bool warningCaptured = false, fallingCaptured = false;
        while (!collapse.IsSettled && !player.IsDead && Time.time < end)
        {
            Fire(); if (collapse.SafetyBlocked) Move(SafeCollapsePoint()); else player.StepMovement(Time.deltaTime, Vector2.zero, false);
            if (!warningCaptured && collapse.State == AlphaDistrictCollapse.CollapsePhase.Warning)
            { CaptureLooking("gantry-warning.png", LoadCenter()); warningCaptured = true; }
            if (collapse.State == AlphaDistrictCollapse.CollapsePhase.Falling)
            {
                if (fallingAt < 0) fallingAt = Time.time;
                if (!fallingCaptured && Time.time - fallingAt >= .35f)
                { CaptureLooking("gantry-falling.png", LoadCenter()); fallingCaptured = true; }
            }
            TickEvidence(); yield return null;
        }
        Check(collapse.IsSettled && collapse.TransitionCount == 1, "The live gantry settles once after both real supports are severed");
        if (!collapse.IsSettled || player.IsDead) yield break;
        Check(collapse.finalRamp.GetComponentsInChildren<Collider>().Any(c => c.enabled) && !collapse.oldNest.activeInHierarchy && collapse.lowBlocker.activeInHierarchy,
            "Settling replaces the hostile nest and low passage with stable ramp geometry");
        // Carving is asynchronous. Keep real combat/collision running while the settled
        // surface and blocker propagate to navigation before taking its path measurement.
        float navigationReadyAt = Time.time + .45f;
        while (!player.IsDead && Time.time < navigationReadyAt)
        { Fire(); player.StepMovement(Time.deltaTime, Vector2.zero, false); TickEvidence(); yield return null; }
        bool afterPath = CompletePath(near, far, out _, out float after);
        Check(beforePath && afterPath, "The districts remain connected in both gantry states");
        Info("Gantry path metres before/after=" + F(before) + "/" + F(after) + ", live impact kills=" + collapse.ImpactKills + "; no immobilized target guarantees a crush.");
        CaptureLooking("gantry-settled.png", LoadCenter()); yield return Leg(RampCenter(), "actual-final-ramp-surface");
        if (legReached && !player.IsDead) yield return Leg(far, "gantry-ramp-exit");
    }
    IEnumerator CollapseView()
    {
        active = new Trace { name = "collapse-view",
            limitation = "Focused lateral observation reached by the real motor through B, under normal live pressure. No objectives, freeze, QA healing, actor placement or internal teleport; captures use a fixed world look target." };
        evidence.traces.Add(active); attacks.Clear(); photographedZones.Clear(); observedZone = -1;
        traceBegan = Time.time; nextSample = Time.time; previousPosition = player.transform.position;
        UnityEngine.Random.InitState(251025); director.StartEncounter();
        yield return Leg(Marker("QA.B").position, "collapse-view-via-B");
        if (legReached && !player.IsDead)
            yield return Leg(new Vector3(-35, 4.72f, 21), "collapse-view-BE-ramp");
        var collapse = director.collapse; Vector3 look = new Vector3(-12, 6, 19);
        bool warningCaptured = false, fallingCaptured = false;
        if (legReached && !player.IsDead)
        {
            float initialLoadY = collapse.movingLoad.position.y;
            phase = "collapse-view-initial"; Record();
            CaptureLooking("gantry-initial.png", look);
            Info("Lateral collapse view reached at " + V(player.transform.position) + ", fixed gaze=" + V(look));
            foreach (var support in collapse.supports)
            {
                yield return Break(support, "lateral-gantry-support");
                if (player.IsDead) break;
            }
            phase = "collapse-view-live-warning-and-fall"; float end = Time.time + 35;
            while (!collapse.IsSettled && !player.IsDead && Time.time < end)
            {
                Fire(); player.StepMovement(Time.deltaTime, Vector2.zero, false);
                if (!warningCaptured && collapse.State == AlphaDistrictCollapse.CollapsePhase.Warning)
                {
                    CaptureLooking("gantry-warning.png", look); warningCaptured = true;
                    Info("Lateral warning capture at " + V(player.transform.position) + ", safetyBlocked=" + collapse.SafetyBlocked);
                }
                if (!fallingCaptured && collapse.State == AlphaDistrictCollapse.CollapsePhase.Falling &&
                    initialLoadY - collapse.movingLoad.position.y >= 4)
                {
                    CaptureLooking("gantry-midfall.png", look); fallingCaptured = true;
                    Info("Lateral midfall capture at " + V(player.transform.position) + ", load descent=" + F(initialLoadY - collapse.movingLoad.position.y));
                }
                TickEvidence(); yield return null;
            }
            if (collapse.IsSettled)
            {
                phase = "collapse-view-settled"; Record(); CaptureLooking("gantry-settled.png", look);
                Info("Lateral settled capture at " + V(player.transform.position) + ", safetyBlocked=" + collapse.SafetyBlocked);
            }
        }
        Record(); active.seconds = Time.time - traceBegan; active.died = player.IsDead;
        active.completed = director.Completed; active.powerA = director.PowerA; active.powerB = director.PowerB;
        active.alarm = director.AlarmCalled; active.containmentOpened = director.containment.IsOpen;
        active.collapsed = collapse.IsSettled; active.impactKills = collapse.ImpactKills; active.aliveAtExit = director.AliveCount;
        Check(collapse.IsSettled && collapse.TransitionCount == 1, "Focused live observation settles the gantry once");
        Check(warningCaptured && fallingCaptured, "Focused observation records warning and a real four-metre descent before settling");
        Info("collapse-view: seconds=" + F(active.seconds) + ", metres=" + F(active.distance) + ", damage=" +
            F(active.healthDamage + active.shieldDamage) + ", gameHealing=" + F(active.gameHealing) +
            ", alive=" + active.aliveAtExit + "; focused event evidence, no objective-completion or human-fun assertion.");
        active = null;
    }
    void TickEvidence()
    {
        if (active == null) return;
        active.distance += Vector3.Distance(previousPosition, player.transform.position); previousPosition = player.transform.position;
        if (director.CurrentZoneIndex != observedZone)
        {
            observedZone = director.CurrentZoneIndex; Info(active.name + " enters zone=" + observedZone + " phase=" + phase + " position=" + V(player.transform.position));
            if (photographedZones.Add(observedZone)) Capture("district-" + observedZone + ".png");
        }
        if (Time.time >= nextSample) { Record(); nextSample = Time.time + 1; }
    }
    void Record()
    {
        if (active == null) return;
        var all = director.Enemies.Where(e => e != null).ToArray(); var alive = all.Where(e => !e.IsDead).ToArray();
        foreach (var enemy in all)
        {
            var melee = enemy.GetComponent<MeleeRobotCombat>();
            attacks[enemy.GetInstanceID()] = new[] { enemy.Ranged != null ? enemy.Ranged.ProjectilesLaunched : 0,
                enemy.Ranged != null ? enemy.Ranged.LaserShotsFired : 0, melee != null ? melee.AttacksStarted : 0 };
        }
        int specialists = alive.Count(e => e.IsVeteran); var p = player.transform.position;
        active.maxAlive = Mathf.Max(active.maxAlive, alive.Length); active.maxSpecialists = Mathf.Max(active.maxSpecialists, specialists);
        active.samples.Add(new Sample
        {
            phase = phase, collapse = director.collapse.State.ToString(), seconds = Time.time - traceBegan,
            x = p.x, y = p.y, z = p.z, health = player.Health, shield = player.GetComponent<Shield>()?.Current ?? 0,
            damage = active.shieldDamage + active.healthDamage, healed = active.gameHealing, distance = active.distance,
            zone = director.CurrentZoneIndex, alive = alive.Length, pending = director.PendingSpawns, spawned = director.SpawnedTotal,
            specialists = specialists, shots = active.shots, structuralShots = active.structuralShots,
            within15m = alive.Count(e => Vector3.Distance(e.transform.position, p) <= 15),
            visibleEnemies = alive.Count(CameraSees),
            nearestEnemy = alive.Length == 0 ? -1 : alive.Min(e => Vector3.Distance(e.transform.position, p)),
            projectiles = attacks.Values.Sum(v => v[0]), lasers = attacks.Values.Sum(v => v[1]), meleeAttacks = attacks.Values.Sum(v => v[2]),
            powerA = director.PowerA, powerB = director.PowerB, alarm = director.AlarmCalled, completed = director.Completed,
            extraction = director.SecondsToExtraction, impactKills = director.collapse.ImpactKills
        });
    }
    void VisualContracts()
    {
        visuals = Object.FindFirstObjectByType<AlphaDistrictVisuals>();
        Check(visuals != null && visuals.pipeline != null && visuals.UsingLocalPipeline, "The actual district pipeline is active in quality and graphics settings");
        if (visuals == null) return;
        Check(visuals.CameraCount >= 2 && visuals.StyledRenderers > 0, "Real player cameras and renderers receive the district style");
        Check(visuals.actorTemplate != null && visuals.actorTemplate.shader != null && visuals.actorTemplate.shader.isSupported, "The actual actor shader is supported");
        bool cameraRenderer = player.GetComponentsInChildren<Camera>(true).All(camera =>
        {
            var data = camera.GetUniversalAdditionalCameraData();
            return data.scriptableRenderer != null && (RendererIndex == null || (int)RendererIndex.GetValue(data) == 0);
        });
        Check(cameraRenderer, "Actual player cameras use renderer zero of the local pipeline");
        Info("RENDER pipeline=" + visuals.pipeline.name + ", actorShader=" + visuals.actorTemplate.shader.name + ", styledRenderers=" + visuals.StyledRenderers + ", cameras=" + visuals.CameraCount + "; visual quality still requires screenshot review.");
    }
    IEnumerator NormalSmoke()
    {
        Check(!Environment.GetCommandLineArgs().Contains("-district-qa"), "Normal smoke does not enable the QA encounter branch");
        yield return new WaitForSeconds(2); view = player.GetComponentsInChildren<Camera>().First(c => c.gameObject.layer != 8);
        Check(player.enabled && !TowerSession.Instance.weapons.AutomatedInput, "Normal player motor and weapon input remain enabled");
        Check(Object.FindObjectsByType<PlayerStateMachine>(FindObjectsSortMode.None).Length == 1 && TowerSession.Instance.CurrentFloor == null && !GameManager.Instance.IsSpawning,
            "Normal startup contains one real player and no tower waves");
        Check(director.AliveCount > 0 && director.SpawnedTotal > 0, "Normal startup populates the district with real robots");
        var weapons = TowerSession.Instance.weapons;
        var available = Enumerable.Range(0, weapons.Weapons.Count).Where(weapons.IsAvailable).Select(i => weapons.Weapons[i]).ToArray();
        Check(available.Any(w => w is MachineGun) && available.Any(w => w is Pistol) && available.Any(w => w is Knife) && !available.Any(w => w is ShotGun),
            "Normal arsenal exposes machine gun, pistol and knife while shotgun is a milestone reward");
        VisualContracts(); Capture("normal-arrival.png");
        string hud = Path.Combine(output, "normal-arrival-hud.png"); ScreenCapture.CaptureScreenshot(hud); yield return new WaitForEndOfFrame();
        yield return Until(() => File.Exists(hud) && new FileInfo(hud).Length > 0, 5, "Normal framebuffer capture includes the gameplay HUD");
        Finish();
    }
    void Aim(Vector3 point)
    {
        var look = Quaternion.LookRotation(point - view.transform.position);
        player.transform.rotation = Quaternion.Euler(0, look.eulerAngles.y, 0); player.headCam.localRotation = Quaternion.Euler(look.eulerAngles.x, 0, 0); Physics.SyncTransforms();
    }
    bool CameraSees(NormalEnemyStateMachine enemy)
    {
        Vector3 screen = view.WorldToViewportPoint(enemy.AimPoint);
        return screen.z > 0 && screen.x >= 0 && screen.x <= 1 && screen.y >= 0 && screen.y <= 1 && Visible(enemy);
    }
    Vector3 LoadCenter()
    {
        var load = director.collapse.movingLoad;
        var renderers = load.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return load.position;
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds.center;
    }
    void CaptureLooking(string file, Vector3 point)
    {
        // A labelled review view from the actual reached position, not an extra route,
        // enemy placement or a persistent alteration of the combat controller's gaze.
        Quaternion body = player.transform.rotation, head = player.headCam.localRotation;
        Aim(point); Capture(file);
        player.transform.rotation = body; player.headCam.localRotation = head; Physics.SyncTransforms();
    }
    void Capture(string file)
    {
        if (view == null || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
        string folder = active != null ? Path.Combine(output, active.name) : output; Directory.CreateDirectory(folder);
        var target = new RenderTexture(1280, 720, 24); target.Create();
        RenderPipeline.SubmitRenderRequest(view, new RenderPipeline.StandardRequest { destination = target });
        var previous = RenderTexture.active; RenderTexture.active = target;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(folder, file), image.EncodeToPNG()); RenderTexture.active = previous; Destroy(image); target.Release(); Destroy(target);
    }
    void Finish()
    {
        if (finished) return; finished = true; active = null;
        if (player != null) { player.Damaged -= Damaged; player.Healed -= Healed; }
        Application.logMessageReceived -= Log;
        File.WriteAllText(Path.Combine(output, "results.txt"), string.Join("\n", checks) + "\nERRORS\n" + string.Join("\n", errors));
        if (!smoke)
        {
            File.WriteAllText(Path.Combine(output, "observations.json"), JsonUtility.ToJson(evidence, true));
            var csv = new List<string> { "trace,phase,seconds,x,y,z,zone,health,shield,damage,healed,distance,alive,within15m,visibleEnemies,nearestEnemy,pending,spawned,specialists,shots,structuralShots,projectiles,lasers,meleeAttacks,powerA,powerB,alarm,extraction,collapse,impactKills,completed" };
            foreach (var trace in evidence.traces) foreach (var s in trace.samples)
                csv.Add(string.Join(",", new[] { trace.name, s.phase, F(s.seconds), F(s.x), F(s.y), F(s.z), s.zone.ToString(), F(s.health), F(s.shield), F(s.damage),
                    F(s.healed), F(s.distance), s.alive.ToString(), s.within15m.ToString(), s.visibleEnemies.ToString(), F(s.nearestEnemy), s.pending.ToString(), s.spawned.ToString(), s.specialists.ToString(), s.shots.ToString(), s.structuralShots.ToString(),
                    s.projectiles.ToString(), s.lasers.ToString(), s.meleeAttacks.ToString(), s.powerA.ToString(), s.powerB.ToString(), s.alarm.ToString(), F(s.extraction), s.collapse, s.impactKills.ToString(), s.completed.ToString() }));
            File.WriteAllLines(Path.Combine(output, "observations.csv"), csv);
        }
        Debug.Log((smoke ? "ALPHA DISTRICT NORMAL SMOKE COMPLETE: " : "ALPHA DISTRICT INTEGRATION COMPLETE: ") + checks.Count + " entries, " + errors.Count + " errors. This does not measure human fun.");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(errors.Count == 0 ? 0 : 1);
#else
        Application.Quit(errors.Count == 0 ? 0 : 1);
#endif
    }
    static string F(float value) => value.ToString("F3", CultureInfo.InvariantCulture);
    static string V(Vector3 value) => "(" + F(value.x) + "," + F(value.y) + "," + F(value.z) + ")";
}
#endif
