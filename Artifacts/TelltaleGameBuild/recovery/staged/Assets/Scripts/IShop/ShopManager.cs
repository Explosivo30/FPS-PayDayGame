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
    private System.IDisposable pause;
    private void OnDestroy(){pause?.Dispose();if(Instance==this)Instance=null;}

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
            if (i >= shops.Count) break;
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
        if (_isOpen || (TowerSession.Instance!=null&&(TowerSession.Instance.IsTransitioning||(TowerSession.Instance.Rewards?.Pending??false)))) return;
        _isOpen = true;
        if(TowerSession.Instance!=null)TowerSession.Instance.Menu.ShowShop();
        else shopUI.SetActive(true);

        // Freeze time:
        pause=RunPause.Acquire(true);
    }

    public void CloseShop()
    {
        if (!_isOpen) return;
        _isOpen = false;
        shopUI.SetActive(false);
        TowerSession.Instance?.Menu.Hide();

        pause?.Dispose();pause=null;
        TowerSession.Instance?.weapons.RequireTriggerRelease();
    }

    public void RefreshAllButtons()
    {
        for(int i = 0; i < shops.Count; i++)
        {
            shops[i].IsInteractable();
        }
    }
}
