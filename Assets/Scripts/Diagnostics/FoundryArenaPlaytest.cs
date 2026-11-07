#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Project.Scripts.Fractures;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Opt-in physical contracts and labelled observations; neither is a human playtest.</summary>
public sealed class FoundryArenaPlaytest : MonoBehaviour
{
    [Serializable]
    public sealed class Observation
    {
        public string name, limitation;
        public bool screenOpen, died, collectedCore, cleared, exited;
        public float seconds, distance, shieldDamage, healthDamage;
        public int hits, shots;
        public int structuralShots;
        public float structuralDamage, firstNeSightSeconds = -1, neKilledSeconds = -1;
        public bool reachedFarSide;
        public List<string> damageCauses = new List<string>();
        public List<Sample> samples = new List<Sample>();
    }
    [Serializable]
    public sealed class Sample
    {
        public string phase;
        public float seconds, x, y, z, health, shield, shieldDamage, healthDamage, distance;
        public int wave, alive, melee, ranged, visible, nearArrival, outsideSector, pendingSpawns, spawned, spawnWaits, projectiles, lasers, charges;
    }
    [Serializable]
    sealed class ObservationFile
    {
        public string status = "Controlled observations, not measurements of fun or proof of fairness.";
        public string movement = "The real StepMovement motor follows NavMesh corners. No jump or slide; perfect target selection for the armed trace.";
        public List<Observation> observations = new List<Observation>();
    }

