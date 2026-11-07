using UnityEngine;

public class RecoilUpgradable : MonoBehaviour, IUpgradeable
{
    [SerializeField] private BaseGun gun;
    [SerializeField] private float reductionPerLevel = 0.1f;
    [SerializeField] private int maxLevel = 5;
    private int level;

    public string Id => $"{gun.name}_recoil";
    public int Level => level;
    public int MaxLevel => maxLevel;

    private void Awake() => GameManager.Instance.Register(this);

    public int GetUpgradeCost() => 120 * (level + 1);

    public void ApplyUpgrade()
    {
        if (level >= maxLevel) return;
        level++;
        gun.Recoil.recoilKickUp = Mathf.Max(0, gun.Recoil.recoilKickUp - reductionPerLevel);
        gun.Recoil.recoilKickSide = Mathf.Max(0, gun.Recoil.recoilKickSide - reductionPerLevel);
        Debug.Log($"{Id} lvl {level}, new recoil up/side = {gun.Recoil.recoilKickUp}/{gun.Recoil.recoilKickSide}");
    }
}
