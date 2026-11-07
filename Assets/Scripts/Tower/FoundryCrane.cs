using UnityEngine;
using UnityEngine.AI;

public sealed class FoundryCrane : MonoBehaviour
{
    public enum CraneState { Armed, Warning, Falling, Landed }
    public Transform load;
    public Transform[] anchors;
    public Transform warningZone;
    public Vector3 landingPosition, impactHalfExtents = new Vector3(3.5f,1.1f,2);
    public CraneState State { get; private set; }
    public int LatchesCut { get; private set; }
    public int ImpactKills { get; private set; }
    public bool PlayerSafetyBlocked { get; private set; }
    public Transform Load => load;
    public Vector3 LandingPosition => landingPosition;
    public bool Ready => load != null;
    public bool WarningActive => State==CraneState.Warning && hazard!=null && hazard.IsValid;
    bool[] cut=new bool[2];
    float timer;
    Vector3 initialPosition;
    AudioSource audioSource;
    AudioClip impact;
    CombatHazardBudget.Lease hazard;
    void Start()
    {
        initialPosition=load.position;
        audioSource=gameObject.AddComponent<AudioSource>();audioSource.spatialBlend=.7f;audioSource.maxDistance=45;
        impact=ImpactFeedbackPlayer.MakeSound("Carga industrial",1);
        if(warningZone)warningZone.gameObject.SetActive(false);
        foreach(var collider in load.GetComponentsInChildren<Collider>())collider.enabled=false;
        foreach(var nav in load.GetComponentsInChildren<NavMeshObstacle>())nav.enabled=false;
    }
    public bool CutLatch(int index)
    {
        if(index<0||index>=2||cut[index]||State!=CraneState.Armed||WeaponAction.CombatPaused)return false;
        cut[index]=true;LatchesCut++;
        if(anchors!=null&&index<anchors.Length&&anchors[index])anchors[index].gameObject.SetActive(false);
        if(LatchesCut==2){State=CraneState.Warning;timer=0;}
        return true;
    }
    void Update()
    {
        if(WeaponAction.CombatPaused||State==CraneState.Armed||State==CraneState.Landed)return;
        PlayerSafetyBlocked=false;
        // Wait only for the player; robots beneath the load are intentional targets.
        foreach(var hit in Physics.OverlapBox(landingPosition+Vector3.up*.6f,impactHalfExtents+new Vector3(.65f,1,.65f)))
            if(hit.GetComponentInParent<PlayerStateMachine>()!=null) {PlayerSafetyBlocked=true;break;}
        if(PlayerSafetyBlocked)
        {
            if(State==CraneState.Warning)
            {hazard?.Dispose();hazard=null;timer=0;if(warningZone)warningZone.gameObject.SetActive(false);}
            return;
        }
        if(hazard==null||!hazard.IsValid)
        { if(!CombatHazardBudget.TryReservePrimary(this,out hazard))return; }
        if(warningZone)warningZone.gameObject.SetActive(true);
        timer+=Time.deltaTime;
        if(State==CraneState.Warning&&timer>=1.2f){State=CraneState.Falling;timer=0;}
        if(State==CraneState.Falling)
        {
            float t=Mathf.Clamp01(timer/.85f);load.position=Vector3.Lerp(initialPosition,landingPosition,t*t);
            if(t>=1)Land();
        }
    }
    void Land()
    {
        State=CraneState.Landed;
        hazard?.Dispose();hazard=null;
        foreach(var enemy in Object.FindObjectsByType<NormalEnemyStateMachine>(FindObjectsSortMode.None))
        {
            if(enemy.IsDead)continue;
            var p=enemy.transform.position-landingPosition;
            if(Mathf.Abs(p.x)<=impactHalfExtents.x+.35f && Mathf.Abs(p.z)<=impactHalfExtents.z+.35f && Mathf.Abs(p.y)<=impactHalfExtents.y+2)
            {enemy.TakeDamage(250);if(enemy.IsDead)ImpactKills++;}
        }
        foreach(var collider in load.GetComponentsInChildren<Collider>())collider.enabled=true;
        foreach(var nav in load.GetComponentsInChildren<NavMeshObstacle>())nav.enabled=true;
        if(warningZone)warningZone.gameObject.SetActive(false);
        audioSource.PlayOneShot(impact,.9f);
    }
    void OnDisable(){hazard?.Dispose();hazard=null;}
    void OnDestroy(){if(impact)Destroy(impact);}
}
