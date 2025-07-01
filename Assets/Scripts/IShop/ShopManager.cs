using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class ShopManager : MonoBehaviour
{
    [SerializeField] private Transform buttonContainer;
    //[SerializeField] private GameObject buttonPrefab; // con ShopButton

    [SerializeField] private List<ShopButton> shops = new List<ShopButton>();

    private void Start()
    {
        int i = 0;
        foreach (var u in UpgradeManager.Instance.catalog)
        {
            if (i > shops.Count) break;
            shops[i].Setup(u);
            i++;
        }

            //TO MAKE YOUR BUTTONS But i want the ones im using
        foreach (var u in UpgradeManager.Instance.catalog)
        {
            //var go = Instantiate(buttonPrefab, buttonContainer);
            //var sb = go.GetComponent<ShopButton>();
            //sb.Setup(u);
        }
    }
}
