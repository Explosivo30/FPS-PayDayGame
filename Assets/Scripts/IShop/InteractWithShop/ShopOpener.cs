using UnityEngine;

public class ShopOpener : MonoBehaviour, IInteractable
{

    public void Interact()
    {
        ShopManager.Instance.OpenShop();
    }
}
