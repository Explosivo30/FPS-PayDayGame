public class IdleNormalEnemyState : NormalEnemyBaseState
{
    public IdleNormalEnemyState(NormalEnemyStateMachine stateMachine):base(stateMachine) {}
    public override void Enter() {}
    public override void Tick() { stateMachine.TickCombat(); }
    public override void Exit() {}
}
