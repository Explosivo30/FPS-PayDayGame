using UnityEngine;

public class PlayerIdleState : PlayerBaseState
{
    public PlayerIdleState(PlayerStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override void Enter()
    {

        
    }
    public override void Tick()
    {
        Debug.Log("Estoy en idle");
        stateMachine.PlayerLook();
        stateMachine.GroundDetection();
        
        stateMachine.ApplyGravity();
        stateMachine.PlayerHorizontalMovement(stateMachine.CameraOritentedMovement(stateMachine.GetInput()));

    }
    public override void Exit()
    {
        
    }


}
