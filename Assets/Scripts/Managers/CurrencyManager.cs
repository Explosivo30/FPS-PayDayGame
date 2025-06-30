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
        if (Coins < amount)
        {
            Debug.LogWarning($"No hay suficientes monedas: hace falta {amount}, tienes {Coins}");
            return false;
        }

        Coins -= amount;
        UpdateUI();
        Debug.Log($"Gastadas {amount} monedas → quedan {Coins}");
        GameManager.Instance.SetPlayerPoints(Coins);
        return true;
    }

    public bool SpendCheck(int amount)
    {
        return GameManager.Instance.GetPlayerPoints() >= amount;
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
