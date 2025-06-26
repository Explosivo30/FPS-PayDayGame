using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopButton : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText, costText;
    [SerializeField] private Image iconImage;
    [SerializeField] private Button buyButton;
    [SerializeField] private UpgradeItem itemData;

    public void Setup(UpgradeItem data)
    {
        itemData = data;
        nameText.text = data.DisplayName;
        costText.text = data.Cost.ToString();
        iconImage.sprite = data.Icon;
        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(OnClick);
        UpdateInteractable();
    }

    public void OnClick()
    {
        itemData.Buy();
        //UpdateInteractable();
    }

    private void UpdateInteractable()
    {
        buyButton.interactable = itemData.CanBuy();
    }
}
