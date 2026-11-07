using UnityEngine;
using UnityEngine.AI;

/// <summary>Poses only child joints. RobotPresentation owns the visual root and its hit/death spring.</summary>
[DefaultExecutionOrder(65)]
public class MeleeRobotPresentation : MonoBehaviour
{
    public Transform torso,head,leftArm,rightArm,leftForearm,rightForearm,leftThigh,rightThigh,leftShin,rightShin;
    public Renderer warning;
    public TrailRenderer fistTrail;
    public AudioClip windupSound,swingSound,contactSound,stepSound;
    MeleeRobotCombat combat;
    NormalEnemyStateMachine owner;
    RobotPresentation reactions;
    NavMeshAgent agent;
    AudioSource voice,feet;
    Transform[] joints;
    Quaternion[] rest;
    float stride,walk,previousStep;
    bool audioPaused;
    void Awake()
    {
        combat=GetComponent<MeleeRobotCombat>();owner=GetComponent<NormalEnemyStateMachine>();
        reactions=GetComponent<RobotPresentation>();agent=GetComponent<NavMeshAgent>();
        joints=new[]{torso,head,leftArm,rightArm,leftForearm,rightForearm,leftThigh,rightThigh,leftShin,rightShin};
        rest=new Quaternion[joints.Length];for(int i=0;i<joints.Length;i++)rest[i]=joints[i].localRotation;
        voice=MakeSource(50,22);feet=MakeSource(100,14);
        if(warning!=null)warning.enabled=false;
        if(fistTrail!=null) { fistTrail.emitting=false;fistTrail.Clear(); }
    }
    AudioSource MakeSource(int priority,float distance)
    {
        var source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1;
        source.minDistance=2;source.maxDistance=distance;source.rolloffMode=AudioRolloffMode.Linear;
        source.priority=priority;source.dopplerLevel=0;return source;
    }
    void OnEnable() { combat.PhaseChanged+=OnPhase;combat.PunchResolved+=OnContact; }
    void OnDisable()
    {
        combat.PhaseChanged-=OnPhase;combat.PunchResolved-=OnContact;voice.Stop();feet.Stop();
        if(fistTrail!=null)fistTrail.emitting=false;
    }
    void OnPhase(MeleeRobotPhase phase)
    {
        if(phase==MeleeRobotPhase.Windup) { voice.PlayOneShot(windupSound,.52f);reactions.Telegraph(combat.windupSeconds); }
        if(phase==MeleeRobotPhase.Strike)voice.PlayOneShot(swingSound,.55f);
        if(phase==MeleeRobotPhase.Dead) { voice.Stop();feet.Stop(); }
        if(fistTrail!=null) { fistTrail.emitting=phase==MeleeRobotPhase.Strike;if(phase==MeleeRobotPhase.Dead)fistTrail.Clear(); }
        if(warning!=null)warning.enabled=phase==MeleeRobotPhase.Windup;
    }
    void OnContact(bool hit) { if(hit)voice.PlayOneShot(contactSound,.65f); }
    void LateUpdate()
    {
        if(WeaponAction.CombatPaused)
        {
            if(!audioPaused) { voice.Pause();feet.Pause();audioPaused=true; }
            return;
        }
        if(audioPaused) { voice.UnPause();feet.UnPause();audioPaused=false; }
        if(owner.IsDead)return;
        float speed=agent!=null&&agent.enabled?agent.velocity.magnitude:0;
        walk=Mathf.MoveTowards(walk,Mathf.Clamp01(speed/3.5f),Time.deltaTime*7);
        stride+=speed*Time.deltaTime*3.3f;
        float step=Mathf.Sin(stride),swing=step*27*walk;
        if(walk>.25f&&Mathf.Sign(step)!=Mathf.Sign(previousStep))feet.PlayOneShot(stepSound,.2f);
        previousStep=step;
        Vector3 body=new Vector3(4*walk,-step*4*walk,0),face=Vector3.zero;
        Vector3 la=new Vector3(-swing-12,0,-8),ra=new Vector3(swing-12,0,8);
        Vector3 lf=new Vector3(-20,0,0),rf=new Vector3(-20,0,0);
        float pose=0,p= combat.PhaseProgress;
        if(combat.Phase==MeleeRobotPhase.Windup)
        {
            pose=Mathf.SmoothStep(0,1,Mathf.Clamp01(p*2.5f));
            body=Vector3.Lerp(body,new Vector3(-8,-24,-7),pose);
            ra=Vector3.Lerp(ra,new Vector3(-105,-18,28),pose);
            rf=Vector3.Lerp(rf,new Vector3(-115,0,0),pose);
        }
        else if(combat.Phase==MeleeRobotPhase.Strike||combat.Phase==MeleeRobotPhase.Recover)
        {
            float extend=combat.Phase==MeleeRobotPhase.Strike?Mathf.SmoothStep(0,1,Mathf.Clamp01(p/.45f)):1;
            pose=combat.Phase==MeleeRobotPhase.Recover?1-Mathf.SmoothStep(0,1,p):1;
            body=Vector3.Lerp(body,Vector3.Lerp(new Vector3(-8,-24,-7),new Vector3(14,18,3),extend),pose);
            ra=Vector3.Lerp(ra,Vector3.Lerp(new Vector3(-105,-18,28),new Vector3(-83,-9,-10),extend),pose);
            rf=Vector3.Lerp(rf,Vector3.Lerp(new Vector3(-115,0,0),new Vector3(-4,0,0),extend),pose);
        }
        la=Vector3.Lerp(la,new Vector3(-30,15,-15),pose);lf=Vector3.Lerp(lf,new Vector3(-65,0,0),pose);
        face.y=-body.y*.7f;
        Pose(0,body);Pose(1,face);Pose(2,la);Pose(3,ra);Pose(4,lf);Pose(5,rf);
        Pose(6,new Vector3(swing,0,0));Pose(7,new Vector3(-swing,0,0));
        Pose(8,new Vector3(Mathf.Max(0,-step)*35*walk,0,0));Pose(9,new Vector3(Mathf.Max(0,step)*35*walk,0,0));
        if(warning!=null&&warning.enabled)warning.transform.localScale=Vector3.one*(.85f+.15f*Mathf.Sin(p*30));
    }
    void Pose(int index,Vector3 angles) { joints[index].localRotation=rest[index]*Quaternion.Euler(angles); }
}
