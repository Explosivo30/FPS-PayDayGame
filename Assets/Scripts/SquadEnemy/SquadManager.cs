using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-150)]
public class SquadManager : MonoBehaviour
{
    public static SquadManager Instance { get; private set; }
    [SerializeField] private Color leaderColor=Color.yellow;
    [SerializeField] private float baseRadius=3;
    [SerializeField] private int squadSize=4;
    private readonly List<ISquadMember> members=new List<ISquadMember>();
    private readonly List<Squad> squads=new List<Squad>();
    private int updatedFrame=-1;
    private void Awake() { if(Instance!=null&&Instance!=this) { Destroy(this); return; } Instance=this; }
    private void OnDestroy() { if(Instance==this) Instance=null; }
    public void Register(ISquadMember member) { if(member!=null&&!members.Contains(member)) members.Add(member); }
    public void Unregister(ISquadMember member) { members.Remove(member); }
    private void LateUpdate() { UpdateSquadsOnce(); }
    public void UpdateSquadsOnce()
    {
        if(updatedFrame==Time.frameCount) return;
        updatedFrame=Time.frameCount;
        foreach(var squad in squads) squad.UpdateSquad();
    }
    public void RebuildSquads()
    {
        members.RemoveAll(m=>(m as UnityEngine.Object)==null || m is NormalEnemyStateMachine enemy && enemy.IsDead);
        squads.Clear();
        if(GameManager.Instance==null||GameManager.Instance.GetPlayerTransforms().Count==0) return;
        for(int i=0;i<members.Count;i+=Mathf.Max(1,squadSize))
            squads.Add(new Squad(members.GetRange(i,Mathf.Min(squadSize,members.Count-i)),baseRadius,GameManager.Instance.GetPlayerTransforms()[0]));
        updatedFrame=-1; UpdateSquadsOnce();
    }
}
public static class EnemyEvents
{
    public static Action<IDamageable> OnDeath;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { OnDeath=null; }
}
