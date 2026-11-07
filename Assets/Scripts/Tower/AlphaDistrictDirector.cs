using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Independent district: objectives persist across connected combat areas; extraction never requires an empty map.</summary>
public sealed class AlphaDistrictDirector : MonoBehaviour
{
    public Transform arrival, exitPoint, containmentEntry, extractionVehicle, nestSpawn;
    public AlphaDistrictZone[] zones;
    public AlphaDistrictTerminal[] terminals;
    public NormalEnemyStateMachine rangedPrefab, meleePrefab;
    public AlphaDistrictCollapse collapse;
    public FoundryStructure containment;
    public FoundryCombatSector nestSector;
    public bool Ready { get; private set; }
    public bool Completed { get; private set; }
    public bool AlarmCalled { get; private set; }
    public bool PowerA { get; private set; }
    public bool PowerB { get; private set; }
    public float SecondsToExtraction => AlarmCalled ? Mathf.Max(0, 30 - alarmSeconds) : 30;
    public int CurrentZoneIndex { get; private set; }
    public int AliveCount => enemies.Count(e => e && !e.IsDead);
    public int PendingSpawns => requests.Count + (entryWarning ? 1 : 0);
    public int SpawnedTotal { get; private set; }
    public int RetiredCount { get; private set; }
    public int SpawnWaits { get; private set; }
    public int PeakAlive { get; private set; }
    public bool EncounterStarted { get; private set; }
    public string PressurePhase { get; private set; } = "Llegada";
    public PlayerStateMachine Player { get; private set; }
    public IReadOnlyList<NormalEnemyStateMachine> Enemies => enemies;
    public bool ContainmentOpen => containment && containment.IsOpen;
    readonly List<NormalEnemyStateMachine> enemies = new List<NormalEnemyStateMachine>();
    readonly Dictionary<NormalEnemyStateMachine, float> lastNear = new Dictionary<NormalEnemyStateMachine, float>();
    readonly Queue<Request> requests = new Queue<Request>();
    readonly bool[] charging = new bool[2];
    readonly float[] charge = new float[2];
    readonly System.Random random = new System.Random(251025);
    sealed class Request { public bool ranged, veteran; public int zone; }
    EnemyEntrySignal entryWarning;
    GunController weapons;
    AlphaDistrictVisuals visuals;
    Camera worldCamera;
    IDisposable endPause;
    bool qa, localDeath, restarting, medicalUsed, containmentObserved, mapVisible;
    float clock, nextSpawn, alarmSeconds, messageUntil, nextRetirement;
    string message;
    Vector3 vehicleHome;
    AudioSource audioSource;
    AudioClip signal;

