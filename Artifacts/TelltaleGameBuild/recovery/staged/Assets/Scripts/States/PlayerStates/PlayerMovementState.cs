using UnityEngine;

public class PlayerMovementState : PlayerBaseState
{
    public PlayerMovementState(PlayerStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override void Enter()
    {
        stateMachine.controls.JumpEvent += OnJump;
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

        if (stateMachine.controls.isCrouching)
        {
            Vector3 horizVel = new Vector3(stateMachine.PlayerVelocity.x, 0, stateMachine.PlayerVelocity.z);
            if (horizVel.magnitude > stateMachine.maxCrouchSpeed * 1.5f)
            {
                stateMachine.SwitchState(new PlayerSlideState(stateMachine));
            }
            else
            {
                stateMachine.SwitchState(new PlayerCrouchState(stateMachine));
            }
            return;
        }

        // Apply friction when grounded
        stateMachine.ApplyFriction(stateMachine.groundFriction);

        // Get Input
        Vector2 input = stateMachine.controls.MovementValue;

        if (input.x > 0)
        {
            stateMachine.cameraTilt.DoTilt(-1f);
        }
        else if (input.x < 0)
        {
            stateMachine.cameraTilt.DoTilt(1f);

        }
        else
        {
            // Reset camera tilt if no horizontal input
            stateMachine.cameraTilt.DoTilt(0f);
        }

        if (input.sqrMagnitude < 0.01f)
        {
            stateMachine.SwitchState(new PlayerIdleState(stateMachine));
            return;
        }

        Vector3 camForward = stateMachine.GetCameraForward();
        Vector3 camRight = stateMachine.GetCameraRight();

        Vector3 moveDir = (camForward * input.y + camRight * input.x).normalized;

        stateMachine.Accelerate(moveDir, stateMachine.maxGroundSpeed, stateMachine.groundAcceleration);
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
