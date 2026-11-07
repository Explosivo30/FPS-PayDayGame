using UnityEngine;

public class PlayerAirState : PlayerBaseState
{
    public PlayerAirState(PlayerStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override void Enter()
    {
    }

    public override void Tick()
    {
        stateMachine.PlayerLook();
        stateMachine.GroundDetection();

        if (stateMachine.Grounded && stateMachine.PlayerVelocity.y <= 0)
        {
            if (stateMachine.controls.isCrouching)
            {
                Vector3 horizVel = new Vector3(stateMachine.PlayerVelocity.x, 0, stateMachine.PlayerVelocity.z);
                if (horizVel.magnitude > stateMachine.maxCrouchSpeed * 1.5f)
                    stateMachine.SwitchState(new PlayerSlideState(stateMachine));
                else
                    stateMachine.SwitchState(new PlayerCrouchState(stateMachine));
            }
            else
            {
                stateMachine.SwitchState(new PlayerMovementState(stateMachine));
            }
            return;
        }

        // Slight air friction
        stateMachine.ApplyFriction(stateMachine.airFriction);

        Vector2 input = stateMachine.controls.MovementValue;
        Vector3 camForward = stateMachine.GetCameraForward();
        Vector3 camRight = stateMachine.GetCameraRight();

        Vector3 moveDir = (camForward * input.y + camRight * input.x).normalized;

        stateMachine.Accelerate(moveDir, stateMachine.maxAirSpeed, stateMachine.airAcceleration);
        stateMachine.ApplyGravityCustom();
        stateMachine.MovePlayer();
    }

    public override void Exit()
    {
    }
}
