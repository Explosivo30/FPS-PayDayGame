using UnityEngine;
using TMPro;
public class TowerOverlay : MonoBehaviour
{
    public TMP_Text floorText, objectiveText, messageText, interactionText;
    public CanvasGroup fade;
    Canvas hudCanvas;
    void Awake(){hudCanvas=GetComponentInParent<Canvas>();}
    void Update()
    {
        var session = TowerSession.Instance;
        if(hudCanvas!=null)hudCanvas.enabled=!(session?.ModalVisible??false);
        if (session == null || session.CurrentFloor == null) return;
        var floor = session.CurrentFloor;
        floorText.text = "TORRE HELIX  /  " + floor.floorNumber.ToString("00") + "  /  " + floor.displayName;
        objectiveText.text = session.ExitUnlocked ?
            (GameManager.Instance.WaitingForExtraWave?"F  OLEADA EXTRA · MÁS AMENAZA · "+GameManager.Instance.NextExtraReward+" CHIPS  /  O SUBE EN EL ASCENSOR":"OLEADA EXTRA ACTIVA · ASCENSOR DISPONIBLE PARA RETIRARTE") :
            "DESBLOQUEA EL ASCENSOR  /  " + GameManager.Instance.CompletedWaves + " DE " + floor.mandatoryWaves + " OLEADAS";
        messageText.text = session.VisibleMessage;
        interactionText.text = "";
        if (session.IsTransitioning || WeaponAction.CombatPaused) return;
        var head = session.player.headCam;
        if (!Physics.Raycast(head.position, head.forward, out var hit, 3, ~(1<<7|1<<8|1<<2), QueryTriggerInteraction.Ignore)) return;
        if (hit.collider.GetComponentInParent<ElevatorPanel>() != null)
            interactionText.text = session.ExitUnlocked ? "E   SUBIR AL SIGUIENTE PISO" : "ASCENSOR BLOQUEADO";
        else if (hit.collider.GetComponentInParent<ShopOpener>() != null) interactionText.text = "E   MEJORAR EQUIPO";
        else if (hit.collider.GetComponentInParent<TowerSupply>() is TowerSupply supply)
            interactionText.text = "E   " + (supply.medical ? "RECUPERAR 40 SALUD" : "REPOSICIÓN DE MUNICIÓN") + "  /  " + supply.price + " CHIPS";
    }
}
