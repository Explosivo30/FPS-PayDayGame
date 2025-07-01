using System.Collections.Generic;
using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    [Tooltip("Todos los assets StatUpgrade")]
     public StatUpgrade[] catalog;

    // Mapa asset → nivel actual
    private Dictionary<StatUpgrade, int> currentLevels = new();

    private void Awake()
    {
        if (Instance != null) { Destroy(this); return; }
        Instance = this;

        // inicializa a nivel 0
        foreach (var u in catalog)
            currentLevels[u] = 0;
    }

    /// <summary>Devuelve el nivel actual de esa mejora.</summary>
    public int GetLevel(StatUpgrade u) => currentLevels[u];

    /// <summary>Coste para mejorar al siguiente nivel.</summary>
    public int GetNextCost(StatUpgrade u)
        => GetLevel(u) < u.MaxLevel ? u.GetCost(GetLevel(u)) : -1;

    /// <summary>¿Se puede comprar?</summary>
    public bool CanUpgrade(StatUpgrade u)
        => GetLevel(u) < u.MaxLevel
        && CurrencyManager.Instance.SpendCheck(GetNextCost(u));  // solo check

    /// <summary>Compra y aplica la mejora.</summary>
    public void BuyUpgrade(StatUpgrade u)
    {
        int lvl = GetLevel(u);
        if (lvl >= u.MaxLevel) return;

        int cost = u.GetCost(lvl);
        if (!CurrencyManager.Instance.Spend(cost)) return;

        // sube level
        currentLevels[u] = lvl + 1;
        ApplyUpgrade(u, lvl + 1);
    }

    private void ApplyUpgrade(StatUpgrade u, int newLevel)
    {
        float val = u.GetValue(newLevel - 1);
        if (u.target == UpgradeTarget.Player)
        {
            PlayerStateMachine player = GameManager.Instance.GetPlayerTransforms()[0].GetComponent<PlayerStateMachine>(); // tu referencia
            
            player.ApplyPlayerStat(u.playerStat, val, u.weaponStat == WeaponStat.Damage /*mode*/);
        }
        else // arma
        {
            foreach (BaseGun gun in FindObjectsByType<BaseGun>(FindObjectsSortMode.None))
                if (gun.GunTypeID == u.gunTypeID)
                    gun.ApplyWeaponStat(u.weaponStat, val);
        }
    }
}
