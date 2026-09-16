using System.Linq;
using UnityEngine;

/// <summary>Clips animate fingers; two-bone IK keeps wrists on authored weapon grips.</summary>
[DefaultExecutionOrder(120)]
public class OperatorHands : MonoBehaviour
{
    public WeaponPresentation weapon;
    public Transform head;
    private Transform[] upper = new Transform[2], fore = new Transform[2], hand = new Transform[2];
    private Quaternion[] upperRest = new Quaternion[2], foreRest = new Quaternion[2];
    private Animation clips;
    private WeaponAction action;
    private void Awake()
    {
        var bones=GetComponentsInChildren<Transform>(true);
        for(int i=0;i<2;i++)
        {
            string side=i==0?"R":"L";
            upper[i]=bones.FirstOrDefault(t=>t.name=="UpperArm_"+side);
            fore[i]=bones.FirstOrDefault(t=>t.name=="Forearm_"+side);
            hand[i]=bones.FirstOrDefault(t=>t.name=="Hand_"+side);
            if(upper[i]!=null) upperRest[i]=upper[i].localRotation;
            if(fore[i]!=null) foreRest[i]=fore[i].localRotation;
        }
        clips=GetComponent<Animation>();
        action=weapon.GetComponent<WeaponAction>();
    }
    private void OnEnable()
    {
        if(action==null && weapon!=null) action=weapon.GetComponent<WeaponAction>();
        if(action!=null) { action.StateChanged+=OnState; action.ShotFired+=OnShot; }
        Play("Idle",.15f);
    }
    private void OnDisable() { if(action!=null) { action.StateChanged-=OnState; action.ShotFired-=OnShot; } }
    private void OnState(WeaponActionState state)
    {
        string clip=state==WeaponActionState.Reloading?"Reload":state==WeaponActionState.Drawing?"Draw":state==WeaponActionState.Holstering?"Holster":state==WeaponActionState.Attacking?"KnifeSlash":"Idle";
        Play(clip,.06f);
    }
    private void OnShot() { Play(weapon.GetComponent<Knife>()!=null?"KnifeSlash":"Fire",.025f); }
    private void Play(string name,float blend)
    {
        if(clips==null) clips=GetComponent<Animation>();
        if(clips==null||clips[name]==null) return;
        clips[name].wrapMode=name=="Idle"?WrapMode.Loop:WrapMode.Once;
        if(name=="Reload" && action!=null) clips[name].speed=clips[name].length/Mathf.Max(.1f,action.reloadDuration);
        clips.CrossFade(name,blend);
        if(name!="Idle") clips.CrossFadeQueued("Idle",.08f,QueueMode.CompleteOthers);
    }
    private void LateUpdate()
    {
        if(head==null||weapon==null) return;
        transform.SetPositionAndRotation(head.position,head.rotation);
        for(int i=0;i<2;i++)
        {
            Transform target=i==0?weapon.rightGrip:weapon.leftGrip;
            if(upper[i]==null||fore[i]==null||hand[i]==null||target==null) continue;
            upper[i].localRotation=upperRest[i]; fore[i].localRotation=foreRest[i];
            Vector3 goal=target.position;
            if(i==1 && action.State==WeaponActionState.Reloading)
            {
                float t=action.Progress;
                float gesture=action.shellReload?Mathf.Sin(t*Mathf.PI):Mathf.Sin(t*Mathf.PI);
                goal+=head.TransformVector(new Vector3(-.1f,-.16f,.025f))*gesture;
            }
            Solve(upper[i],fore[i],hand[i],goal,head.TransformPoint(new Vector3(i==0?.52f:-.52f,-.43f,.22f)));
            hand[i].rotation=target.rotation;
        }
    }
    private static void Solve(Transform a,Transform b,Transform c,Vector3 goal,Vector3 hint)
    {
        float ab=Vector3.Distance(a.position,b.position),bc=Vector3.Distance(b.position,c.position);
        Vector3 delta=goal-a.position; float distance=Mathf.Clamp(delta.magnitude,.01f,ab+bc-.001f);
        Vector3 direction=delta.normalized;
        Vector3 bend=Vector3.ProjectOnPlane(hint-a.position,direction).normalized;
        float adjacent=(ab*ab+distance*distance-bc*bc)/(2*distance);
        float height=Mathf.Sqrt(Mathf.Max(0,ab*ab-adjacent*adjacent));
        Vector3 elbow=a.position+direction*adjacent+bend*height;
        a.rotation=Quaternion.FromToRotation(b.position-a.position,elbow-a.position)*a.rotation;
        b.rotation=Quaternion.FromToRotation(c.position-b.position,goal-b.position)*b.rotation;
    }
}
