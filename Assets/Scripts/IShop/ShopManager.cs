using System.Collections.Generic;
using UnityEngine;

public class ShopManager : MonoBehaviour
{
    [SerializeField] private Transform buttonContainer;
    [SerializeField] private GameObject buttonPrefab; // con ShopButton

    private void Start()
    {
        foreach (var u in UpgradeManager.Instance.catalog)
        {
            var go = Instantiate(buttonPrefab, buttonContainer);
            var sb = go.GetComponent<ShopButton>();
            sb.Setup(u);
        }
    }
}
