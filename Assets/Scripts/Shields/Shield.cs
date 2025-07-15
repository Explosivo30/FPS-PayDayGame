using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerStateMachine))]

public class Shield : MonoBehaviour, IShield, IUpgradeable
{
    // --- REGISTRATION & UPGRADE TRACKING ---
    public string Id => "player_shield";
    private int _level = 0;
    [SerializeField] private int maxUpgradeLevel = 10;
    public int Level => _level;
    public int MaxLevel => maxUpgradeLevel;

    // --- SHIELD STATE ---
    [SerializeField] private float _maxShield;
    private float _currentShield;
    private Coroutine _regenCoroutine;
    private PlayerStateMachine _player;

    [Header("Regen Settings")]
    [Tooltip("Seconds after last hit before regen starts")]
    [SerializeField] private float regenDelay = 3f;
    [Tooltip("Shield per second")]
    [SerializeField] private float regenRate = 5f;

    public float Current => _currentShield;
    public float Max => _maxShield;

    public void Absorb(float amount)
    {
        // stop any running regen while taking damage
        if (_regenCoroutine != null) StopCoroutine(_regenCoroutine);

        float leftover = amount - _currentShield;
        _currentShield = Mathf.Max(0f, _currentShield - amount);

        // start regen delay
        _regenCoroutine = StartCoroutine(RegenerateRoutine());

        // if overflow, pass to health
        if (leftover > 0f)
            _player.TakeDamage(leftover);
    }

    private IEnumerator RegenerateRoutine()
    {
        // wait delay
        yield return new WaitForSeconds(regenDelay);

        // then regen until full
        while (_currentShield < Max)
        {
            _currentShield = Mathf.Min(Max, _currentShield + regenRate * Time.deltaTime);
            yield return null;
        }
        _regenCoroutine = null;
    }

    public void Regenerate()
    {
        if (_regenCoroutine != null) StopCoroutine(_regenCoroutine);
        _regenCoroutine = StartCoroutine(RegenerateRoutine());
    }



    #region IUpgradeable



    public void GetNewUpgrade(float value, bool isPercent)
    {
        if (_level >= maxUpgradeLevel)
            return;
        _level++;

        float newMax = isPercent
            ? _maxShield * (1 + value / 100f)
            : _maxShield + value;

        SetMaxShield(newMax);
        Debug.Log($"[Shield] Upgraded via IUpgradeable: level {_level}, newMax {newMax}");
    }


    #endregion

    private void Awake()
    {
        _player = GetComponent<PlayerStateMachine>();
        _currentShield = Max;
        GameManager.Instance.Register(this); // so your shop can find it

    }


    /// <summary>
    /// Called by UpgradeManager when the StatUpgrade for Shield happens.
    /// </summary>
    public void SetMaxShield(float newMax)
    {
        _maxShield = newMax;
        _currentShield = newMax;  // refill immediately
        Debug.Log($"[Shield] New MaxShield = {_maxShield}");
    }

    private void Update()
    {
        Debug.Log(_currentShield);
    }

    public int GetUpgradeCost()
    {
        throw new System.NotImplementedException();
    }

    public void ApplyUpgrade()
    {
        throw new System.NotImplementedException();
    }
}
