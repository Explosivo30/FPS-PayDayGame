using UnityEngine;
[DisallowMultipleComponent]
public sealed class EnemyPerception : MonoBehaviour
{
    NormalEnemyStateMachine owner;
    public PlayerStateMachine Target { get; private set; }
    public bool Visible { get; private set; }
    public bool HasMemory=>hasInformation&&Time.time-lastInformation<Profile.memorySeconds;
    public Vector3 LastKnown { get; private set; }
    public int Observations { get; private set; }
    public ThreatProfile Profile=>TowerSession.Instance?.Threat?.Profile??ThreatProfile.Basic;
    float nextSense,lastInformation;bool hasInformation;
    void Awake(){owner=GetComponent<NormalEnemyStateMachine>();LastKnown=transform.position;}
    void OnEnable(){CombatFeedback.PlayerShot+=Hear;}
    void OnDisable(){CombatFeedback.PlayerShot-=Hear;Visible=false;}
    public void SeedSearch(Vector3 point){if(!hasInformation)LastKnown=point;}
    public void Tick()
    {
        if(WeaponAction.CombatPaused||owner.IsDead||Time.time<nextSense)return;
        nextSense=Time.time+.1f+Mathf.Abs(GetInstanceID()%4)*.013f;Observations++;
        if(Target==null)
        {
            var players=GameManager.Instance?.GetPlayerTransforms();
            if(players!=null&&players.Count>0)Target=players[0].GetComponent<PlayerStateMachine>();
        }
        Visible=false;if(Target==null||Target.IsDead)return;
        Vector3 origin=transform.position+Vector3.up*1.1f;
        Vector3 point=Target.transform.position+Vector3.up*.6f;
        if(Vector3.Distance(origin,point)>owner.detectionRange)return;
        // Arena robots have panoramic sensors, but require an unobstructed physical ray.
        if(Physics.Linecast(origin,point,out var hit,owner.layerMask,QueryTriggerInteraction.Ignore)&&hit.collider.GetComponentInParent<PlayerStateMachine>()==Target)
        {Visible=true;LastKnown=Target.transform.position;lastInformation=Time.time;hasInformation=true;}
    }
    void Hear(Vector3 origin)
    {
        if(owner.IsDead||WeaponAction.CombatPaused||Vector3.Distance(transform.position,origin)>Profile.hearingRange)return;
        // Hearing gives a location, never line of sight or permission to fire.
        LastKnown=origin;lastInformation=Time.time;hasInformation=true;
    }
}
