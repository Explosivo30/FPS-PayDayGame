using UnityEngine;

public class PlayerCrouchState : PlayerBaseState
{
    public PlayerCrouchState(PlayerStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override void Enter()
    {
        stateMachine.SetCrouchedScale(true);
    }

    public override void Tick()
    {
        stateMachine.PlayerLook();
        stateMachine.GroundDetection();

        if (!stateMachine.Grounded)
        {
            stateMachine.SwitchState(new PlayerAirState(stateMachine));
            return;
        }

        // If stop crouching
        if (!stateMachine.controls.isCrouching)
        {
            if (stateMachine.CanStandUp())
            {
                stateMachine.SetCrouchedScale(false);
                stateMachine.SwitchState(new PlayerMovementState(stateMachine));
                return;
            }
            // else stay crouched until out of roof
        }

        stateMachine.ApplyFriction(stateMachine.groundFriction);

        Vector2 input = stateMachine.controls.MovementValue;
        Vector3 camForward = stateMachine.GetCameraForward();
        Vector3 camRight = stateMachine.GetCameraRight();

        Vector3 moveDir = (camForward * input.y + camRight * input.x).normalized;

        stateMachine.Accelerate(moveDir, stateMachine.maxCrouchSpeed, stateMachine.groundAcceleration);
        stateMachine.ApplyGravityCustom();
        stateMachine.MovePlayer();
    }

    public override void Exit()
    {
    }
}
