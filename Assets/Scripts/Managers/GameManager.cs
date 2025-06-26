using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    private List <Transform> players = new List<Transform>();

    [SerializeField] private int playerPoints = 0;

    [Tooltip("Points awarded per enemy kill")]
    [SerializeField] private int pointsPerKill = 10;


    [Header("Score")]
    [Tooltip("UI Text for displaying the score")]
    [SerializeField] private TextMeshProUGUI scoreText;

    /// <summary>
    /// WAVE SETTINGS MANAGER
    /// </summary>
    /// 
    #region WaveSettings

    [Header("Wave Settings")]
    [Tooltip("Prefab of the enemy to spawn")]
    [SerializeField] private GameObject enemyPrefab;
    [Tooltip("Spawn locations for enemies")]
    [SerializeField] private Transform[] spawnPoints;
    [Tooltip("Base enemies per wave")]
    [SerializeField] private int baseEnemyCount = 5;
    [Tooltip("Additional enemies per wave")]
    [SerializeField] private int incrementPerWave = 2;
    [Tooltip("Base move speed multiplier")]
    [SerializeField] private float baseSpeed = 1f;
    [Tooltip("Speed increase per wave")]
    [SerializeField] private float speedIncrement = 0.1f;

    [Header("UI References")]
    [Tooltip("TMP Text for round counter")]
    [SerializeField] private TextMeshProUGUI roundText;
    [Tooltip("Panel or text for wave complete announcement")]
    [SerializeField] private GameObject waveCompletePanel;
    [Tooltip("Time that the announcement stays visible")]
    [SerializeField] private float announcementDuration = 2f;


    private int _currentWave = 0;
    private int _enemiesRemaining;
    private bool _isSpawning;

    #endregion

    private void Awake()
    {
        if (Instance != null)
            return;

        Instance = this;
        UpdateScoreUI();

        //DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // Subscribe to global death event
        EnemyEvents.OnDeath += OnEnemyDeath;
        StartNextWave();
    }

    #region HandleWave

    private void OnDestroy()
    {
        EnemyEvents.OnDeath -= OnEnemyDeath;
    }

    private void OnEnemyDeath(IDamageable dead)
    {
        _enemiesRemaining--;
        AddScore(pointsPerKill);
        if (_enemiesRemaining <= 0 && !_isSpawning)
            StartCoroutine(HandleWaveComplete());
    }

    public void AddScore(int points)
    {
        playerPoints += points;
        UpdateScoreUI();
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
            scoreText.text = playerPoints.ToString();
    }

    private IEnumerator HandleWaveComplete()
    {
        waveCompletePanel?.SetActive(true);
        yield return new WaitForSeconds(announcementDuration);
        waveCompletePanel?.SetActive(false);
        StartNextWave();
    }

    private void StartNextWave()
    {
        _currentWave++;
        UpdateRoundUI();
        SpawnWave();
        // Incorporate new enemies into squads
        SquadManager.Instance.RebuildSquads();
    }

    private void UpdateRoundUI()
    {
        if (roundText != null)
            roundText.text = $"Round {_currentWave}";
    }

    private void SpawnWave()
    {
        _isSpawning = true;

        int enemyCount = baseEnemyCount + (_currentWave - 1) * incrementPerWave;
        _enemiesRemaining = enemyCount;

        float speed = baseSpeed + (_currentWave - 1) * speedIncrement;

        for (int i = 0; i < enemyCount; i++)
        {
            Transform spawn = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
            GameObject go = Instantiate(enemyPrefab, spawn.position, spawn.rotation);

            // Configure NavMesh speed if available
            if (go.TryGetComponent<UnityEngine.AI.NavMeshAgent>(out var nav))
            {
                nav.speed = speed;
            }

            // Register any ISquadMember component on the spawned object
            if (go.TryGetComponent<ISquadMember>(out var squadMember))
            {
                SquadManager.Instance.Register(squadMember);
            }
        }

        _isSpawning = false;
    }

    #endregion

    public List <Transform> GetPlayerTransforms() {  return players;  }

    public void AddPlayerTransforms(Transform player){ if(!players.Contains(player)) players.Add(player); }

}
