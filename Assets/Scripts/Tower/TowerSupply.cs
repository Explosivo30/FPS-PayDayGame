using UnityEngine;
public class TowerSupply : MonoBehaviour, IInteractable
{
    public bool medical;
    public int price = 40;
    public void Interact()
    {
        var session = TowerSession.Instance;
        if (session == null || session.IsTransitioning || WeaponAction.CombatPaused) return;
        bool needed = medical ? session.player.Health < session.player.MaxHealth : false;
        if (!medical) foreach (var gun in session.player.GetComponentsInChildren<BaseGun>(true))
            needed |= gun.reserveAmmo < gun.maxReserveAmmo;
        if (!needed) { session.ShowMessage(medical ? "Salud completa." : "Reservas completas."); return; }
        if (!CurrencyManager.Instance.SpendCheck(price)) { session.ShowMessage("Necesitas " + price + " chips."); return; }
        CurrencyManager.Instance.Spend(price);
        if (medical) session.player.Heal(40);
        else foreach (var gun in session.player.GetComponentsInChildren<BaseGun>(true))
            gun.AddReserve(gun.ammo * 2);
        session.ShowMessage(medical ? "+40 SALUD" : "MUNICIÓN REPUESTA · +2 CARGADORES POR ARMA");
    }
}
