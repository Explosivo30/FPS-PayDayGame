using UnityEngine;

public class MagazineUpgradable : MonoBehaviour, IUpgradeable
{
    [SerializeField] private BaseGun gun;       // assign in inspector
    [SerializeField] private int increment = 5;  // bullets per level
    [SerializeField] private int maxLevel = 5;
    private int level;

    public string Id => $"{gun.name}_magazine";
    public int Level => level;
    public int MaxLevel => maxLevel;

    private void Awake() => GameManager.Instance.Register(this);

    public int GetUpgradeCost() => 100 * (level + 1);

    public void ApplyUpgrade()
    {
        if (level >= maxLevel) return;
        level++;
        gun.ammo += increment;
        Debug.Log($"{Id} upgraded to level {level}, new magazine size = {gun.ammo}");
    }
}
