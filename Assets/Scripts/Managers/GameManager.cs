using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-200)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    private readonly List<Transform> players=new List<Transform>();
    private readonly Dictionary<string,IUpgradeable> upgrades=new Dictionary<string,IUpgradeable>();
    [SerializeField] private int playerPoints;
    [SerializeField] private int pointsPerKill=10;
    [SerializeField] private TextMeshProUGUI scoreText;
    [Header("Waves")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private GameObject meleeEnemyPrefab;
    [SerializeField,Range(0,1)] private float meleeShare=.4f;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private int baseEnemyCount=5,incrementPerWave=2,maxAlive=12;
    [SerializeField] private float baseSpeed=3.4f,speedIncrement=.1f,spawnInterval=.65f;
    [SerializeField] private TextMeshProUGUI roundText;
    [SerializeField] private GameObject waveCompletePanel;
    [SerializeField] private float announcementDuration=8f;
    private readonly HashSet<IDamageable> waveEnemies=new HashSet<IDamageable>();
    private Coroutine waveRoutine;
    private int remaining;
    private TowerFloor floor;
    private EnemyEntrySignal entrySignal;
    private int nextEntry;
    public WaveThreat WavePlan { get; private set; }
    ThreatProfile Profile=>TowerSession.Instance?.Threat?.Profile??ThreatProfile.Basic;
    private float initialDelay=2f;
    public int CurrentWave { get; private set; }
    public int CompletedWaves { get; private set; }
    public int EnemiesRemaining => remaining;
    public int ActiveEnemies => waveEnemies.Count;
    public bool IsSpawning { get; private set; }
    public float NextWaveIn { get; private set; }
    public bool AutomaticWaves=true;
    public bool WaitingForExtraWave { get; private set; }
    public int NextExtraReward=> (Mathf.Min(Profile.maximumCount,Profile.initialCount+(TowerSession.Instance?.Threat?.Current?.Wave??CurrentWave)*Profile.perWave))*(pointsPerKill+Mathf.Max(0,CurrentWave+1-(floor!=null?floor.mandatoryWaves:3))*2)+30+Mathf.Max(0,CurrentWave+1-(floor!=null?floor.mandatoryWaves:3))*10;
    public bool RequestExtraWave()
    {
        if(!WaitingForExtraWave||WeaponAction.CombatPaused||(TowerSession.Instance?.Rewards?.Pending??false))return false;
        WaitingForExtraWave=false;
        TowerSession.Instance?.ShowMessage("OLEADA EXTRA · Amenaza aumentada. Puedes retirarte por el ascensor.",5);
        return true;
    }

    private void Awake()
    {
        if(Instance!=null && Instance!=this) { Destroy(gameObject); return; }
        Instance=this; if(!RunPause.IsPaused)Time.timeScale=1; UpdateScoreUI();
    }
    private void Start()
    {
        EnemyEvents.OnDeath+=OnEnemyDeath;
        if(AutomaticWaves) BeginWaves();
    }
    public void BeginWaves()
    {
        if(waveRoutine==null) waveRoutine=StartCoroutine(Waves());
    }
    public void StopFloor()
    {
        if (waveRoutine != null) StopCoroutine(waveRoutine);
        waveRoutine = null;
        waveEnemies.Clear();SquadManager.Instance?.ClearCombat();
        if(entrySignal!=null)Destroy(entrySignal.gameObject);
        foreach (var enemy in FindObjectsByType<NormalEnemyStateMachine>(FindObjectsSortMode.None))
            Destroy(enemy.gameObject);
        remaining = 0; IsSpawning = false; NextWaveIn = 0; WaitingForExtraWave=false;
    }
    public void ConfigureFloor(TowerFloor settings)
    {
        StopFloor();
        floor = settings; spawnPoints = settings.spawnPoints;
        CurrentWave = 0; CompletedWaves = 0; initialDelay = settings.introductionSeconds;
        BeginWaves();
    }
    private void OnDestroy()
    {
        EnemyEvents.OnDeath-=OnEnemyDeath;
        if(Instance==this) Instance=null;
    }
    public void Register(IUpgradeable up) { if(up!=null&&!string.IsNullOrEmpty(up.Id)) upgrades[up.Id]=up; }
    public IUpgradeable Get(string id) { return upgrades.TryGetValue(id,out var value)?value:null; }
    public List<Transform> GetPlayerTransforms() { players.RemoveAll(t=>t==null); return players; }
    public void AddPlayerTransforms(Transform t) { if(t!=null&&!players.Contains(t)) players.Add(t); }
    public void AddScore(int amount) { playerPoints+=amount; UpdateScoreUI(); }
    public int GetPlayerPoints() => playerPoints;
    public void SetPlayerPoints(int points) { playerPoints=points; UpdateScoreUI(); }
    private void UpdateScoreUI() { if(scoreText!=null) scoreText.text=playerPoints.ToString(); }
    private void OnEnemyDeath(IDamageable dead)
    {
        // Scene props, duplicate notifications and old corpses cannot complete a wave.
        if(!waveEnemies.Remove(dead)) return;
        remaining=Mathf.Max(0,remaining-1); AddScore(pointsPerKill + (floor != null ? Mathf.Max(0,CurrentWave-floor.mandatoryWaves)*2 : 0));
        if(dead is Component target)TowerSession.Instance?.RecordKill(target.transform.position);
        SquadManager.Instance?.RebuildSquads();
    }
    private IEnumerator Waves()
    {
        NextWaveIn=initialDelay;
        while(NextWaveIn>0) { NextWaveIn=Mathf.Max(0,NextWaveIn-Time.deltaTime);yield return null; }
        if(enemyPrefab==null||spawnPoints==null||spawnPoints.Length==0) { Debug.LogError("Wave configuration is incomplete."); yield break; }
        while(true)
        {
            CurrentWave++;
            var threat=TowerSession.Instance?.Threat;
            WavePlan=threat!=null?threat.BeginWave():
                new WaveThreat(Profile,CurrentWave,floor!=null?floor.enemyHealth:100,floor!=null?floor.enemyDamage:12);
            remaining=WavePlan.Count;
            SquadManager.Instance?.Configure(WavePlan.AttackLimit,Profile.meleeAttackLimit);
            TowerSession.Instance?.ShowMessage("AMENAZA "+WavePlan.Wave+" · "+WavePlan.Announcement,4);
            if(roundText!=null) roundText.text="OLEADA "+CurrentWave;
            IsSpawning=true;
            int pending=remaining;
            while(pending>0)
            {
                if(waveEnemies.Count>=Mathf.Min(maxAlive,Profile.maximumAlive) || !TrySpawnPosition(out Vector3 position))
                { yield return new WaitForSeconds(.25f); continue; }
                entrySignal=EnemyEntrySignal.Create(position);
                yield return new WaitForSeconds(Mathf.Max(.2f,Profile.spawnWarningSeconds));
                Destroy(entrySignal.gameObject);entrySignal=null;
                if(InDirectView(position))continue;
                int index=WavePlan.Count-pending;
                bool melee=meleeEnemyPrefab!=null&&WavePlan.IsMelee(index);
                var go=Instantiate(melee?meleeEnemyPrefab:enemyPrefab,position,Quaternion.identity);
                var enemy=go.GetComponent<NormalEnemyStateMachine>();
                if(enemy==null) { Destroy(go); Debug.LogError("Wave prefab requires NormalEnemyStateMachine."); yield break; }
                waveEnemies.Add(enemy);
                bool veteran=WavePlan.IsVeteran(index);
                enemy.ConfigureDifficulty(WavePlan.Health*(veteran?Profile.veteranHealth:1),WavePlan.Damage,WavePlan.Accuracy);
                enemy.ConfigureVeteran(veteran);
                enemy.Perception.SeedSearch(floor!=null&&floor.arrival!=null?floor.arrival.position:Vector3.zero);
                var closeCombat=enemy.GetComponent<MeleeRobotCombat>();
                enemy.agent.speed=closeCombat!=null?Mathf.Min(Profile.maximumMeleeSpeed,WavePlan.Speed*closeCombat.speedMultiplier):WavePlan.Speed;
                SquadManager.Instance?.RebuildSquads();
                pending--;
                yield return new WaitForSeconds(spawnInterval*(WavePlan.Wave>=9?.5f:WavePlan.Wave>=3?.65f:1));
            }
            IsSpawning=false;
            while(remaining>0) yield return null;
            CompletedWaves++;
            if(floor!=null)
            {
                AddScore(30+Mathf.Max(0,CurrentWave-floor.mandatoryWaves)*10);
                TowerSession.Instance?.RecordWaveCompleted();
                if(CompletedWaves>=floor.mandatoryWaves){WaitingForExtraWave=true;NextWaveIn=0;}
                while(TowerSession.Instance?.Rewards?.Pending??false)yield return null;
                if(CompletedWaves>=floor.mandatoryWaves)
                    while(WaitingForExtraWave)yield return null;
            }
            if(waveCompletePanel!=null) waveCompletePanel.SetActive(true);
            NextWaveIn=announcementDuration;
            while(NextWaveIn>0) { NextWaveIn=Mathf.Max(0,NextWaveIn-Time.deltaTime); yield return null; }
            if(waveCompletePanel!=null) waveCompletePanel.SetActive(false);
        }
    }
    private bool InDirectView(Vector3 position)
    {
        var camera=Camera.main;if(camera==null)return false;
        Vector3 target=position+Vector3.up;
        Vector3 screen=camera.WorldToViewportPoint(target);
        if(screen.z<=0||screen.x<-.05f||screen.x>1.05f||screen.y<-.05f||screen.y>1.05f)return false;
        if(Physics.Linecast(camera.transform.position,target,out var hit,~(1<<6|1<<8),QueryTriggerInteraction.Ignore))
            return hit.collider.GetComponentInParent<PlayerStateMachine>()!=null;
        return true;
    }
    private bool TrySpawnPosition(out Vector3 position)
    {
        position=Vector3.zero;
        if(GetPlayerTransforms().Count==0) return false;
        int start=nextEntry;
        Vector3 player=players[0].position;
        for(int i=0;i<spawnPoints.Length;i++)
        {
            var point=spawnPoints[(start+i)%spawnPoints.Length];
            if(point==null||Vector3.Distance(point.position,player)<8) continue;
            if(!NavMesh.SamplePosition(point.position,out var sample,2,NavMesh.AllAreas)) continue;
            var route=new NavMeshPath();
            if(!NavMesh.CalculatePath(sample.position,player,NavMesh.AllAreas,route)||route.status!=NavMeshPathStatus.PathComplete) continue;
            if(InDirectView(sample.position))continue;
            nextEntry=(start+i+(WavePlan!=null&&WavePlan.Wave>=3?1:0))%spawnPoints.Length;
            position=sample.position; return true;
        }
        return false;
    }
}
