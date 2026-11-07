using UnityEngine;

public class DamageUpgradable : MonoBehaviour, IUpgradeable
{
    [SerializeField] private BaseGun gun;
    [SerializeField] private float incrementPerLevel = 1f;
    [SerializeField] private int maxLevel = 5;
    private int level;

    public string Id => $"{gun.name}_damage";
    public int Level => level;
    public int MaxLevel => maxLevel;

    private void Awake() => GameManager.Instance.Register(this);

    public int GetUpgradeCost() => 200 * (level + 1);

    public void ApplyUpgrade()
    {
        if (level >= maxLevel) return;
        level++;
        gun.damage += incrementPerLevel;
        Debug.Log($"{Id} lvl {level}, new damage = {gun.damage}");
    }
}
