using UnityEngine;

[CreateAssetMenu(menuName = "Shop/UpgradeItem")]
public class UpgradeItem : ScriptableObject
{
    [Header("UI")]
    public string DisplayName;
    public Sprite Icon;

    [Header("Upgrade Target")]
    [Tooltip("ID del IUpgradeable registrado en UpgradeRegistry")]
    public string targetID;

    [Header("Cost Settings")]
    [Tooltip("Fallback cost si no se encuentra el IUpgradeable")]
    public int BaseCost = 100;

    /// <summary>
    /// Coste actual de la siguiente mejora
    /// </summary>
    public int Cost
    {
        get
        {
            var up = GameManager.Instance.Get(targetID);
            // If we found it, use its GetUpgradeCost(), otherwise fallback
            return up != null ? up.GetUpgradeCost() : BaseCost;
        }
    }

    /// <summary>
    /// Comprueba si hay monedas suficientes y si no alcanzó el nivel máximo
    /// </summary>
    public bool CanBuy()
    {
        var up = GameManager.Instance.Get(targetID);
        if (up == null) return false;
        if (up.Level >= up.MaxLevel) return false;
        return CurrencyManager.Instance.Coins >= Cost;
    }

    /// <summary>
    /// Realiza la compra: descuenta monedas y aplica la mejora
    /// </summary>
    public void Buy()
    {

        ///DEBUG END
        // 1) Look up the real IUpgradeable instance
        var up = GameManager.Instance.Get(targetID);
        if (up == null)
        {
            Debug.LogError($"UpgradeItem.Buy(): no IUpgradeable registered under ID '{targetID}'");
            return;
        }

        // 2) Spend the coins
        if (!CurrencyManager.Instance.Spend(Cost))
        {
            Debug.LogWarning("No hay suficientes monedas para comprar.");
            return;
        }
            
        Debug.Log("Can spend");
        // 3) Actually upgrade
        up.ApplyUpgrade();

        // 4) Feedback
        //AudioManager.Instance.Play("purchase");
        //UIManager.Instance.ShowFeedback(
        //    $"{DisplayName} mejorado al nivel {up.Level}"
        //);
    }
}

