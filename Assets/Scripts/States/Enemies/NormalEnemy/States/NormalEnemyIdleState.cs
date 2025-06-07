using UnityEngine;

public class IdleNormalEnemyState : NormalEnemyBaseState
{
    public IdleNormalEnemyState(NormalEnemyStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override void Enter()
    {
        
    }
    
    public override void Tick()
    {
        Transform player;
        if (GameManager.Instance.GetPlayerTransforms().Count >0)
        {
            player = GameManager.Instance.GetPlayerTransforms()[0];
            stateMachine.agent.SetDestination(player.position);
        }
        
        
    }

    public override void Exit()
    {
        
    }


}
