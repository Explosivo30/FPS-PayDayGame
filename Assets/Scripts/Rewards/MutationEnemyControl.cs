using UnityEngine;
using UnityEngine.AI;

/// <summary>Bounded, shared crowd control. Never warps through cover or across a NavMesh edge.</summary>
public sealed class MutationEnemyControl : MonoBehaviour
{
    MutationRun run;NormalEnemyStateMachine enemy;NavMeshAgent agent;
    float budgetUntil,moved,slowUntil,baseSpeed,ignoreDecoy;
    bool slowing;
    public bool DecoyIgnored=>run!=null&&run.Clock<ignoreDecoy;
    public static MutationEnemyControl For(NormalEnemyStateMachine target,MutationRun owner)
    {
        var state=target.GetComponent<MutationEnemyControl>();if(state==null)state=target.gameObject.AddComponent<MutationEnemyControl>();
        state.run=owner;state.enemy=target;state.agent=target.agent;return state;
    }
    public void IgnoreDecoy(){if(run!=null)ignoreDecoy=run.Clock+2.1f;}
    public void Slow(float seconds)
    {
        if(agent==null||run==null||enemy.IsDead)return;
        if(!slowing){baseSpeed=agent.speed;slowing=true;}
        slowUntil=Mathf.Max(slowUntil,run.Clock+seconds);agent.speed=baseSpeed*.65f;
    }
    void Update()
    {
        if(slowing&&(run==null||run.Clock>=slowUntil||enemy==null||enemy.IsDead))Restore();
    }
    void Restore(){if(slowing&&agent!=null)agent.speed=baseSpeed;slowing=false;}
    void OnDisable(){Restore();}
    public float MoveToward(Vector3 target,float requested)
    {
        if(run==null||enemy==null||enemy.IsDead||WeaponAction.CombatPaused||agent==null||!agent.enabled||!agent.isOnNavMesh)return 0;
        if(run.Clock>=budgetUntil){budgetUntil=run.Clock+2;moved=0;}
        float remaining=(enemy.IsVeteran?.5f:2.5f)-moved;
        Vector3 start=transform.position,delta=Vector3.ProjectOnPlane(target-start,Vector3.up);
        float step=Mathf.Min(delta.magnitude,Mathf.Min(Mathf.Max(0,requested),remaining));if(step<.001f)return 0;
        Vector3 dir=delta.normalized;
        foreach(var hit in Physics.CapsuleCastAll(start+Vector3.up*.5f,start+Vector3.up*1.3f,Mathf.Max(.15f,agent.radius*.85f),dir,step+.04f,1<<3,QueryTriggerInteraction.Ignore))
            if(hit.collider.GetComponentInParent<MutationObject>()==null)step=Mathf.Min(step,Mathf.Max(0,hit.distance-.04f));
        Vector3 goal=start+dir*step;
        if(NavMesh.Raycast(start,goal,out var edge,agent.areaMask))goal=edge.position-dir*.03f;
        if(!NavMesh.SamplePosition(goal,out var ground,.2f,agent.areaMask)||Mathf.Abs(ground.position.y-start.y)>.25f)return 0;
        agent.Move(ground.position-start);float actual=Vector3.Distance(start,transform.position);moved+=actual;return actual;
    }
}
