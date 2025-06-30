using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopButton : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText, costText;
    [SerializeField] private Image iconImage;
    [SerializeField] private Button buyBtn;

    private StatUpgrade data;

   
    public void Setup(StatUpgrade u)
    {
        data = u;
        nameText.text = u.displayName;
        costText.text = UpgradeManager.Instance.GetNextCost(u).ToString();
        buyBtn.onClick.AddListener(() => {
            UpgradeManager.Instance.BuyUpgrade(u);
            Refresh();
        });
        Refresh();
    }

    private void Refresh()
    {
        buyBtn.interactable =
          UpgradeManager.Instance.GetLevel(data) < data.MaxLevel
          && CurrencyManager.Instance.SpendCheck(UpgradeManager.Instance.GetNextCost(data));
        costText.text = UpgradeManager.Instance.GetNextCost(data).ToString();
    }
    
}
