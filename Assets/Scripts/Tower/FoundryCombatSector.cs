using UnityEngine;
using UnityEngine.AI;

/// <summary>A visible, scene-local firing sector. Robots still rotate, move, take damage and obey LOS.</summary>
public sealed class FoundryCombatSector : MonoBehaviour
{
    public Bounds bounds;
    public Transform[] positions;
    public bool Contains(Vector3 point) => bounds.Contains(point);
    public bool TryPosition(NormalEnemyStateMachine owner, Vector3 target, bool forceMove, out Vector3 position)
    {
        position=default;float best=float.NegativeInfinity;bool found=false;
        if(positions==null)return false;
        foreach(var point in positions)
        {
            if(!point||!NavMesh.SamplePosition(point.position,out var hit,1.2f,owner.agent.areaMask)||!Contains(hit.position))continue;
            float travel=Vector3.Distance(owner.transform.position,hit.position);
            if(forceMove&&travel<2)continue;
            var path=new NavMeshPath();
            if(!owner.agent.CalculatePath(hit.position,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
            bool inside=true;foreach(var corner in path.corners)if(!Contains(corner)){inside=false;break;}
            if(!inside)continue;
            bool clear=!Physics.Linecast(hit.position+Vector3.up*1.15f,target+Vector3.up*.6f,out var obstacle,owner.layerMask,QueryTriggerInteraction.Ignore)
                ||obstacle.collider.GetComponentInParent<PlayerStateMachine>()!=null;
            float range=Vector3.Distance(hit.position,target);
            float score=(clear?30:0)-Mathf.Abs(range-12)-travel*.12f;
            if(score<=best)continue;best=score;position=hit.position;found=true;
        }
        return found;
    }
}
