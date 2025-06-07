using UnityEngine;
using UnityEngine.AI;

public class NormalEnemyStateMachine : StateMachine
{
    public NavMeshAgent agent;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        
        SwitchState(new IdleNormalEnemyState(this));
    }
}
