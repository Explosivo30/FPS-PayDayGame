using UnityEngine;

public class ShopOpener : MonoBehaviour, IInteractable
{

    public void Interact()
    {
        CurrencyManager.Instance.AddCurrentPoints();
        ShopManager.Instance.OpenShop();
    }
}
