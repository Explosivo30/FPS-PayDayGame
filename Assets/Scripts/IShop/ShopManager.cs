using System.Collections.Generic;
using UnityEngine;

public class ShopManager : MonoBehaviour
{
    [Header("Catalog")]
    [Tooltip("List of UpgradeItem assets")]
    [SerializeField] private List<UpgradeItem> catalog;

    [Header("UI")]
    [Tooltip("Parent for instantiating buttons")]
    [SerializeField] private Transform itemContainer;
    [Tooltip("Button prefab with ShopButton component")]
    [SerializeField] private GameObject itemButtonPrefab;

    private void Start()
    {
        foreach (var item in catalog)
        {
            var go = Instantiate(itemButtonPrefab, itemContainer);
            var btn = go.GetComponent<ShopButton>();
            btn.Setup(item);
        }
    }
}
