using UnityEngine;
using UnityEngine.AI;
[DisallowMultipleComponent]
public sealed class EnemyNavigation : MonoBehaviour
{
    NormalEnemyStateMachine owner;EnemyPerception senses;NavMeshAgent agent;
    NavMeshPath path;
    Vector3 destination;bool hasDestination,forceMove;float nextDecision,searchAngle,moveUntil;
    public int Decisions { get; private set; }
    public Vector3 Destination=>destination;
    ThreatProfile Profile=>senses.Profile;
    void Awake()
    {
        path=new NavMeshPath();owner=GetComponent<NormalEnemyStateMachine>();senses=GetComponent<EnemyPerception>();agent=GetComponent<NavMeshAgent>();
        nextDecision=Time.time+Mathf.Abs(GetInstanceID()%7)*.03f;searchAngle=Mathf.Abs(GetInstanceID()%360);
    }
    public void Reposition(){forceMove=true;nextDecision=0;moveUntil=Time.time+1.3f;}
    public void Stop(){if(agent!=null&&agent.enabled&&agent.isOnNavMesh)agent.isStopped=true;}
    public void Tick(bool melee,bool canEngage)
    {
        if(owner.IsDead||agent==null||!agent.enabled||!agent.isOnNavMesh)return;
        if(WeaponAction.CombatPaused){Stop();return;}
        if(Time.time<nextDecision)return;
        nextDecision=Time.time+Profile.decisionInterval+Mathf.Abs(GetInstanceID()%5)*.017f;Decisions++;
        Vector3 target=senses.LastKnown;
        float distance=Vector3.Distance(transform.position,target);
        if(melee&&senses.Visible&&canEngage&&distance<3.5f)
        {
            Vector3 side=Vector3.Cross(Vector3.up,(transform.position-target).normalized);
            Move(target+side*(SquadManager.Instance!=null?SquadManager.Instance.Lane(owner)*.7f:0),.65f);return;
        }
        if(!senses.Visible&&senses.HasMemory)
        {
            // Investigate the observed position, with nearby navigable alternatives if the slot is occupied.
            if(Move(target,Profile.slotDistance))return;
            for(int i=0;i<4;i++)if(Move(target+Quaternion.Euler(0,i*90+45,0)*Vector3.forward*2,Profile.slotDistance))return;
            Stop();return;
        }
        if(!melee&&!canEngage&&(!hasDestination||Vector3.Distance(transform.position,destination)<1))forceMove=true;
        if(!melee&&hasDestination&&!forceMove&&Time.time<moveUntil&&Vector3.Distance(transform.position,destination)>1&&distance>=Profile.retreatDistance)
        {agent.isStopped=false;return;}
        if(!melee&&canEngage&&senses.Visible&&!forceMove&&distance>=Profile.preferredMinimum&&distance<=Profile.preferredMaximum)
        {
            if(hasDestination&&Time.time<moveUntil&&Vector3.Distance(transform.position,destination)>1){agent.isStopped=false;return;}
            Stop();SquadManager.Instance?.ReleaseDestination(owner);hasDestination=false;return;
        }
        if(!senses.Visible&&!senses.HasMemory)
        {
            searchAngle+=65;
            float radius=2+(Decisions%4)*1.5f;
            if(hasDestination&&Vector3.Distance(transform.position,destination)>1.2f){agent.isStopped=false;return;}
            for(int i=0;i<6;i++)
                if(Move(target+Quaternion.Euler(0,searchAngle+i*60,0)*Vector3.forward*radius,1.5f))return;
            Stop();return;
        }
        Vector3 away=transform.position-target;away.y=0;if(away.sqrMagnitude<.1f)away=transform.forward;away.Normalize();
        int lane=SquadManager.Instance!=null?SquadManager.Instance.Lane(owner):GetInstanceID()%3-1;
        float range=melee?(canEngage?2.8f:4f):Mathf.Lerp(Profile.preferredMinimum,Profile.preferredMaximum,.5f);
        bool advanced=(TowerSession.Instance?.Threat?.Current?.Wave??1)>=9;
        float angle=melee?lane*55:(lane==0?1:lane)*(forceMove?(advanced?75:55):35);
        if(!senses.Visible&&!melee)angle+=45;
        for(int i=0;i<9;i++)
        {
            Vector3 candidate=target+Quaternion.Euler(0,angle+(i%2==0?1:-1)*i*20,0)*away*range;
            if(forceMove&&Vector3.Distance(candidate,transform.position)<(advanced?5:3))continue;
            if(!melee&&senses.Visible&&Physics.Linecast(candidate+Vector3.up,target+Vector3.up*.6f,out var obstruction,owner.layerMask,QueryTriggerInteraction.Ignore)
                &&obstruction.collider.GetComponentInParent<PlayerStateMachine>()==null)continue;
            if(Move(candidate,Profile.slotDistance)){forceMove=false;moveUntil=Time.time+1.3f;return;}
        }
        if(!senses.Visible)Move(target,Profile.slotDistance);
    }
    bool Move(Vector3 point,float separation)
    {
        if(!NavMesh.SamplePosition(point,out var sample,2,agent.areaMask))return false;
        if(!agent.CalculatePath(sample.position,path)||path.status!=NavMeshPathStatus.PathComplete)return false;
        if(SquadManager.Instance!=null&&!SquadManager.Instance.Reserve(owner,sample.position,separation))return false;
        destination=sample.position;hasDestination=true;agent.isStopped=false;agent.SetPath(path);return true;
    }
    void OnDisable(){if(owner!=null){SquadManager.Instance?.ReleaseDestination(owner);SquadManager.Instance?.ReleaseAttack(owner);}}
}
