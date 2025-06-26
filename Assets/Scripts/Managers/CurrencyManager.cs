using TMPro;
using UnityEngine;

public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance { get; private set; }

    
    public int Coins { get; private set; }

    [SerializeField] private TextMeshProUGUI coinsText;

    private void Awake()
    {
       
        if (Instance != null) Destroy(gameObject);
        else { Instance = this; DontDestroyOnLoad(this); }

        Coins = GameManager.Instance.GetPlayerPoints();
    }

    public bool Spend(int amount)
    {
        Coins = GameManager.Instance.GetPlayerPoints();
        if (Coins < amount) return false;
        Coins -= amount;
        GameManager.Instance.SetPlayerPoints(Coins);
        Debug.Log($"[GameManager] Spend {amount} coins → quedan {GameManager.Instance.GetPlayerPoints()}");
        UpdateUI();
        return true;
    }

    public void Earn(int amount)
    {
        Coins = GameManager.Instance.GetPlayerPoints();
        Coins += amount;
        GameManager.Instance.SetPlayerPoints(Coins);
        
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (coinsText != null)
            coinsText.text = Coins.ToString();
    }
}
