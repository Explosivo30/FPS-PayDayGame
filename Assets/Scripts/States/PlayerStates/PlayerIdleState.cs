using UnityEngine;

public class PlayerIdleState : PlayerBaseState
{
    public PlayerIdleState(PlayerStateMachine stateMachine) : base(stateMachine)
    {
    }

    int maxInteractDistance = 3;

    public override void Enter()
    {
        stateMachine.controls.InteractEvent += InteractEvent;
        
    }    

    public override void Tick()
    {
        //Debug.Log("Estoy en idle");
        stateMachine.PlayerLook();
        stateMachine.GroundDetection();
        if(Input.GetKeyDown(KeyCode.H))
        {
            stateMachine.TakeDamage(20);
        }
        stateMachine.ApplyGravity();
        stateMachine.PlayerHorizontalMovement(stateMachine.CameraOritentedMovement(stateMachine.GetInput()));

        

    }
    public override void Exit()
    {
        stateMachine.controls.InteractEvent -= InteractEvent;
    }

    private void InteractEvent()
    {
        // 1) If the shop is open, close it immediately:
        if (ShopManager.Instance.IsOpen)
        {
            ShopManager.Instance.CloseShop();
            return;
        }


        // ray from center of screen
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out var hit, maxInteractDistance,
                LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.TryGetComponent<IInteractable>(out var target))
            {
                target.Interact();
            }
        }
    }
}
