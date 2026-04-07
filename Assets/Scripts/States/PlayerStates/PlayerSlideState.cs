using UnityEngine;

public class PlayerSlideState : PlayerBaseState
{
    private float slideTimer;

    public PlayerSlideState(PlayerStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override void Enter()
    {
        stateMachine.SetCrouchedScale(true);
        slideTimer = stateMachine.slideDuration;

        // Apply jump/slide boost in current direction
        Vector3 vel2D = new Vector3(stateMachine.PlayerVelocity.x, 0, stateMachine.PlayerVelocity.z);
        if (vel2D.magnitude > 0)
        {
            Vector3 dir = vel2D.normalized;
            stateMachine.PlayerVelocity += dir * stateMachine.slideBoost;
        }

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

        slideTimer -= Time.deltaTime;

        bool wantsToStand = !stateMachine.controls.isCrouching;

        if (wantsToStand || slideTimer <= 0)
        {
            if (wantsToStand)
            {
                if (stateMachine.CanStandUp())
                {
                    stateMachine.SetCrouchedScale(false);
                    stateMachine.SwitchState(new PlayerMovementState(stateMachine));
                    return;
                }
                else
                {
                    stateMachine.SwitchState(new PlayerCrouchState(stateMachine));
                    return;
                }
            }
            else
            {
                stateMachine.SwitchState(new PlayerCrouchState(stateMachine));
                return;
            }
        }

        // Reduced friction for sliding
        stateMachine.ApplyFriction(stateMachine.slideFriction);

        // Apply down-slope sliding gravity
        if (stateMachine.Sliding)
        {
            Vector3 slideDirection = Vector3.ProjectOnPlane(Vector3.down, stateMachine.SlideNormal).normalized;
            stateMachine.PlayerVelocity += slideDirection * stateMachine.GravityForce * Time.deltaTime * 2f; 
        }

        // Still allow minor direction changes (strafing) while sliding
        Vector2 input = stateMachine.controls.MovementValue;
        Vector3 camForward = stateMachine.GetCameraForward();
        Vector3 camRight = stateMachine.GetCameraRight();
        Vector3 moveDir = (camForward * input.y + camRight * input.x).normalized;

        stateMachine.Accelerate(moveDir, stateMachine.maxGroundSpeed * 1.5f, stateMachine.groundAcceleration * 0.2f);
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
