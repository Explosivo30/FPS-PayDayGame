using UnityEngine;
using UnityEngine.AI;

public class RobotPresentation : MonoBehaviour
{
    public Transform visual,muzzle,turret;
    public ParticleSystem sparks,muzzleFlash;
    private Vector3 home,target,hitOffset;
    private Quaternion rotation,turretRotation;
    private float pulse,telegraph,deathTime=-1;
    private NavMeshAgent agent;
    private Renderer[] renderers;
    private MaterialPropertyBlock properties;
    public Vector3 MuzzlePosition=>muzzle!=null?muzzle.position:transform.position+Vector3.up;
    private void Awake()
    {
        agent=GetComponent<NavMeshAgent>();
        if(visual==null) visual=transform;
        home=visual.localPosition;rotation=visual.localRotation;
        if(turret!=null) turretRotation=turret.localRotation;
        renderers=visual.GetComponentsInChildren<Renderer>(); properties=new MaterialPropertyBlock();
    }
    public void SetTarget(Vector3 position) { target=position; }
    public void Telegraph(float duration) { telegraph=duration; }
    public void Fire() { pulse=1; muzzleFlash?.Play(); }
    public void Hit(Vector3 direction) { hitOffset=transform.InverseTransformDirection(direction)*.10f;pulse=.5f; }
    public void Die() { deathTime=Time.time; sparks?.Play(); }
    private void LateUpdate()
    {
        if(visual==null) return;
        float dt=Time.deltaTime;
        if(deathTime>=0)
        {
            float t=Mathf.Clamp01((Time.time-deathTime)/.5f);
            visual.localRotation=rotation*Quaternion.Euler(0,0,-85*t);
            visual.localPosition=home+Vector3.down*.35f*t;
            return;
        }
        Vector3 velocity=agent!=null&&agent.enabled?transform.InverseTransformDirection(agent.velocity):Vector3.zero;
        visual.localPosition=home+Vector3.up*Mathf.Sin(Time.time*5+GetInstanceID())*.025f+hitOffset;
        visual.localRotation=Quaternion.Slerp(visual.localRotation,rotation*Quaternion.Euler(velocity.z*1.5f,-pulse*2,-velocity.x*2),1-Mathf.Exp(-12*dt));
        if(turret!=null)
        {
            Vector3 direction=target-MuzzlePosition;
            float pitch=-Mathf.Atan2(direction.y,new Vector2(direction.x,direction.z).magnitude)*Mathf.Rad2Deg;
            turret.localRotation=turretRotation*Quaternion.Euler(Mathf.Clamp(pitch,-35,35)-pulse*8,0,0);
        }
        hitOffset*=Mathf.Exp(-18*dt);pulse*=Mathf.Exp(-16*dt);
        telegraph=Mathf.Max(0,telegraph-dt);
        foreach(var renderer in renderers)
        {
            if(renderer is ParticleSystemRenderer) continue;
            renderer.GetPropertyBlock(properties);
            properties.SetColor("_EmissionColor",telegraph>0?new Color(2,.45f,.03f):new Color(.02f,.1f,.13f));
            renderer.SetPropertyBlock(properties);
        }
    }
}
