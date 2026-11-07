using System.Collections;
using Project.Scripts.Fractures;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Scene-local structural state: one health pool across intact and fractured colliders.</summary>
public sealed class FoundryStructure : MonoBehaviour
{
    public FractureThis source;
    public bool opensPassage;
    public Vector3 passageCenter, passageHalfExtents = new Vector3(.65f,.95f,.9f);
    public NavMeshObstacle obstacle;
    public float structuralHealth = 90;
    public FractureThis Source => source;
    public bool IsOpen { get; private set; }
    public bool Ready => graph != null;
    public float Health { get; private set; }
    public bool PassageClear => IsOpen && !Physics.CheckBox(passageCenter, passageHalfExtents, Quaternion.identity,
        (1<<0)|(1<<3)|(1<<10), QueryTriggerInteraction.Ignore);
    ChunkGraphManager graph;
    ImpactDetonator whole, fractured;
    Collider[] rubble = new Collider[0];
    IEnumerator Start()
    {
        Health=structuralHealth;
        while(source != null && source.GeneratedGraph == null) yield return null;
        if(source == null) yield break;
        graph=source.GeneratedGraph; whole=source.GetComponent<ImpactDetonator>();fractured=graph.GetComponent<ImpactDetonator>();
        whole.StructuralHit+=OnStructuralHit;fractured.StructuralHit+=OnStructuralHit;
        var relay=graph.gameObject.AddComponent<FoundryStructureRelay>();relay.target=this;
    }
    public static FoundryStructure From(Collider collider)
    {
        var direct=collider.GetComponentInParent<FoundryStructure>();
        return direct != null ? direct : collider.GetComponentInParent<FoundryStructureRelay>()?.target;
    }
    public bool ApplyImpact(Vector3 point,Vector3 direction,float damage)
    {
        if(!Ready||IsOpen||WeaponAction.CombatPaused)return false;
        var receiver=whole.gameObject.activeInHierarchy?whole:fractured;
        receiver.ApplyWorldImpact(point,direction,damage);return true;
    }
    void OnStructuralHit(float damage)
    {
        if(IsOpen)return;
        Health=Mathf.Max(0,Health-damage);
        if(Health<=0)Collapse();
    }
    void Collapse()
    {
        IsOpen=true;
        if(obstacle)obstacle.enabled=false;
        source.gameObject.SetActive(false);graph.gameObject.SetActive(true);
        graph.enabled=false; // The structure is fully severed; no graph search may freeze rubble again.
        foreach(var node in graph.GetComponentsInChildren<ChunkNode>(true))
        {
            var body=node.GetComponent<Rigidbody>();body.isKinematic=false;
            node.ReleaseFromImpact((node.transform.position-transform.position).normalized*3+Vector3.up*2,node.transform.position);
            // Keep physical contact with the floor while excluding bullets, players and robots.
            node.gameObject.layer=2;
            Destroy(node.gameObject,3.5f);
        }
        rubble=graph.GetComponentsInChildren<Collider>();
        foreach(var player in Object.FindObjectsByType<PlayerStateMachine>(FindObjectsSortMode.None))IgnoreActorCollisions(player.GetComponentsInChildren<Collider>());
        foreach(var enemy in Object.FindObjectsByType<NormalEnemyStateMachine>(FindObjectsSortMode.None))IgnoreActorCollisions(enemy.GetComponentsInChildren<Collider>());
    }
    public void IgnoreActorCollisions(Collider[] actor)
    { if(!IsOpen)return;foreach(var piece in rubble)if(piece)foreach(var body in actor)if(body)Physics.IgnoreCollision(piece,body,true); }
    void OnDestroy()
    { if(whole)whole.StructuralHit-=OnStructuralHit;if(fractured)fractured.StructuralHit-=OnStructuralHit; }
}