    IEnumerator Start()
    {
        qa = Environment.GetCommandLineArgs().Contains("-district-qa");
        if (!TowerSession.Instance) yield return SceneManager.LoadSceneAsync("TowerCore", LoadSceneMode.Additive);
        while (!TowerSession.Instance || !TowerSession.Instance.Ready) yield return null;
        yield return null;
        SceneManager.SetActiveScene(gameObject.scene);
        GameManager.Instance.AutomaticWaves = false;
        GameManager.Instance.StopFloor(); // Bootstrap only: never discard the encounter when crossing a zone.
        var session = TowerSession.Instance;
        session.firstScene = "PuertoResonancia";
        session.DeathPresentationOverride = () => { localDeath = true; ClearWarning(); };
        Player = session.player; weapons = session.weapons;
        worldCamera = Player.GetComponentsInChildren<Camera>().FirstOrDefault(c => c.gameObject.layer != 8);
        Player.TeleportTo(arrival.position, arrival.rotation);
        weapons.WeaponAvailable = w => w is Pistol || w is MachineGun || w is Knife || PowerA;
        foreach (var gun in Player.GetComponentsInChildren<BaseGun>(true)) { gun.limitedReserve = false; gun.currentAmmo = gun.ammo; }
        int carbine = weapons.Weapons.ToList().FindIndex(w => w is MachineGun); if (carbine >= 0) weapons.EquipImmediate(carbine);
        if (session.overlay) session.overlay.gameObject.SetActive(false);
        var hud = FindFirstObjectByType<LabHUD>();
        if (hud)
        {
            if (hud.waveText) hud.waveText.gameObject.SetActive(false);
            if (hud.scoreText) hud.scoreText.gameObject.SetActive(false);
            foreach (var item in hud.GetComponentsInChildren<Transform>(true))
                if (item.name == "Mission glass" || item.name == "Wallet glass") item.gameObject.SetActive(false);
        }
        visuals = FindFirstObjectByType<AlphaDistrictVisuals>();
        if (visuals) visuals.Stylize(Player.gameObject);
        if (extractionVehicle) { vehicleHome = extractionVehicle.position; extractionVehicle.position = vehicleHome + Vector3.back * 30; }
        audioSource = gameObject.AddComponent<AudioSource>(); signal = ImpactFeedbackPlayer.MakeSound("Port signal", 1);
        yield return new WaitForSeconds(.45f);
        Ready = true; CurrentZoneIndex = ZoneAt(Player.transform.position);
        StartCoroutine(Entries());
        Tell("Activa las bobinas del TALLER y el CORAZÓN DE RAÍCES. Elige tu ruta.", 8);
        if (!qa) StartEncounter();
    }
    public float TerminalProgress(int kind) => kind >= 0 && kind < 2 ? charge[kind] / 18 : 0;
    public bool IsCharging(int kind) => kind >= 0 && kind < 2 && charging[kind];
    public void StartEncounter()
    {
        if (!Ready || EncounterStarted || Completed || WeaponAction.CombatPaused) return;
        EncounterStarted = true; clock = 0; nextSpawn = Time.time + 2;
        SquadManager.Instance?.Configure(4, 3);
        if (nestSpawn && (!collapse || !collapse.IsSettled) && NavMesh.SamplePosition(nestSpawn.position, out var hit, .7f, NavMesh.AllAreas))
            CreateEnemy(new Request { ranged = true, veteran = true, zone = 4 }, hit.position, nestSector);
        Burst(3, CurrentZoneIndex);
    }
    public bool TryUse(int kind)
    {
        if (!Ready || Completed || WeaponAction.CombatPaused || localDeath) return false;
        var terminal = terminals.FirstOrDefault(t => t && t.kind == kind);
        if (!terminal || Vector3.Distance(Player.headCam.position, terminal.transform.position) > 3.25f) return false;
        Vector3 direction = terminal.transform.position - Player.headCam.position;
        if (!Physics.Raycast(Player.headCam.position, direction.normalized, out var hit, direction.magnitude + .15f,
            ~(1 << 7 | 1 << 8 | 1 << 2), QueryTriggerInteraction.Ignore) || hit.collider.GetComponentInParent<AlphaDistrictTerminal>() != terminal) return false;
        StartEncounter();
        if (kind < 2)
        {
            if (charging[kind] || (kind == 0 ? PowerA : PowerB)) return false;
            charging[kind] = true; Burst(5, terminal.zoneIndex); audioSource.PlayOneShot(signal, .6f);
            Tell("CARGA EN MARCHA · Mantente en este distrito y combate entre sus accesos.", 5); return true;
        }
        if (kind == 2)
        {
            if (AlarmCalled || !PowerA || !PowerB) { Tell("Faltan las bobinas del Taller o el Corazón de Raíces.", 3); return false; }
            AlarmCalled = true; alarmSeconds = 0; Burst(7, CurrentZoneIndex); audioSource.PlayOneShot(signal, .8f);
            Tell("EVACUACIÓN EN 30 SEGUNDOS · Regresa al muelle por cualquiera de los circuitos.", 8); return true;
        }
        if (kind == 3)
        {
            if (!AlarmCalled || SecondsToExtraction > 0) { Tell("El transporte aún no está listo. Solicítalo en la Plaza tras alimentar los dos sistemas.", 4); return false; }
            Completed = true; requests.Clear(); ClearWarning(); EnemyProjectile.ClearAll();
            endPause = RunPause.Acquire(true); Tell("PUERTO ASEGURADO · ALPHA COMPLETADA", 999); return true;
        }
        if (kind == 4 && !medicalUsed)
        {
            if (Player.Health >= Player.MaxHealth) { Tell("SALUD COMPLETA · El botiquín permanece disponible.", 3); return false; }
            medicalUsed = true; Player.Heal(35); audioSource.PlayOneShot(signal, .45f); Tell("BOTIQUÍN CONSUMIDO · +35 salud", 3); return true;
        }
        return false;
    }
    void Update()
    {
        if (Ready && Input.GetKeyDown(KeyCode.F5)) { RestartLevel(); return; }
        if (Ready && Input.GetKeyDown(KeyCode.M)) mapVisible = !mapVisible;
        if (!Ready || Completed || localDeath || WeaponAction.CombatPaused) return;
        if (Player.transform.position.y < -12) { Player.TakeDamage(1000, Player.transform.position, "Caída al puerto"); return; }
        int zone = ZoneAt(Player.transform.position);
        if (zone != CurrentZoneIndex)
        {
            CurrentZoneIndex = zone;
            if (EncounterStarted) Burst(2, zone);
        }
        for (int kind = 0; kind < 2; kind++)
        {
            if (!charging[kind]) continue;
            var terminal = terminals.FirstOrDefault(t => t && t.kind == kind);
            if (terminal && zone == terminal.zoneIndex) charge[kind] = Mathf.Min(18, charge[kind] + Time.deltaTime);
            if (charge[kind] < 18) continue;
            charging[kind] = false; if (kind == 0) PowerA = true; else PowerB = true;
            Player.Heal(20); audioSource.PlayOneShot(signal, .7f);
            Tell(kind == 0 ? "BOBINAS ESTABLES · Escopeta disponible. Falta el otro sistema o la radio de la Plaza." : "CORAZÓN ESTABLE · Busca el otro sistema o solicita evacuación en la Plaza.", 6);
        }
        if (AlarmCalled)
        {
            alarmSeconds += Time.deltaTime;
            if (extractionVehicle) extractionVehicle.position = Vector3.Lerp(vehicleHome + Vector3.back * 30, vehicleHome, Mathf.Clamp01((alarmSeconds - 25) / 5));
        }
        if (ContainmentOpen && !containmentObserved)
        {
            containmentObserved = true; Burst(4, 5);
            Tell("CONTENCIÓN ROTA · Plaza y Raíces comparten paso. Los perseguidores también.", 6);
        }
        if (!EncounterStarted) return;
        clock += Time.deltaTime;
        string phase = AlarmCalled || charging.Any(v => v) ? "Pico" : clock % 52 < 18 ? "Subida" : clock % 52 < 30 ? "Pico" : clock % 52 < 40 ? "Descenso" : "Respiro";
        if (phase != PressurePhase && phase == "Respiro") requests.Clear();
        PressurePhase = phase;
        int target = phase == "Pico" ? 11 : phase == "Subida" ? 7 : 0;
        if (Time.time >= nextSpawn && AliveCount + PendingSpawns < target)
        { EnqueueRole(CurrentZoneIndex); nextSpawn = Time.time + (phase == "Pico" ? 1.7f : 3.4f); }
        PeakAlive = Mathf.Max(PeakAlive, AliveCount);
        RetireDistantPopulation();
    }
    public int ZoneAt(Vector3 point)
    {
        var containing = zones.Where(z => z && z.Contains(point)).ToArray();
        return (containing.Length > 0 ? containing : zones.Where(z => z))
            .OrderBy(z => Vector3.ProjectOnPlane(point - z.bounds.center, Vector3.up).sqrMagnitude).First().index;
    }
    void Burst(int count, int zone) { for (int i = 0; i < count && requests.Count < 12; i++) EnqueueRole(zone); }
    void EnqueueRole(int zone)
    {
        int ranged = enemies.Count(e => e && !e.IsDead && !e.IsMelee) + requests.Count(r => r.ranged);
        bool gunner = ranged < 3 && random.Next(4) == 0;
        bool veteran = !gunner && enemies.Count(e => e && !e.IsDead && e.IsMelee && e.IsVeteran) + requests.Count(r => r.veteran) == 0 && SpawnedTotal > 5;
        requests.Enqueue(new Request { ranged = gunner, veteran = veteran, zone = zone });
    }
    IEnumerator Entries()
    {
        while (true)
        {
            if (!EncounterStarted || Completed || localDeath || WeaponAction.CombatPaused || requests.Count == 0 || AliveCount >= 12 || PressurePhase == "Respiro")
            { yield return null; continue; }
            var request = requests.Dequeue();
            if (!FindEntry(request, out var point, out var sector))
            { requests.Enqueue(request); SpawnWaits++; yield return new WaitForSeconds(.45f); continue; }
            entryWarning = EnemyEntrySignal.Create(point);
            yield return new WaitForSeconds(1.05f);
            if (Completed || localDeath || PressurePhase == "Respiro") { ClearWarning(); continue; }
            if (!PhysicalEntry(point, 5)) { requests.Enqueue(request); ClearWarning(); continue; }
            CreateEnemy(request, point, request.ranged ? sector : null);
            ClearWarning(); yield return new WaitForSeconds(.25f);
        }
    }
    bool FindEntry(Request request, out Vector3 position, out FoundryCombatSector sector)
    {
        position = default; sector = null;
        var active = zones[CurrentZoneIndex];
        var allowed = new HashSet<int>(active.neighbors ?? new int[0]) { active.index };
        var candidates = new List<(Transform point, AlphaDistrictZone zone)>();
        foreach (var zone in zones.Where(z => z && allowed.Contains(z.index)))
            foreach (var point in zone.entries) if (point) candidates.Add((point, zone));
        if (ContainmentOpen && containmentEntry) candidates.Add((containmentEntry, zones.First(z => z.index == 5)));
        // Real navigation distance chooses legal nearby routes; unobserved arrivals can approach from either connected side.
        foreach (var item in candidates.OrderBy(item => item.zone.index == request.zone ? 0 : item.zone.index == CurrentZoneIndex ? 1 : 2).ThenBy(_ => random.Next()))
        {
            if (!NavMesh.SamplePosition(item.point.position, out var hit, .8f, NavMesh.AllAreas) || !PhysicalEntry(hit.position, 12)) continue;
            var path = new NavMeshPath();
            if (!NavMesh.SamplePosition(Player.transform.position, out var target, 1.5f, NavMesh.AllAreas) ||
                !NavMesh.CalculatePath(hit.position, target.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            float distance = PathLength(path); if (distance < 14 || distance > 60) continue;
            if (!Physics.Linecast(Player.headCam.position, hit.position + Vector3.up, out _, (1 << 0) | (1 << 3) | (1 << 10), QueryTriggerInteraction.Ignore)) continue;
            if (request.ranged && item.zone.rangedSector && !item.zone.rangedSector.Contains(hit.position)) continue;
            position = hit.position; sector = item.zone.rangedSector; return true;
        }
        return false;
    }
    bool PhysicalEntry(Vector3 point, float distance) => Vector3.Distance(point, Player.transform.position) >= distance &&
        !Physics.CheckCapsule(point + Vector3.up * .4f, point + Vector3.up * 1.5f, .34f,
            (1 << 0) | (1 << 3) | (1 << 6) | (1 << 7) | (1 << 10), QueryTriggerInteraction.Ignore);
    void CreateEnemy(Request request, Vector3 point, FoundryCombatSector sector)
    {
        var enemy = Instantiate(request.ranged ? rangedPrefab : meleePrefab, point, Quaternion.identity);
        SceneManager.MoveGameObjectToScene(enemy.gameObject, gameObject.scene);
        if (sector) enemy.gameObject.AddComponent<FoundrySectorMember>().sector = sector;
        enemy.ConfigureDifficulty(request.veteran ? 105 : 75, request.ranged ? 7 : 9, .8f);
        enemy.ConfigureVeteran(request.veteran); enemy.detectionRange = 34;
        enemy.Navigation.GroundPlaneMeleeApproach = !request.ranged;
        if (request.ranged) enemy.Ranged?.SetRange(32);
        else enemy.GetComponent<MeleeRobotCombat>().StopChargeAtPlayer = true;
        if (enemy.agent) enemy.agent.speed = request.ranged ? 3.6f : request.veteran ? 5.0f : 4.7f;
        enemy.Perception.SeedSearch(Player.transform.position);
        if (visuals) visuals.Stylize(enemy.gameObject);
        foreach (var structure in FindObjectsByType<FoundryStructure>(FindObjectsSortMode.None)) structure.IgnoreActorCollisions(enemy.GetComponentsInChildren<Collider>());
        enemies.Add(enemy); lastNear[enemy] = Time.time; SpawnedTotal++;
    }
    void RetireDistantPopulation()
    {
        if (Time.time < nextRetirement) return; nextRetirement = Time.time + 3;
        var active = zones[CurrentZoneIndex];
        var local = new HashSet<int>(active.neighbors ?? new int[0]) { active.index };
        foreach (var enemy in enemies.Where(e => e && !e.IsDead).ToArray())
        {
            if (local.Contains(ZoneAt(enemy.transform.position)) || enemy.Perception.Visible || PlayerSees(enemy))
            { lastNear[enemy] = Time.time; continue; }
            if (Time.time - lastNear[enemy] < 18) continue;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(enemy.transform.position, Player.transform.position - Vector3.up, NavMesh.AllAreas, path) || PathLength(path) < 80) continue;
            // Off-screen population outside the active neighbourhood returns to the pool; it grants no kill or progress.
            enemy.Ranged?.Cancel(); enemy.GetComponent<MeleeRobotCombat>()?.CancelOnDeath();
            Destroy(enemy.gameObject); RetiredCount++;
        }
    }
    bool PlayerSees(NormalEnemyStateMachine enemy)
    {
        if (!worldCamera) return true;
        Vector3 point = worldCamera.WorldToViewportPoint(enemy.AimPoint);
        return point.z > 0 && point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1 &&
            !Physics.Linecast(Player.headCam.position, enemy.AimPoint, out _, (1 << 0) | (1 << 3) | (1 << 10), QueryTriggerInteraction.Ignore);
    }
    static float PathLength(NavMeshPath path) { float n = 0; for (int i = 1; i < path.corners.Length; i++) n += Vector3.Distance(path.corners[i - 1], path.corners[i]); return n; }
    public void RestartLevel()
    {
        if (restarting) return; restarting = true; endPause?.Dispose(); endPause = null;
        RunPause.Reset(); EnemyProjectile.ClearAll(); CombatHazardBudget.ResetAll();
        SceneManager.LoadScene("PuertoResonancia");
    }
    void Tell(string text, float duration) { message = text; messageUntil = Time.time + duration; }
    void ClearWarning() { if (entryWarning) Destroy(entryWarning.gameObject); entryWarning = null; }
    void OnDestroy() { ClearWarning(); endPause?.Dispose(); if (signal) Destroy(signal); }
    void OnGUI()
    {
        if (!Ready)
        { GUI.color = new Color(.025f,.04f,.09f); GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture); GUI.color=Color.white; GUI.Label(new Rect(30,30,500,35),"CARGANDO PUERTO DE RESONANCIA…"); return; }
        GUI.color = new Color(.025f,.035f,.08f,.92f); GUI.DrawTexture(new Rect(18,16,745,107),Texture2D.whiteTexture); GUI.color = Color.white;
        var title = new GUIStyle(GUI.skin.label) {fontSize=23,fontStyle=FontStyle.Bold};
        GUI.Label(new Rect(32,22,710,30),"HELIX / PUERTO DE RESONANCIA · ALPHA",title);
        string objective = localDeath ? "OPERADOR CAÍDO · F5 para volver a empezar" : Completed ? "EVACUACIÓN COMPLETADA · F5 para repetir" :
            !PowerA || !PowerB ? "ALIMENTACIÓN  /  TALLER " + (PowerA?"✓":Mathf.CeilToInt(charge[0])+"/18") + "   ·   RAÍCES " + (PowerB?"✓":Mathf.CeilToInt(charge[1])+"/18") :
            !AlarmCalled ? "SOLICITA EVACUACIÓN EN LA PLAZA DEL REGULADOR" : SecondsToExtraction>0 ? "REGRESA AL MUELLE  /  TRANSPORTE EN "+Mathf.CeilToInt(SecondsToExtraction)+" s" : "ABORDA EL TRANSPORTE EN EL MUELLE / E";
        GUI.Label(new Rect(32,57,710,24),objective);
        GUI.Label(new Rect(32,82,710,24),zones[CurrentZoneIndex].label+"   ·   "+AliveCount+" hostiles   ·   E usar / M mapa / F5 reiniciar");
        if(message!=null&&Time.time<messageUntil)
        {
            GUI.color=new Color(.025f,.035f,.08f,.94f);GUI.DrawTexture(new Rect(18,125,745,40),Texture2D.whiteTexture);GUI.color=Color.white;
            GUI.Label(new Rect(32,130,717,36),message);
        }
        if(collapse&&collapse.SafetyBlocked)
        {
            GUI.color=new Color(.025f,.035f,.08f,.94f);GUI.DrawTexture(new Rect(Screen.width*.2f,Screen.height*.3f,Screen.width*.7f,44),Texture2D.whiteTexture);GUI.color=Color.white;
            GUI.Label(new Rect(Screen.width*.21f,Screen.height*.3f+7,Screen.width*.68f,34),"FRENO DE SEGURIDAD · Sal del área rayada para dejar caer el brazo.");
        }
        if(!WeaponAction.CombatPaused&&Physics.Raycast(Player.headCam.position,Player.headCam.forward,out var hit,3,~(1<<7|1<<8|1<<2),QueryTriggerInteraction.Ignore))
        {
            var terminal=hit.collider.GetComponentInParent<AlphaDistrictTerminal>();
            if(terminal)GUI.Label(new Rect(Screen.width/2-180,Screen.height/2+38,500,30),terminal.kind==3?"E  ABORDAR":terminal.kind==2?"E  SOLICITAR EVACUACIÓN":terminal.kind==4?"E  BOTIQUÍN (+35)":"E  ACTIVAR SISTEMA · Defiende el distrito");
        }
        if((localDeath||Completed)&&GUI.Button(new Rect(Screen.width/2-110,Screen.height/2,220,45),"REINICIAR PUERTO / F5"))RestartLevel();
        if (mapVisible) DrawMap();
    }
    void DrawMap()
    {
        var rect = new Rect(Screen.width - 354, 18, 336, 285);
        GUI.color = new Color(.025f,.035f,.08f,.96f); GUI.DrawTexture(rect,Texture2D.whiteTexture); GUI.color=Color.white;
        GUI.Label(new Rect(rect.x+12,rect.y+7,310,24),"PUERTO / NORTE ↑     M cerrar");
        Func<Vector3,Vector2> screen = point => new Vector2(rect.x+168+point.x*2.65f,rect.y+145-point.z*2.55f);
        foreach(var zone in zones)
            foreach(int next in zone.neighbors) if(next>zone.index) MapLine(screen(zone.bounds.center),screen(zones[next].bounds.center),new Color(.35f,.52f,.64f));
        if(ContainmentOpen)MapLine(screen(zones[2].bounds.center),screen(zones[5].bounds.center),new Color(1,.45f,.16f));
        foreach(var zone in zones)
        {
            Vector2 at=screen(zone.bounds.center);
            GUI.color=zone.index==CurrentZoneIndex?new Color(1,.75f,.16f):(zone.index==1&&PowerA||zone.index==5&&PowerB)?new Color(.2f,.95f,.6f):new Color(.68f,.85f,.9f);
            GUI.DrawTexture(new Rect(at.x-4,at.y-4,8,8),Texture2D.whiteTexture);
            GUI.Label(new Rect(at.x-45,at.y+5,100,22),zone.label); GUI.color=Color.white;
        }
        Vector2 player=screen(Player.transform.position); GUI.color=new Color(1,.3f,.2f);
        GUI.DrawTexture(new Rect(player.x-3,player.y-3,6,6),Texture2D.whiteTexture);GUI.color=Color.white;
    }
    static void MapLine(Vector2 a,Vector2 b,Color color)
    {
        Matrix4x4 saved=GUI.matrix; GUI.color=color;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg,a);
        GUI.DrawTexture(new Rect(a.x,a.y,Vector2.Distance(a,b),2),Texture2D.whiteTexture);
        GUI.matrix=saved;GUI.color=Color.white;
    }
}
