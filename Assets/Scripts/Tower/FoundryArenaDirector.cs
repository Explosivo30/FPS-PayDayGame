using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;

/// <summary>A short assault: readable entries, area defenders, a weapon to recover and a real exit.</summary>
public sealed class FoundryArenaDirector : MonoBehaviour
{
    public Transform arrival, encounterGate, corePickup, exitPoint, exitDoorLeft, exitDoorRight;
    public NormalEnemyStateMachine rangedPrefab, meleePrefab;
    public Transform[] sentrySpawns, meleeSpawns, reinforcementSpawns, galleryPositions, auditLoopPoints;
    public Transform sniperSpawn, bruteSpawn;
    public FoundryCombatSector[] sentrySectors;
    public FoundryCombatSector sniperSector;
    public FoundryStructure breach;
    // Serialized legacy references remain harmless while the previous scene is being replaced.
    public FoundryCrane crane;
    public ArenaMechanicalPetal petal;
    public bool Ready { get; private set; }
    public PlayerStateMachine Player { get; private set; }
    public IReadOnlyList<NormalEnemyStateMachine> Enemies => enemies;
    public FoundryStructure Breach => breach;
    public FoundryCrane Crane => crane;
    public ArenaMechanicalPetal Petal => petal;
    public int Wave { get; private set; }
    public int AliveCount => enemies.Count(e => e != null && !e.IsDead);
    public int PendingSpawns => requests.Count + (warningActive ? 1 : 0);
    public int TotalSpawned { get; private set; }
    public bool CoreCollected { get; private set; }
    public bool AssaultCleared { get; private set; }
    public bool Completed { get; private set; }
    public bool Exited => Completed;
    public bool ReinforcementsRequested { get; private set; }
    public bool ExitOpen => doorProgress >= 1;
    public string Message { get; private set; }
    public int SpawnWaits { get; private set; }
    readonly List<NormalEnemyStateMachine> enemies = new List<NormalEnemyStateMachine>();
    readonly Queue<EntryRequest> requests = new Queue<EntryRequest>();
    sealed class EntryRequest { public bool melee, veteran; public Transform point; public FoundryCombatSector sector; }
    bool qa, restarting, localDeath, warningActive;
    float doorProgress, messageUntil;
    Vector3 leftClosed, rightClosed;
    EnemyEntrySignal entryWarning;
    AudioSource audioSource;
    AudioClip pickupSound;
    GunController weapons;

