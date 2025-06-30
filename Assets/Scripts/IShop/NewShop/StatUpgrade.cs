using UnityEngine;

public enum UpgradeTarget { Player, Weapon }
public enum WeaponStat { AmmoCapacity, FireRate, Damage, RecoilKickUp }
public enum PlayerStat { Acceleration, JumpHeight, MaxHealth }
public enum UpgradeMode { Additive, Multiplicative }


[CreateAssetMenu(menuName = "Upgrades/StatUpgrade")]

public class StatUpgrade : ScriptableObject
{
    [Header("UI")]
    public string displayName;
    public Sprite icon;

    [Header("Target")]
    public UpgradeTarget target;
    [Tooltip("If Weapon, filtra por gunType; si Player, ignorar.")]
    public string gunTypeID;
    [Tooltip("Qué stat aplicar.")]
    public WeaponStat weaponStat;
    public PlayerStat playerStat;

    [Header("Levels (array length = max levels)")]
    [Tooltip("Para cada nivel: cost/value.")]
    public LevelDefinition[] levels;

    [System.Serializable]
    public struct LevelDefinition
    {
        public int cost;
        public float value;
    }

    public int MaxLevel => levels?.Length ?? 0;
    public int GetCost(int lvl) => levels != null && lvl < levels.Length ? levels[lvl].cost : int.MaxValue;
    public float GetValue(int lvl) => levels != null && lvl < levels.Length ? levels[lvl].value : 0f;

}
