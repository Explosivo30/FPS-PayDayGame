using UnityEngine;
/// Cualquier objeto que pueda comprarse en el menú
public interface IShopItem
{
    string DisplayName { get; }
    Sprite Icon { get; }
    int Cost { get; }
    bool CanBuy();                      // ¿el jugador tiene suficientes monedas?
    void Buy();                         // ejecuta la compra (resta monedas y aplica efecto)
}
