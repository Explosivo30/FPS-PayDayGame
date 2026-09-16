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
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private int baseEnemyCount=5,incrementPerWave=2,maxAlive=12;
    [SerializeField] private float baseSpeed=3.4f,speedIncrement=.1f,spawnInterval=.65f;
    [SerializeField] private TextMeshProUGUI roundText;
    [SerializeField] private GameObject waveCompletePanel;
    [SerializeField] private float announcementDuration=8f;
    private readonly HashSet<IDamageable> waveEnemies=new HashSet<IDamageable>();
    private Coroutine waveRoutine;
    private int remaining;
    public int CurrentWave { get; private set; }
    public int CompletedWaves { get; private set; }
    public int EnemiesRemaining => remaining;
    public int ActiveEnemies => waveEnemies.Count;
    public bool IsSpawning { get; private set; }
    public float NextWaveIn { get; private set; }
    public bool AutomaticWaves=true;

    private void Awake()
    {
        if(Instance!=null && Instance!=this) { Destroy(gameObject); return; }
        Instance=this; Time.timeScale=1; UpdateScoreUI();
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
        remaining=Mathf.Max(0,remaining-1); AddScore(pointsPerKill);
        SquadManager.Instance?.RebuildSquads();
    }
    private IEnumerator Waves()
    {
        yield return new WaitForSeconds(2);
        if(enemyPrefab==null||spawnPoints==null||spawnPoints.Length==0) { Debug.LogError("Wave configuration is incomplete."); yield break; }
        while(true)
        {
            CurrentWave++; remaining=baseEnemyCount+(CurrentWave-1)*incrementPerWave;
            if(roundText!=null) roundText.text="OLEADA "+CurrentWave;
            IsSpawning=true;
            int pending=remaining;
            while(pending>0)
            {
                if(waveEnemies.Count>=maxAlive || !TrySpawnPosition(out Vector3 position))
                { yield return new WaitForSeconds(.25f); continue; }
                var go=Instantiate(enemyPrefab,position,Quaternion.identity);
                var enemy=go.GetComponent<NormalEnemyStateMachine>();
                if(enemy==null) { Destroy(go); Debug.LogError("Wave prefab requires NormalEnemyStateMachine."); yield break; }
                waveEnemies.Add(enemy);
                enemy.agent.speed=Mathf.Min(5.5f,baseSpeed+(CurrentWave-1)*speedIncrement);
                SquadManager.Instance?.RebuildSquads();
                pending--;
                yield return new WaitForSeconds(spawnInterval);
            }
            IsSpawning=false;
            while(remaining>0) yield return null;
            CompletedWaves++;
            if(waveCompletePanel!=null) waveCompletePanel.SetActive(true);
            NextWaveIn=announcementDuration;
            while(NextWaveIn>0) { NextWaveIn=Mathf.Max(0,NextWaveIn-Time.deltaTime); yield return null; }
            if(waveCompletePanel!=null) waveCompletePanel.SetActive(false);
        }
    }
    private bool TrySpawnPosition(out Vector3 position)
    {
        position=Vector3.zero;
        if(GetPlayerTransforms().Count==0) return false;
        int start=Random.Range(0,spawnPoints.Length);
        Vector3 player=players[0].position;
        for(int i=0;i<spawnPoints.Length;i++)
        {
            var point=spawnPoints[(start+i)%spawnPoints.Length];
            if(point==null||Vector3.Distance(point.position,player)<8) continue;
            if(!NavMesh.SamplePosition(point.position,out var sample,2,NavMesh.AllAreas)) continue;
            var route=new NavMeshPath();
            if(!NavMesh.CalculatePath(sample.position,player,NavMesh.AllAreas,route)||route.status!=NavMeshPathStatus.PathComplete) continue;
            position=sample.position; return true;
        }
        return false;
    }
}
