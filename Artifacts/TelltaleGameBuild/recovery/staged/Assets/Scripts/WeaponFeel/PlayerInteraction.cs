using UnityEngine;

[RequireComponent(typeof(InputReader))]
public class PlayerInteraction : MonoBehaviour
{
    private InputReader input;
    private PlayerStateMachine player;
    private void Awake() { input=GetComponent<InputReader>();player=GetComponent<PlayerStateMachine>(); }
    private void OnEnable() { input.InteractEvent+=Interact; }
    private void OnDisable() { input.InteractEvent-=Interact; }
    private void Interact()
    {
        if(ShopManager.Instance!=null&&ShopManager.Instance.IsOpen) { ShopManager.Instance.CloseShop();return; }
        if(WeaponAction.CombatPaused||player==null) return;
        var ray=new Ray(player.headCam.position,player.headCam.forward);
        if(Physics.Raycast(ray,out var hit,3,~(1<<7|1<<8|1<<2),QueryTriggerInteraction.Ignore))
            hit.collider.GetComponentInParent<IInteractable>()?.Interact();
    }
}
