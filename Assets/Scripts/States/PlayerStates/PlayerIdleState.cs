using UnityEngine;

public class PlayerIdleState : PlayerBaseState
{
    public PlayerIdleState(PlayerStateMachine stateMachine) : base(stateMachine)
    {
    }

    int maxInteractDistance = 3;

    public override void Enter()
    {

        stateMachine.controls.JumpEvent += OnJump;
    }    

    public override void Tick()
    {
        stateMachine.PlayerLook();
        stateMachine.GroundDetection();
        
        if(Input.GetKeyDown(KeyCode.H))
        {
            stateMachine.TakeDamage(20);
        }

        if (!stateMachine.Grounded)
        {
            stateMachine.SwitchState(new PlayerAirState(stateMachine));
            return;
        }

        if (stateMachine.controls.isCrouching)
        {
            stateMachine.SwitchState(new PlayerCrouchState(stateMachine));
            return;
        }

        if (stateMachine.controls.MovementValue.sqrMagnitude > 0.01f)
        {
            stateMachine.SwitchState(new PlayerMovementState(stateMachine));
            return;
        }

        // Apply friction to stop completely
        stateMachine.ApplyFriction(stateMachine.groundFriction);
        stateMachine.ApplyGravityCustom();
        stateMachine.MovePlayer();
    }
    
    public override void Exit()
    {

        stateMachine.controls.JumpEvent -= OnJump;
    }

    private void OnJump()
    {
        if (stateMachine.Grounded)
        {
            stateMachine.Jump();
            stateMachine.SwitchState(new PlayerAirState(stateMachine));
        }
    }

}