    readonly List<string> checks = new List<string>(), errors = new List<string>();
    readonly ObservationFile observations = new ObservationFile();
    readonly Dictionary<int, int[]> attackCounts = new Dictionary<int, int[]>();
    FoundryArenaDirector director;
    PlayerStateMachine player;
    Camera view;
    Pistol pistol;
    Observation active;
    string output, activeSummary, observationPhase;
    float began;
    Vector3 motorProgressPosition, motorDestination;
    float motorProgressAt = -1, motorNextReport;
    bool motorLegReached;
    bool finished, smoke, choice;
    const int WorldMask = (1 << 0) | (1 << 3) | (1 << 10);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var args = Environment.GetCommandLineArgs();
        if ((!args.Contains("-foundry-qa") && !args.Contains("-foundry-smoke")) || Object.FindFirstObjectByType<FoundryArenaPlaytest>() != null) return;
        var go = new GameObject("Foundry level validation");
        DontDestroyOnLoad(go); go.AddComponent<FoundryArenaPlaytest>();
    }
    void Awake()
    {
        var args = Environment.GetCommandLineArgs();
        smoke = args.Contains("-foundry-smoke");
        choice = args.Contains("-foundry-choice");
        int index = Array.IndexOf(args, "-foundry-output");
        output = Path.GetFullPath(index >= 0 && index + 1 < args.Length ? args[index + 1] :
            smoke ? "Artifacts/FoundrySmoke-2026-10-23" : "Artifacts/FoundryArena-2026-10-23");
        Directory.CreateDirectory(output); Application.runInBackground = true;
        Application.logMessageReceived += Log; began = Time.realtimeSinceStartup;
    }
    void Update()
    {
        if (!finished && Time.realtimeSinceStartup - began > 480) { Check(false, "Foundry validation watchdog"); Finish(); }
    }
    void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack);
    }
    void Check(bool ok, string label)
    {
        checks.Add((ok ? "PASS " : "FAIL ") + label); Debug.Log(checks.Last());
        if (!ok) errors.Add(label);
    }
    void Info(string label) { checks.Add("INFO " + label); Debug.Log(checks.Last()); }
    IEnumerator Until(Func<bool> predicate, float seconds, string label)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (!finished && !predicate() && Time.realtimeSinceStartup < end) yield return null;
        if (!finished) Check(predicate(), label);
    }
    IEnumerator Start()
    {
        if (smoke) { yield return NormalSmoke(); yield break; }
        yield return Until(() => Object.FindFirstObjectByType<FoundryArenaDirector>() != null &&
            Object.FindFirstObjectByType<FoundryArenaDirector>().Ready, 75, "The rebuilt level loads its shared playable core");
        Bind();
        if (director == null || player == null) { Finish(); yield break; }
        yield return Until(() => director.Breach != null && director.Breach.Ready, 30, "The central screen has a prepared fracture graph");
        if (director.Breach == null || !director.Breach.Ready) { Finish(); yield break; }
        Check(SceneManager.GetActiveScene().name == "FundicionReactiva", "The rebuilt foundry is the active scene");
        Check(Object.FindObjectsByType<PlayerStateMachine>(FindObjectsSortMode.None).Length == 1, "There is exactly one real player");
        Check(TowerSession.Instance.CurrentFloor == null && !GameManager.Instance.IsSpawning, "Local level does not start tower progression");
        Check(director.Wave == 0 && director.Enemies.Count == 0, "Opt-in QA starts before the actual encounter");
        Check(!director.CollectCore() && !director.TryExit(), "Remote collection and the locked exit cannot skip the spatial objective");
        Info("Previous Artifacts/FoundryArena results belong to the old room and are not this level's validation.");
        if (choice)
        {
            yield return ChoiceTrace(false);
            yield return ChoiceTrace(true);
            Finish(); yield break;
        }
        player.TeleportTo(Anchor("QA.Arrival").position, director.arrival.rotation);
        yield return new WaitForSeconds(.35f); Capture("arrival.png");
        yield return RoutesAndScreen();
        yield return MotorAccess();
        yield return OccupiedEntryQueue();
        yield return ObservationProbe("stationary-intact", false, false);
        yield return ObservationProbe("orbit-intact", true, false);
        yield return ObservationProbe("stationary-open", false, true);
        yield return ObservationProbe("orbit-open", true, true);
        yield return CombatAdvance(false);
        yield return CombatAdvance(true);
        yield return Restart();
        player.TakeDamage(10000, Anchor("QA.CombatCenter").position, "Foundry validation death");
        Check(player.IsDead && TowerSession.Instance.RunEnded && WeaponAction.CombatPaused, "Actual player death pauses the level");
        yield return Restart(); Finish();
    }
    IEnumerator NormalSmoke()
    {
        Info("NORMAL STARTUP SMOKE: no foundry-qa flag, no Bind, no motor/input automation, no teleports or scripted combat.");
        Check(!Environment.GetCommandLineArgs().Contains("-foundry-qa"), "Smoke invocation does not enable the QA encounter branch");
        yield return Until(() => Object.FindFirstObjectByType<FoundryArenaDirector>() != null &&
            Object.FindFirstObjectByType<FoundryArenaDirector>().Ready, 75, "Normal startup reaches the actual level director");
        director = Object.FindFirstObjectByType<FoundryArenaDirector>();
        player = director != null ? director.Player : null;
        if (director == null || player == null) { Finish(); yield break; }
        yield return new WaitForSeconds(2);
        var weapons = TowerSession.Instance.weapons;
        view = player.GetComponentsInChildren<Camera>().First(c => c.gameObject.layer != 8);
        Check(player.enabled && !weapons.AutomatedInput, "Normal player motor and weapon input remain enabled");
        Check(Object.FindObjectsByType<PlayerStateMachine>(FindObjectsSortMode.None).Length == 1, "Normal startup contains one real player");
        Check(SceneManager.GetActiveScene().name == "FundicionReactiva" && TowerSession.Instance.CurrentFloor == null &&
            !GameManager.Instance.IsSpawning && TowerSession.Instance.RunWaves == 0, "Normal startup runs the independent level without tower waves");
        var alive = director.Enemies.Where(e => e != null && !e.IsDead).ToArray();
        Check(director.Wave == 1 && director.TotalSpawned == 4 && director.PendingSpawns == 0 && alive.Length == 4 &&
            alive.Count(e => !e.IsMelee && !e.IsVeteran) == 2 && alive.Count(e => e.IsMelee && !e.IsVeteran) == 2,
            "Normal startup actually places two Centinelas and two melee defenders");
        var available = Enumerable.Range(0, weapons.Weapons.Count).Where(weapons.IsAvailable).Select(i => weapons.Weapons[i]).ToArray();
        Check(available.Any(w => w is Pistol) && available.Any(w => w is Knife) && available.All(w => w is Pistol || w is Knife) &&
            weapons.currentWeapon is Pistol && !director.CoreCollected, "Normal arsenal starts with pistol and knife while the carbine remains locked");
        Capture("normal-arrival.png");
        string hud = Path.Combine(output, "normal-arrival-hud.png");
        ScreenCapture.CaptureScreenshot(hud);
        yield return new WaitForEndOfFrame();
        yield return Until(() => File.Exists(hud) && new FileInfo(hud).Length > 0, 5, "Normal framebuffer capture includes the gameplay HUD");
        Finish();
    }
    void Bind()
    {
        if (player != null) player.Damaged -= Damage;
        director = Object.FindFirstObjectByType<FoundryArenaDirector>();
        player = director != null ? director.Player : null;
        if (player == null) return;
        player.enabled = false; player.Damaged += Damage;
        motorProgressAt = -1;
        TowerSession.Instance.weapons.AutomatedInput = true;
        view = player.GetComponentsInChildren<Camera>().First(c => c.gameObject.layer != 8);
        pistol = TowerSession.Instance.weapons.Weapons.OfType<Pistol>().FirstOrDefault();
        if (pistol != null) TowerSession.Instance.weapons.EquipImmediate(Array.IndexOf(TowerSession.Instance.weapons.Weapons.ToArray(), pistol));
    }
    void Damage(PlayerDamage hit)
    {
        if (active == null) return;
        active.hits++; active.shieldDamage += hit.ShieldLoss; active.healthDamage += hit.HealthLoss;
        active.damageCauses.Add(hit.Cause);
    }
    Transform Anchor(string name)
    {
        var value = GameObject.Find(name); Check(value != null, "Scene anchor exists: " + name);
        return value != null ? value.transform : director.arrival;
    }
    Vector3 Ground(Vector3 point)
    {
        return NavMesh.SamplePosition(point, out var sample, 1.5f, NavMesh.AllAreas) ? sample.position : point;
    }
    bool CompletePath(Vector3 from, Vector3 to, out NavMeshPath path, out float length)
    {
        path = new NavMeshPath(); length = 0;
        bool complete = NavMesh.CalculatePath(Ground(from), Ground(to), NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
        for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        return complete;
    }
    bool ScreenBlocks(Vector3 from, Vector3 to)
    {
        return Physics.RaycastAll(from, (to - from).normalized, Vector3.Distance(from, to), WorldMask, QueryTriggerInteraction.Ignore)
            .Any(h => FoundryStructure.From(h.collider) == director.Breach);
    }
    IEnumerator RoutesAndScreen()
    {
        Vector3 south = Anchor("QA.BreachSouth").position, north = Anchor("QA.BreachNorth").position;
        Vector3 arrival = Anchor("QA.Arrival").position, exit = Anchor("QA.ExitApproach").position;
        Vector3 center = Anchor("QA.CombatCenter").position;
        yield return new WaitForSeconds(.5f);
        Check(CompletePath(arrival, center, out _, out _) && CompletePath(center, exit, out _, out _), "The intact screen leaves a complete route to the locked exit approach");
        bool intactPath = CompletePath(south, north, out _, out float before);
        Check(intactPath && NavMesh.Raycast(Ground(south), Ground(north), out _, NavMesh.AllAreas), "The intact central screen blocks the direct crossing but has a detour");
        Check(ScreenBlocks(south + Vector3.up * .35f, north + Vector3.up * .35f) && ScreenBlocks(north + Vector3.up * .35f, south + Vector3.up * .35f),
            "The intact central screen blocks the measured reciprocal firing line");
        int chunks = director.Breach.Source.GeneratedGraph.GetComponentsInChildren<ChunkNode>(true).Length;
        Check(chunks > 1 && chunks <= 128, "The screen uses a bounded real fracture graph");
        Check(pistol != null, "The actual starting pistol is available");
        if (pistol == null) yield break;
        var structure = director.Breach;
        Vector3 target = structure.transform.position;
        player.TeleportTo(south, Quaternion.identity); Aim(target);
        yield return new WaitForSeconds(.4f);
        float health = structure.Health; int ammo = pistol.currentAmmo;
        pistol.Use(); yield return new WaitForSeconds(.1f);
        Check(pistol.currentAmmo == ammo - 1 && Mathf.Approximately(health - structure.Health, Mathf.Min(health, pistol.damage)),
            "A real pistol shot damages the shared structural pool once");
        using (var pause = RunPause.Acquire(false))
        {
            health = structure.Health;
            Check(!structure.ApplyImpact(target, Vector3.forward, 1) && structure.Health == health, "Pause refuses structural damage");
            yield return new WaitForSecondsRealtime(.15f);
        }
        yield return ShootScreen();
        yield return Until(() => !NavMesh.Raycast(Ground(south), Ground(north), out _, NavMesh.AllAreas), 4, "Collapse opens the real direct navigation crossing");
        bool openPath = CompletePath(south, north, out _, out float after);
        Check(openPath && after + .5f < before, "The opened screen creates a shorter physical connection");
        Info("Measured local crossing metres intact/open: " + F(before) + " / " + F(after) + "; this is not evidence of a good combat decision.");
        Physics.SyncTransforms();
        Vector3 a = south + Vector3.up * .35f, b = north + Vector3.up * .35f;
        Check(!Physics.Linecast(a, b, WorldMask, QueryTriggerInteraction.Ignore) && !Physics.Linecast(b, a, WorldMask, QueryTriggerInteraction.Ignore),
            "Collapse opens that firing line in both directions");
        Check(CompletePath(arrival, center, out _, out _) && CompletePath(center, exit, out _, out _), "Opening the screen preserves the complete route to the locked exit approach");
        var cc = player.GetComponent<CharacterController>(); Vector3 feet = Ground(structure.passageCenter) + Vector3.up * .05f;
        Check(!Physics.CheckCapsule(feet + Vector3.up * (cc.radius + .05f), feet + Vector3.up * (cc.height - cc.radius), cc.radius * .95f,
            WorldMask, QueryTriggerInteraction.Ignore), "The opened crossing fits the actual player capsule");
        Aim(target); Capture("central-screen-impact.png");
        yield return new WaitForSeconds(3.6f);
        Capture("central-screen-open.png");
    }
    IEnumerator MotorAccess()
    {
        yield return Restart();
        Info("Dry motor access starts at the actual arrival without enemies. No teleport is used inside the arrival/foot/gallery/core route.");
        var cc = player.GetComponent<CharacterController>();
        Info("Actual motor capsule height=" + F(cc.height) + ", radius=" + F(cc.radius) + ", stepOffset=" + F(cc.stepOffset) + ", slopeLimit=" + F(cc.slopeLimit));
        foreach (string marker in new[] { "QA.SouthRampFoot", "QA.High", "QA.Core" })
        {
            Vector3 destination = Anchor(marker).position;
            Aim(destination);
            yield return MotorLeg(destination, 22, marker);
            Capture("motor-" + marker.Replace("QA.", "") + ".png");
            if (!motorLegReached) yield break;
        }
        Check(director.Wave == 0 && director.Enemies.Count == 0, "The isolated motor route does not silently start a QA encounter");
    }
    IEnumerator MotorLeg(Vector3 destination, float seconds, string label)
    {
        motorProgressAt = -1;
        float end = Time.time + seconds;
        while (!player.IsDead && Vector3.Distance(player.transform.position, destination) > .7f && Time.time < end)
        {
            MoveToward(destination);
            yield return null;
        }
        motorLegReached = Vector3.Distance(player.transform.position, destination) <= .7f;
        Check(motorLegReached, "Actual player motor reaches " + label + " without a jump or teleport");
    }
    IEnumerator OccupiedEntryQueue()
    {
        yield return Restart();
        // Fixture setup only: occupy the announced gallery entry with the actual player.
        player.TeleportTo(director.sniperSpawn.position + Vector3.up * 1.05f, director.sniperSpawn.rotation);
        director.BeginEncounter();
        director.RequestReinforcements();
        yield return new WaitForSeconds(4);
        Check(director.SpawnWaits > 0 && director.TotalSpawned >= 6 && director.PendingSpawns == 1,
            "An occupied first reinforcement entry does not strand the other two physical entries");
        Check(!director.Enemies.Any(e => e != null && !e.IsMelee && e.IsVeteran),
            "The blocked Aguja entry does not materialize a sniper on the occupying player");
        Info("Occupied-entry fixture: spawned=" + director.TotalSpawned + ", pending=" + director.PendingSpawns + ", blocked attempts=" + director.SpawnWaits + "; no freeze or healing during its four seconds.");
        yield return Restart();
    }
    IEnumerator ChoiceTrace(bool breakScreen)
    {
        yield return Restart();
        string label = breakScreen ? "choice-break" : "choice-intact";
        Vector3 near = Anchor("QA.BreachSouth").position, far = Anchor("QA.BreachNorth").position;
        Vector3 arrival = Anchor("QA.Arrival").position;
        player.TeleportTo(near, Quaternion.identity); // Preparation only; the crossing itself uses the motor.
        UnityEngine.Random.InitState(231024);
        director.BeginEncounter();
        var target = director.Enemies.Where(e => e != null && !e.IsMelee && !e.IsVeteran)
            .OrderBy(e => Vector3.Distance(e.transform.position, director.sentrySpawns[1].position)).FirstOrDefault();
        Check(target != null, label + ": the north-east defender is a live encounter robot");
        if (target == null) yield break;
        Aim(target.AimPoint);
        BeginObservation(label, false, "Focused scripted comparison, not an intelligent choice: identical initial equipment/four roles/seed 231024, 1.25s exposure, then detour or shoot the screen under live pressure and cross. No freeze, QA healing or teleport during the trace. First sight means a physical ray, not human recognition.");
        var report = active;
        float start = Time.time, next = 0, nextFrame = 0;
        Vector3 previous = player.transform.position;
        while (!player.IsDead && Time.time - start < 12)
        {
            float elapsed = Time.time - start;
            bool visible = Visible(target);
            if (visible && report.firstNeSightSeconds < 0)
            {
                report.firstNeSightSeconds = elapsed;
                Capture(Path.Combine(label, "first-ne-sight.png"));
            }
            if ((target == null || target.IsDead) && report.neKilledSeconds < 0) report.neKilledSeconds = elapsed;
            if (Vector3.Distance(player.transform.position, far) < .8f) report.reachedFarSide = true;
            if (report.reachedFarSide && report.neKilledSeconds >= 0) break;
            if (elapsed < 1.25f)
            {
                observationPhase = "initial-live-pressure";
                player.StepMovement(Time.deltaTime, Vector2.zero, false);
            }
            else if (breakScreen && !director.Breach.IsOpen)
            {
                observationPhase = "shoot-screen-under-pressure";
                Aim(ScreenTarget());
                int ammo = pistol.currentAmmo; float health = director.Breach.Health;
                if (ammo == 0) pistol.Reload(); else pistol.Use();
                if (pistol.currentAmmo < ammo) { report.structuralShots++; report.shots++; }
                report.structuralDamage += Mathf.Max(0, health - director.Breach.Health);
                player.StepMovement(Time.deltaTime, Vector2.zero, false);
            }
            else
            {
                observationPhase = breakScreen ? "cross-open-screen" : "detour-intact-screen";
                if (visible)
                {
                    Aim(target.AimPoint);
                    int ammo = pistol.currentAmmo;
                    if (ammo == 0) pistol.Reload(); else pistol.Use();
                    if (pistol.currentAmmo < ammo) report.shots++;
                }
                if (!report.reachedFarSide) MoveToward(far);
                else player.StepMovement(Time.deltaTime, Vector2.zero, false);
            }
            active.distance += Vector3.Distance(previous, player.transform.position); previous = player.transform.position;
            if (elapsed >= next) { Record(elapsed, arrival); next += .1f; }
            if (elapsed >= nextFrame)
            {
                Capture(Path.Combine(label, "frame-" + Mathf.FloorToInt(elapsed).ToString("D3") + ".png")); nextFrame += 1;
            }
            yield return null;
        }
        EndObservation(Time.time - start, arrival);
        Info(label + ": " + activeSummary + ", firstNEsight=" + F(report.firstNeSightSeconds) + ", NEkilled=" + F(report.neKilledSeconds) +
            ", structuralShots=" + report.structuralShots + ", structuralDamage=" + F(report.structuralDamage) + ", reachedFar=" + report.reachedFarSide +
            ". A difference here does not prove a voluntary decision or fun.");
        Capture(label + ".png");
    }
    IEnumerator ShootScreen()
    {
        if (director.Breach.IsOpen) yield break;
        float end = Time.realtimeSinceStartup + 8;
        while (!director.Breach.IsOpen && !player.IsDead && Time.realtimeSinceStartup < end)
        {
            // Repeatedly firing into a hole after local fracture would test empty space.
            // Select a physically visible remaining surface; still use the real pistol/muzzle.
            Aim(ScreenTarget()); if (pistol.currentAmmo == 0) pistol.Reload(); else pistol.Use();
            yield return null;
        }
        yield return Until(() => director.Breach.IsOpen && director.Breach.PassageClear, 5, "Actual weapon fire opens a collider-clear screen");
        yield return new WaitForSeconds(.3f);
    }
    Vector3 ScreenTarget()
    {
        var structure = director.Breach;
        var candidates = structure.Source.GetComponentsInChildren<Collider>()
            .Concat(structure.Source.GeneratedGraph.GetComponentsInChildren<Collider>())
            .Where(c => c.enabled && c.gameObject.activeInHierarchy)
            .OrderBy(c => Vector3.Distance(c.bounds.center, structure.transform.position));
        foreach (var collider in candidates)
        {
            Vector3 point = collider.bounds.center, direction = point - view.transform.position;
            if (Physics.Raycast(view.transform.position, direction.normalized, out var hit, direction.magnitude + .2f,
                WorldMask, QueryTriggerInteraction.Ignore) && FoundryStructure.From(hit.collider) == structure) return point;
        }
        return structure.transform.position;
    }
    IEnumerator Prepare(bool open)
    {
        yield return Restart();
        if (open)
        {
            player.TeleportTo(Anchor("QA.BreachSouth").position, Quaternion.identity); yield return ShootScreen();
            // Magazine preparation only; armed observations reload normally.
            pistol.currentAmmo = pistol.ammo; pistol.Action.Cancel();
        }
        player.TeleportTo(Anchor("QA.Arrival").position, director.arrival.rotation);
        UnityEngine.Random.InitState(231023);
        director.BeginEncounter();
        yield return new WaitForSeconds(.2f);
    }
    IEnumerator ObservationProbe(string label, bool orbit, bool open)
    {
        yield return Prepare(open);
        var arrival = Anchor("QA.Arrival").position;
        var points = Enumerable.Range(0, 4).Select(i => Anchor("QA.Orbit." + i).position).ToArray();
        BeginObservation(label, open, "12 seconds without shooting. Random seed 231023; instance-dependent AI schedules can vary. Real entry/advancement triggers remain; orbit may activate reinforcements that waiting does not. No causal comparison of identical enemy pressure is claimed.");
        float start = Time.time, next = 0; int waypoint = 0; Vector3 previous = player.transform.position;
        while (!player.IsDead && Time.time - start < 12)
        {
            if (orbit)
            {
                if (HorizontalDistance(player.transform.position, points[waypoint]) < 1) waypoint = (waypoint + 1) % points.Length;
                MoveToward(points[waypoint]);
            }
            else player.StepMovement(Time.deltaTime, Vector2.zero, false);
            active.distance += Vector3.Distance(previous, player.transform.position); previous = player.transform.position;
            if (Time.time - start >= next) { Record(Time.time - start, arrival); next += .25f; }
            yield return null;
        }
        EndObservation(Time.time - start, arrival); Capture(label + ".png");
        Info(label + ": " + activeSummary + ". Observation only; not a pass/fail for fun, fairness or strategy quality.");
    }
    void BeginObservation(string name, bool open, string limitation)
    {
        attackCounts.Clear();
        observationPhase = "native-probe";
        active = new Observation { name = name, screenOpen = open, limitation = limitation }; observations.observations.Add(active);
    }
    void Record(float seconds, Vector3 arrival)
    {
        var alive = director.Enemies.Where(e => e != null && !e.IsDead).ToArray();
        var all = director.Enemies.Where(e => e != null).ToArray(); Vector3 p = player.transform.position;
        foreach (var enemy in all)
        {
            var melee = enemy.GetComponent<MeleeRobotCombat>();
            attackCounts[enemy.GetInstanceID()] = new[] { enemy.Ranged != null ? enemy.Ranged.ProjectilesLaunched : 0,
                enemy.Ranged != null ? enemy.Ranged.LaserShotsFired : 0, melee != null ? melee.AttacksStarted : 0 };
        }
        active.samples.Add(new Sample
        {
            phase = observationPhase, seconds = seconds, x = p.x, y = p.y, z = p.z, health = player.Health, shield = player.GetComponent<Shield>()?.Current ?? 0,
            healthDamage = active.healthDamage, shieldDamage = active.shieldDamage, distance = active.distance,
            wave = director.Wave, alive = alive.Length, melee = alive.Count(e => e.IsMelee), ranged = alive.Count(e => !e.IsMelee),
            nearArrival = alive.Count(e => HorizontalDistance(e.transform.position, arrival) < 6), visible = alive.Count(Visible),
            outsideSector = alive.Count(OutsideSector),
            pendingSpawns = director.PendingSpawns, spawned = director.TotalSpawned, spawnWaits = director.SpawnWaits,
            projectiles = attackCounts.Values.Sum(v => v[0]), lasers = attackCounts.Values.Sum(v => v[1]), charges = attackCounts.Values.Sum(v => v[2])
        });
    }
    void EndObservation(float seconds, Vector3 arrival)
    {
        Record(seconds, arrival); active.seconds = seconds; active.died = player.IsDead;
        active.collectedCore = director.CoreCollected; active.cleared = director.AssaultCleared; active.exited = director.Completed;
        activeSummary = seconds.ToString("F1", CultureInfo.InvariantCulture) + "s, distance " + active.distance.ToString("F1", CultureInfo.InvariantCulture) +
            "m, damage " + (active.healthDamage + active.shieldDamage).ToString("F1", CultureInfo.InvariantCulture) + ", dead=" + active.died;
        active = null;
    }
    static float HorizontalDistance(Vector3 a, Vector3 b) { return Vector3.ProjectOnPlane(a - b, Vector3.up).magnitude; }
    static bool OutsideSector(NormalEnemyStateMachine enemy)
    {
        var member = enemy.GetComponent<FoundrySectorMember>();
        if (member == null || member.sector == null) return false;
        var tolerance = member.sector.bounds;
        tolerance.Expand(.5f); // Bounds.Expand changes total size: .25m tolerance on each side for avoidance.
        return !tolerance.Contains(enemy.transform.position);
    }
    void MoveToward(Vector3 destination)
    {
        if (!CompletePath(player.transform.position, destination, out var path, out _) || path.corners.Length == 0)
        {
            player.StepMovement(Time.deltaTime, Vector2.zero, false);
            ObserveMotor(destination, path);
            return;
        }
        Vector3 next = path.corners[path.corners.Length - 1];
        for (int i = 1; i < path.corners.Length; i++)
            if (HorizontalDistance(player.transform.position, path.corners[i]) > .2f) { next = path.corners[i]; break; }
        Vector3 delta = Vector3.ProjectOnPlane(next - player.transform.position, Vector3.up);
        Vector3 direction = delta.sqrMagnitude > .0025f ? delta.normalized * Mathf.Min(1, delta.magnitude) : Vector3.zero;
        var input = new Vector2(Vector3.Dot(direction, player.transform.right), Vector3.Dot(direction, player.transform.forward));
        player.StepMovement(Time.deltaTime, input, false);
        ObserveMotor(destination, path);
    }
    void ObserveMotor(Vector3 destination, NavMeshPath path)
    {
        if (motorProgressAt < 0 || Vector3.Distance(destination, motorDestination) > 1 || Vector3.Distance(player.transform.position, motorProgressPosition) > .15f)
        {
            motorProgressAt = Time.time; motorNextReport = Time.time + 2;
            motorProgressPosition = player.transform.position; motorDestination = destination;
            return;
        }
        if (Time.time < motorNextReport || Vector3.Distance(player.transform.position, destination) < 3) return;
        motorNextReport = Time.time + 8;
        bool sourceFound = NavMesh.SamplePosition(player.transform.position, out var source, 1.5f, NavMesh.AllAreas);
        bool destinationFound = NavMesh.SamplePosition(destination, out var goal, 1.5f, NavMesh.AllAreas);
        string corners = path == null ? "none" : string.Join(" > ", path.corners.Select(V));
        Info("MOTOR STALL " + (active != null ? active.name : "dry-access") + " for=" + F(Time.time - motorProgressAt) +
            " position=" + V(player.transform.position) + " destination=" + V(destination) + " grounded=" + player.Grounded +
            " velocity=" + V(player.PlayerVelocity) + " sampledSource=" + sourceFound + ":" + V(source.position) +
            " sampledGoal=" + destinationFound + ":" + V(goal.position) + " path=" + (path == null ? "none" : path.status.ToString()) + " corners=" + corners);
    }
    bool Visible(NormalEnemyStateMachine enemy)
    {
        if (enemy == null || enemy.IsDead || view == null) return false;
        Vector3 direction = enemy.AimPoint - view.transform.position;
        return Physics.Raycast(view.transform.position, direction.normalized, out var hit, direction.magnitude + .2f,
            ~(1 << 7 | 1 << 8 | 1 << 2), QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<NormalEnemyStateMachine>() == enemy;
    }
    void FireVisible()
    {
        var enemy = director.Enemies.Where(Visible).OrderBy(e => Vector3.Distance(player.transform.position, e.transform.position)).FirstOrDefault();
        if (enemy == null) return;
        Aim(enemy.AimPoint); var gun = TowerSession.Instance.weapons.currentWeapon as BaseGun;
        if (gun == null) return;
        if (gun.currentAmmo == 0) gun.Reload();
        else { int before = gun.currentAmmo; gun.Use(); if (active != null && gun.currentAmmo < before) active.shots++; }
    }
    void InteractObjective(Transform target)
    {
        if (target == null || Vector3.Distance(player.headCam.position, target.position) > 3) return;
        Aim(target.position);
        // Invoke the existing E-handler: its real three-metre ray must hit the objective,
        // so neither the floor below the cradle nor a wall can be bypassed by this trace.
        typeof(PlayerInteraction).GetMethod("Interact", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(player.GetComponent<PlayerInteraction>(), null);
    }
    void ObserveBrute(string label, float gallerySeconds)
    {
        foreach (var enemy in director.Enemies.Where(e => e != null && !e.IsDead && e.IsMelee && e.IsVeteran))
        {
            var melee = enemy.GetComponent<MeleeRobotCombat>();
            var cc = player.GetComponent<CharacterController>();
            Vector3 origin = enemy.transform.position + Vector3.up * 1.12f;
            Vector3 contact = cc.ClosestPoint(origin), direction = contact - origin;
            bool ray = direction.sqrMagnitude < .0001f;
            string blocker = "none";
            if (!ray && Physics.Raycast(origin, direction.normalized, out var hit, direction.magnitude + .08f, enemy.layerMask, QueryTriggerInteraction.Ignore))
            {
                ray = hit.collider.GetComponentInParent<PlayerStateMachine>() == player;
                blocker = hit.collider.name;
            }
            bool clearContact = Mathf.Abs(contact.y - origin.y) <= .65f && ray;
            bool nav = enemy.agent != null && enemy.agent.enabled && enemy.agent.isOnNavMesh;
            Info("BRUTE OBS " + label + " gallery=" + F(gallerySeconds) + " enemy=" + V(enemy.transform.position) + " player=" + V(player.transform.position) +
                " flat=" + F(HorizontalDistance(enemy.transform.position, player.transform.position)) + " deltaY=" + F(player.transform.position.y - enemy.transform.position.y) +
                " visible=" + enemy.Perception.Visible + " canAttack=" + (SquadManager.Instance == null || SquadManager.Instance.CanAttack(enemy)) +
                " contactHeight=" + F(Mathf.Abs(contact.y - origin.y)) + " contactRay=" + ray + " contactClear=" + clearContact + " firstCollider=" + blocker +
                " phase=" + melee.Phase + " primary=" + CombatHazardBudget.ActivePrimary + " remaining=" + (nav ? F(enemy.agent.remainingDistance) : "n/a") +
                " stopping=" + (nav ? F(enemy.agent.stoppingDistance) : "n/a") + " destination=" + (nav ? V(enemy.agent.destination) : "n/a") +
                " chargeStop=" + melee.ChargeStopReason + " chargeTravel=" + F(melee.ChargeTravel) + " contactsResolved=" + melee.ContactsResolved);
        }
    }
    IEnumerator CombatAdvance(bool open)
    {
        yield return Prepare(open);
        string label = open ? "advance-open" : "advance-intact";
        Vector3 arrival = Anchor("QA.Arrival").position, exit = Anchor("QA.Exit").position, coreApproach = Anchor("QA.Core").position;
        BeginObservation(label, open, "Armed automated trace: real weapons, reload, E-handler and movement; perfect target acquisition and NavMesh knowledge; no jump, slide, QA healing or damage bypass. On the gallery: eight seconds still without shooting, then eight still while firing. Failure of this limited controller is not proof a human route is impossible.");
        float start = Time.time, next = 0, nextFrame = 0; int waypoint = 0;
        bool openExitPath = false;
        float galleryStart = -1, galleryDamage = 0;
        bool galleryFireAnnounced = false, galleryEndAnnounced = false;
        var northLanding = GameObject.Find("North gallery landing and objective");
        var northDeck = northLanding != null ? northLanding.GetComponent<Collider>() : null;
        bool deckHoldEligible = false, stayedOverDeck = false, deckPenetrated = false;
        var loop = Enumerable.Range(0, 4).Select(i => Anchor("QA.Orbit." + i).position).ToArray(); Vector3 previous = player.transform.position;
        while (!director.Completed && !player.IsDead && Time.time - start < 110)
        {
            Vector3 destination;
            if (!director.CoreCollected) destination = coreApproach;
            else if (director.AssaultCleared) destination = exit;
            else
            {
                if (HorizontalDistance(player.transform.position, loop[waypoint]) < 1) waypoint = (waypoint + 1) % loop.Length;
                destination = loop[waypoint];
            }
            if (director.CoreCollected && galleryStart < 0)
            {
                galleryStart = Time.time;
                galleryDamage = active.healthDamage + active.shieldDamage;
                deckHoldEligible = player.transform.position.y > 3.4f && northDeck != null && northDeck.enabled && northDeck.gameObject.activeInHierarchy;
                stayedOverDeck = OverNorthDeck(player.transform.position);
                Info(label + ": gallery hold begins at " + V(player.transform.position) + ", alive=" + director.AliveCount + ", pending=" + director.PendingSpawns);
            }
            float galleryTime = galleryStart < 0 ? -1 : Time.time - galleryStart;
            if (galleryTime >= 0 && galleryTime < 16)
            {
                observationPhase = galleryTime < 8 ? "gallery-observe" : "gallery-fire";
                if (galleryTime >= 8)
                {
                    if (!galleryFireAnnounced)
                    {
                        Info(label + ": gallery no-fire hold received damage=" + F(active.healthDamage + active.shieldDamage - galleryDamage) + ", alive=" + director.AliveCount);
                        galleryFireAnnounced = true;
                    }
                    FireVisible();
                }
                player.StepMovement(Time.deltaTime, Vector2.zero, false);
                if (deckHoldEligible)
                {
                    // Once the capsule genuinely leaves the footprint, a fall over its edge is valid.
                    stayedOverDeck &= OverNorthDeck(player.transform.position);
                    if (stayedOverDeck && northDeck.enabled && northDeck.gameObject.activeInHierarchy &&
                        player.transform.position.y < northDeck.bounds.max.y + player.GetComponent<CharacterController>().height * .5f - .35f && !deckPenetrated)
                    {
                        deckPenetrated = true;
                        Info(label + ": DECK PENETRATION during zero-input hold from=" + V(previous) + " to=" + V(player.transform.position) +
                            " deckTop=" + F(northDeck.bounds.max.y) + "; the capsule has never left the north landing footprint.");
                    }
                }
            }
            else
            {
                if (galleryTime >= 16 && !galleryEndAnnounced)
                {
                    Info(label + ": gallery complete 8+8s hold damage=" + F(active.healthDamage + active.shieldDamage - galleryDamage) + ", alive=" + director.AliveCount + "; zero surviving threats would make the safety test inconclusive.");
                    galleryEndAnnounced = true;
                }
                observationPhase = !director.CoreCollected ? "approach-core" : director.AssaultCleared ? "reach-exit" : "combat-loop";
                FireVisible(); MoveToward(destination);
            }
            if (!director.CoreCollected && Vector3.Distance(player.transform.position, coreApproach) < 1) InteractObjective(director.corePickup);
            if (director.ExitOpen) openExitPath |= CompletePath(player.transform.position, exit, out _, out _);
            if (director.CoreCollected && director.AssaultCleared && Vector3.Distance(player.transform.position, exit) < 2.6f) InteractObjective(director.exitPoint);
            active.distance += Vector3.Distance(previous, player.transform.position); previous = player.transform.position;
            if (Time.time - start >= next) { Record(Time.time - start, arrival); next += .5f; }
            if (Time.time - start >= nextFrame)
            {
                if (galleryTime >= 0 && galleryTime < 16) ObserveBrute(label, galleryTime);
                Capture(Path.Combine(label, "frame-" + Mathf.FloorToInt(Time.time - start).ToString("D3") + ".png"));
                nextFrame += 2;
            }
            yield return null;
        }
        bool completed = director.Completed, stillIntact = !director.Breach.IsOpen;
        EndObservation(Time.time - start, arrival);
        Info(label + ": " + activeSummary + ", completed=" + completed + ". Human evaluation remains required.");
        // Integration of this limited motor/weapon/goal controller, not difficulty or fun.
        Check(completed, label + ": the limited armed controller reaches the actual core/clear/exit objective");
        if (deckHoldEligible && (stayedOverDeck || deckPenetrated))
            Check(!deckPenetrated, label + ": zero-input hold never passes through the enabled north deck while remaining inside its footprint");
        if (director.ExitOpen) Check(openExitPath, label + ": the unlocked extraction has an observed complete navigation route");
        if (!open) Info("Initially intact trace ends with screen intact=" + stillIntact + "; enemy impacts or physical muzzle obstruction can cause emergent destruction.");
        Capture(label + ".png");
        Check(!TowerSession.Instance.Rewards.Pending && TowerSession.Instance.RunWaves == 0, "The local objective does not grant tower progress");
    }
    static bool OverNorthDeck(Vector3 position) => position.x >= -3.2f && position.x <= 1.2f && position.z >= 4.8f && position.z <= 9.2f;
    IEnumerator Restart()
    {
        active = null; var previous = director; previous.RestartEncounter(); yield return null;
        yield return Until(() => Object.FindFirstObjectByType<FoundryArenaDirector>() != null &&
            Object.FindFirstObjectByType<FoundryArenaDirector>() != previous && Object.FindFirstObjectByType<FoundryArenaDirector>().Ready,
            75, "Restart loads a fresh level and player state");
        Bind(); if (director == null || player == null) yield break;
        yield return Until(() => director.Breach != null && director.Breach.Ready, 30, "Restart reconstructs the screen fracture graph");
        Check(!director.Breach.IsOpen && !director.CoreCollected && !director.AssaultCleared && !director.Completed && director.Wave == 0,
            "Restart restores the screen and spatial objective");
        Check(!player.IsDead && !WeaponAction.CombatPaused, "Restart restores a living unpaused player");
        Check(EnemyProjectile.ActiveCount == 0 && CombatHazardBudget.ActiveSalvos == 0 && CombatHazardBudget.ActivePrimary == 0,
            "Restart leaves no projectile or hazard lease");
        Check(Object.FindObjectsByType<PlayerStateMachine>(FindObjectsSortMode.None).Length == 1, "Restart keeps exactly one shared player");
    }
    void Aim(Vector3 point)
    {
        if (view == null) return;
        var look = Quaternion.LookRotation(point - view.transform.position);
        player.transform.rotation = Quaternion.Euler(0, look.eulerAngles.y, 0);
        player.headCam.localRotation = Quaternion.Euler(look.eulerAngles.x, 0, 0); Physics.SyncTransforms();
    }
    void Capture(string file)
    {
        if (view == null || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        var target = new RenderTexture(1280, 720, 24); target.Create();
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(view, new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
        var previous = RenderTexture.active; RenderTexture.active = target;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
        string path = Path.Combine(output, file);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, image.EncodeToPNG()); RenderTexture.active = previous;
        Destroy(image); target.Release(); Destroy(target);
    }
    void Finish()
    {
        if (finished) return;
        finished = true; active = null; Time.timeScale = 1;
        if (player != null) player.Damaged -= Damage;
        Application.logMessageReceived -= Log;
        var samples = observations.observations.SelectMany(o => o.samples).ToArray();
        if (samples.Length > 0)
        {
            Check(samples.All(s => s.outsideSector == 0), "Observed sector guards stay within their assigned bounds (0.25m avoidance tolerance)");
            Check(samples.All(s => s.alive <= 6), "Observed simultaneous population respects the six-robot ceiling");
        }
        File.WriteAllText(Path.Combine(output, "results.txt"), string.Join("\n", checks) + "\nERRORS\n" + string.Join("\n", errors));
        if (!smoke)
        {
            File.WriteAllText(Path.Combine(output, "observations.json"), JsonUtility.ToJson(observations, true));
            var csv = new List<string> { "observation,phase,seconds,x,y,z,health,shield,healthDamage,shieldDamage,distance,wave,alive,melee,ranged,visible,nearArrival,outsideSector,pendingSpawns,spawned,spawnWaits,projectiles,lasers,charges" };
            foreach (var observation in observations.observations)
                foreach (var s in observation.samples)
                    csv.Add(string.Join(",", new[] { observation.name, s.phase, F(s.seconds), F(s.x), F(s.y), F(s.z), F(s.health), F(s.shield), F(s.healthDamage),
                        F(s.shieldDamage), F(s.distance), s.wave.ToString(), s.alive.ToString(), s.melee.ToString(), s.ranged.ToString(), s.visible.ToString(),
                        s.nearArrival.ToString(), s.outsideSector.ToString(), s.pendingSpawns.ToString(), s.spawned.ToString(), s.spawnWaits.ToString(),
                        s.projectiles.ToString(), s.lasers.ToString(), s.charges.ToString() }));
            File.WriteAllLines(Path.Combine(output, "observations.csv"), csv);
        }
        Debug.Log((smoke ? "FOUNDRY NORMAL SMOKE COMPLETE: " : "FOUNDRY LEVEL VALIDATION COMPLETE: ") + checks.Count + " entries, " + errors.Count + " errors. Controlled observations do not measure fun.");
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
