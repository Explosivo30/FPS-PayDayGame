using UnityEngine;

public enum RewardGroup { Offense, Utility, Survival }
public enum RewardKind { WeaponStat, Health, Shield, Repair, Supply, KillShield, AutoLoader, Chips, Mutation }

[CreateAssetMenu(menuName="Rewards/Card")]
public class RewardCard : ScriptableObject
{
    public string id,title,weaponId;
    [TextArea] public string description;
    [TextArea] public string activationHint;
    public Sprite icon;
    public RewardGroup group;
    public RewardKind kind;
    public WeaponStat stat;
    public MutationKind mutation;
    public bool special;
    [Min(0)] public int maxLevel=3;
    public float value,secondaryValue;
}
