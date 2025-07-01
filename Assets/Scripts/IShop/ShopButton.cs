using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopButton : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText, costText;
    [SerializeField] private Image iconImage;
    [SerializeField] private Button buyBtn;

    private StatUpgrade data;
    private int totalPercentage = 0;

    private void Awake()
    {
        
    }
    public void Setup(StatUpgrade u)
    {
        data = u;
        totalPercentage = UpgradeManager.Instance.GetLevel(u);
        nameText.text = u.displayName + " " + u.GetValue( totalPercentage) + "%";
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


        totalPercentage += (int)data.GetValue(UpgradeManager.Instance.GetLevel(data));
        nameText.text = data.displayName + " " +  totalPercentage + "%";

        if (UpgradeManager.Instance.GetNextCost(data) == -1) 
            costText.text = "LOCKED";
        else
            costText.text =  UpgradeManager.Instance.GetNextCost(data).ToString();
    }
    
}