    IEnumerator Start()
    {
        qa = Environment.GetCommandLineArgs().Contains("-foundry-qa");
        if (TowerSession.Instance == null) yield return SceneManager.LoadSceneAsync("TowerCore", LoadSceneMode.Additive);
        while (TowerSession.Instance == null || !TowerSession.Instance.Ready) yield return null;
        yield return null;
        SceneManager.SetActiveScene(gameObject.scene);
        GameManager.Instance.AutomaticWaves = false; GameManager.Instance.StopFloor();
        TowerSession.Instance.firstScene = "FundicionReactiva";
        TowerSession.Instance.DeathPresentationOverride = () => { localDeath = true; ClearSignals(); };
        Player = TowerSession.Instance.player; weapons = TowerSession.Instance.weapons;
        Player.TeleportTo(arrival.position, arrival.rotation);
        weapons.WeaponAvailable = weapon => weapon is Pistol || weapon is Knife || (CoreCollected && weapon is MachineGun);
        foreach (var gun in Player.GetComponentsInChildren<BaseGun>(true)) { gun.limitedReserve = false; gun.currentAmmo = gun.ammo; }
        int pistol = weapons.Weapons.ToList().FindIndex(w => w is Pistol); weapons.EquipImmediate(pistol);
        if (TowerSession.Instance.overlay) TowerSession.Instance.overlay.gameObject.SetActive(false);
        var hud = FindFirstObjectByType<LabHUD>();
        if (hud != null)
        {
            if (hud.waveText) hud.waveText.gameObject.SetActive(false);
            if (hud.scoreText) hud.scoreText.gameObject.SetActive(false);
            foreach (var item in hud.GetComponentsInChildren<Transform>(true))
                if (item.name == "Mission glass" || item.name == "Wallet glass") item.gameObject.SetActive(false);
        }
        leftClosed = exitDoorLeft ? exitDoorLeft.position : Vector3.zero;
        rightClosed = exitDoorRight ? exitDoorRight.position : Vector3.zero;
        audioSource = gameObject.AddComponent<AudioSource>();
        pickupSound = ImpactFeedbackPlayer.MakeSound("Carabina recuperada", 1);
        Ready = true; StartCoroutine(Entries());
        CombatFeedback.PlayerShot += OnShot;
        if (!qa) BeginEncounter();
        Tell("Recupera la carabina en la plataforma. Dos accesos cruzan la cámara.", 6);
    }
    void OnShot(Vector3 position) { if (Ready && Wave == 0 && !qa) BeginEncounter(); }
    public void BeginEncounter()
    {
        if (!Ready || Wave != 0 || WeaponAction.CombatPaused) return;
        Wave = 1; SquadManager.Instance?.Configure(3, 2);
        for (int i = 0; i < sentrySpawns.Length; i++)
            Defender(false, sentrySpawns[i], sentrySectors != null && i < sentrySectors.Length ? sentrySectors[i] : null);
        foreach (var point in meleeSpawns) Defender(true, point, null);
    }
    void Defender(bool melee, Transform point, FoundryCombatSector sector)
    {
        var request = new EntryRequest { melee = melee, point = point, sector = sector };
        if (FindEntry(request, out var position)) SpawnEnemy(request, position);
        else requests.Enqueue(request);
    }
    public void SpawnWave(int number) { if (number <= 1) BeginEncounter(); else RequestReinforcements(); }
    public void RequestReinforcements()
    {
        if (ReinforcementsRequested || Wave == 0) return;
        ReinforcementsRequested = true; Wave = 2;
        Enqueue(false, true, sniperSpawn, sniperSector);
        Enqueue(false, false, reinforcementSpawns[0], sentrySectors.Length > 1 ? sentrySectors[1] : null);
        Enqueue(true, true, bruteSpawn, null);
        Tell("Refuerzos por las compuertas norte y este. Aguja cubre el puente.", 5);
    }
    void Enqueue(bool melee, bool veteran, Transform point, FoundryCombatSector sector)
    { requests.Enqueue(new EntryRequest { melee = melee, veteran = veteran, point = point, sector = sector }); }
    IEnumerator Entries()
    {
        while (true)
        {
            if (requests.Count == 0 || AliveCount >= 6 || WeaponAction.CombatPaused || localDeath) { yield return null; continue; }
            var request = requests.Peek();
            if (!FindEntry(request, out var position))
            {
                SpawnWaits++; requests.Dequeue(); requests.Enqueue(request);
                yield return new WaitForSeconds(.5f); continue;
            }
            requests.Dequeue(); warningActive = true;
            entryWarning = EnemyEntrySignal.Create(position);
            yield return new WaitForSeconds(.85f);
            if (localDeath) { ClearSignals(); yield return null; continue; }
            if (!EntryFree(position)) { requests.Enqueue(request); ClearSignals(); continue; }
            SpawnEnemy(request, position);
            ClearSignals(); yield return new WaitForSeconds(.3f);
        }
    }
    void SpawnEnemy(EntryRequest request, Vector3 position)
    {
        var enemy = Instantiate(request.melee ? meleePrefab : rangedPrefab, position, request.point.rotation);
        SceneManager.MoveGameObjectToScene(enemy.gameObject, gameObject.scene);
        if (request.sector != null) enemy.gameObject.AddComponent<FoundrySectorMember>().sector = request.sector;
        enemy.ConfigureDifficulty(request.veteran ? 105 : 75, request.melee ? 10 : 7, .8f);
        enemy.ConfigureVeteran(request.veteran); enemy.detectionRange = 32;
        if (!request.melee) enemy.Ranged?.SetRange(28);
        enemy.Navigation.GroundPlaneMeleeApproach = request.melee;
        if (request.melee) enemy.GetComponent<MeleeRobotCombat>().StopChargeAtPlayer = true;
        enemy.Perception.SeedSearch(Player.transform.position);
        if (enemy.agent) enemy.agent.speed = request.melee ? 4.5f : 3.5f;
        enemies.Add(enemy); TotalSpawned++;
        foreach (var structure in FindObjectsByType<FoundryStructure>(FindObjectsSortMode.None))
            structure.IgnoreActorCollisions(enemy.GetComponentsInChildren<Collider>());
    }
    bool FindEntry(EntryRequest request, out Vector3 position)
    {
        position = default;
        foreach (var offset in new[] { Vector3.zero, Vector3.right * 1.2f, Vector3.left * 1.2f, Vector3.forward * 1.2f, Vector3.back * 1.2f })
        {
            if (!NavMesh.SamplePosition(request.point.position + offset, out var sample, .8f, NavMesh.AllAreas)) continue;
            if (request.sector != null && !request.sector.Contains(sample.position)) continue;
            if (!EntryFree(sample.position)) continue;
            position = sample.position; return true;
        }
        return false;
    }
    bool EntryFree(Vector3 position) => Vector3.Distance(position, Player.transform.position) >= 4 &&
        !Physics.CheckCapsule(position + Vector3.up * .4f, position + Vector3.up * 1.5f, .34f,
            (1 << 0) | (1 << 3) | (1 << 6) | (1 << 7) | (1 << 10), QueryTriggerInteraction.Ignore);
    void Update()
    {
        if (Ready && Input.GetKeyDown(KeyCode.F5)) { RestartEncounter(); return; }
        if (!Ready || WeaponAction.CombatPaused || Completed) return;
        if (Wave == 1 && ((encounterGate != null && Vector3.Distance(Player.transform.position, encounterGate.position) < 2.8f) ||
            (TotalSpawned >= 4 && enemies.Count(e => e == null || e.IsDead) >= 2))) RequestReinforcements();
        AssaultCleared = ReinforcementsRequested && PendingSpawns == 0 && TotalSpawned >= 7 && AliveCount == 0;
        if (AssaultCleared && CoreCollected)
        {
            doorProgress = Mathf.Min(1, doorProgress + Time.deltaTime);
            if (exitDoorLeft) exitDoorLeft.position = leftClosed + Vector3.left * (3 * Mathf.SmoothStep(0, 1, doorProgress));
            if (exitDoorRight) exitDoorRight.position = rightClosed + Vector3.right * (3 * Mathf.SmoothStep(0, 1, doorProgress));
            if (doorProgress >= 1)
            {
                foreach (var door in new[] { exitDoorLeft, exitDoorRight }) if (door != null)
                { foreach (var collider in door.GetComponentsInChildren<Collider>()) collider.enabled = false; var obstacle = door.GetComponent<NavMeshObstacle>(); if (obstacle) obstacle.enabled = false; }
            }
        }
    }
    public bool CollectCore()
    {
        if (!Ready || CoreCollected || WeaponAction.CombatPaused || !corePickup || Vector3.Distance(Player.headCam.position, corePickup.position) > 3.25f) return false;
        CoreCollected = true; corePickup.gameObject.SetActive(false);
        Player.Heal(25); audioSource.PlayOneShot(pickupSound, .65f);
        int carbine = weapons.Weapons.ToList().FindIndex(w => w is MachineGun); if (carbine >= 0) weapons.EquipImmediate(carbine);
        BeginEncounter(); RequestReinforcements();
        Tell("CARABINA RECUPERADA · Asegura la cámara y alcanza la salida norte.", 6);
        return true;
    }
    public bool TryExit()
    {
        if (!Ready || !ExitOpen || !AssaultCleared || !CoreCollected || WeaponAction.CombatPaused || !exitPoint ||
            Vector3.Distance(Player.transform.position, exitPoint.position) > 3) return false;
        Completed = true; Tell("TRASVASE ASEGURADO · F5 para volver a jugar.", 60); return true;
    }
    public void RestartEncounter()
    { if (restarting) return; restarting = true; RunPause.Reset(); EnemyProjectile.ClearAll(); SceneManager.LoadScene("FundicionReactiva"); }
    void Tell(string text, float seconds) { Message = text; messageUntil = Time.time + seconds; }
    void ClearSignals() { warningActive = false; if (entryWarning) Destroy(entryWarning.gameObject); entryWarning = null; }
    void OnDestroy()
    { CombatFeedback.PlayerShot -= OnShot; ClearSignals(); if (pickupSound) Destroy(pickupSound); }
    void OnGUI()
    {
        if (!Ready)
        {
            GUI.color = new Color(.018f, .04f, .05f); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white; GUI.Label(new Rect(30, 30, 400, 30), "ENTRANDO EN CÁMARA DE TRASVASE…"); return;
        }
        GUI.color = new Color(.018f, .04f, .05f, .88f); GUI.DrawTexture(new Rect(18, 16, 710, 85), Texture2D.whiteTexture);
        GUI.color = Color.white;
        var title = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold };
        GUI.Label(new Rect(32, 22, 680, 28), "HELIX / CÁMARA DE TRASVASE", title);
        string objective = localDeath ? "OPERADOR CAÍDO · F5 para reintentar" : Completed ? "TRASVASE ASEGURADO" :
            !CoreCollected ? "RECUPERA LA CARABINA / plataforma iluminada" : !AssaultCleared ? "ASEGURA LA CÁMARA / " + AliveCount + " robots + " + PendingSpawns + " accesos" : "ALCANZA LA SALIDA NORTE / E";
        GUI.Label(new Rect(32, 54, 680, 24), objective);
        GUI.Label(new Rect(32, 77, 680, 24), "R recarga · Q arma · E recoger/salir · F5 reiniciar");
        if (Message != null && Time.time < messageUntil) GUI.Label(new Rect(32, 108, Mathf.Min(960, Screen.width - 64), 30), Message);
        string hint = "";
        if (!WeaponAction.CombatPaused && Physics.Raycast(Player.headCam.position, Player.headCam.forward, out var hit, 3, ~(1 << 7 | 1 << 8 | 1 << 2)))
            if (hit.collider.GetComponentInParent<FoundryObjective>() is FoundryObjective target)
                hint = target.isExit ? (ExitOpen ? "E  SALIR" : "ASEGURA LA CÁMARA Y RECUPERA EL EQUIPO") : "E  RECUPERAR CARABINA";
        GUI.Label(new Rect(Screen.width / 2 - 220, Screen.height / 2 + 40, 440, 28), hint);
        if (localDeath && GUI.Button(new Rect(Screen.width / 2 - 100, Screen.height / 2, 200, 45), "REINTENTAR / F5")) RestartEncounter();
    }
}
