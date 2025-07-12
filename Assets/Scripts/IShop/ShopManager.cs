using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    [SerializeField] private Transform buttonContainer;
    //[SerializeField] private GameObject buttonPrefab; // con ShopButton

    [SerializeField] private List<ShopButton> shops = new List<ShopButton>();

    [Tooltip("The whole shop UI panel GameObject")]
    [SerializeField] private GameObject shopUI;

    private bool _isOpen;

    public bool IsOpen => _isOpen;

    private void Awake()
    {
        if (Instance != null) { Destroy(this); return; }
        Instance = this;
        shopUI.SetActive(false);
    }

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


    /// <summary>
    /// Toggle open/close, pause/unpause.
    /// </summary>
    public void OpenShop()
    {
        if (_isOpen) return;
        _isOpen = true;
        shopUI.SetActive(true);

        // Freeze time:
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseShop()
    {
        if (!_isOpen) return;
        _isOpen = false;
        shopUI.SetActive(false);

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
