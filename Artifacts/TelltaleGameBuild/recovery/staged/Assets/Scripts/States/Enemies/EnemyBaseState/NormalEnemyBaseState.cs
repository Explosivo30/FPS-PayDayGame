using UnityEngine;

public abstract class NormalEnemyBaseState : State
{
    public NormalEnemyStateMachine stateMachine;
    public NormalEnemyBaseState(NormalEnemyStateMachine stateMachine)
    {
        this.stateMachine = stateMachine;
    }
}
