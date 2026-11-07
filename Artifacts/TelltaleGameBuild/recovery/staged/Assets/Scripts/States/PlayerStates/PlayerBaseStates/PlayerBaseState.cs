using UnityEngine;

public abstract class PlayerBaseState : State
{
    public PlayerStateMachine stateMachine;

    public PlayerBaseState(PlayerStateMachine stateMachine)
    {
        this.stateMachine = stateMachine;
    }
}
