using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-150)]
public class SquadManager : MonoBehaviour
{
    public static SquadManager Instance { get; private set; }
    readonly List<ISquadMember> members=new List<ISquadMember>();
    readonly HashSet<NormalEnemyStateMachine> attackers=new HashSet<NormalEnemyStateMachine>();
    readonly Dictionary<NormalEnemyStateMachine,Vector3> destinations=new Dictionary<NormalEnemyStateMachine,Vector3>();
    public int AttackLimit { get; private set; }=2;
    public int MeleeLimit { get; private set; }=2;
    public int ActiveAttackers=>attackers.Count;
    public int ReservedDestinations=>destinations.Count;
    void Awake(){if(Instance!=null&&Instance!=this){Destroy(this);return;}Instance=this;}
    void OnDestroy(){if(Instance==this)Instance=null;}
    public void Configure(int limit,int melee){AttackLimit=Mathf.Max(1,limit);MeleeLimit=Mathf.Max(1,melee);}
    public void Register(ISquadMember member){if(member!=null&&!members.Contains(member))members.Add(member);}
    public void Unregister(ISquadMember member)
    {members.Remove(member);if(member is NormalEnemyStateMachine enemy){attackers.Remove(enemy);destinations.Remove(enemy);}}
    public int Lane(NormalEnemyStateMachine enemy)=>Mathf.Max(0,members.IndexOf(enemy))%3-1;
    public bool CanAttack(NormalEnemyStateMachine enemy)
    {
        if(attackers.Contains(enemy))return true;
        int melee=0;foreach(var e in attackers)if(e.IsMelee)melee++;
        return attackers.Count<AttackLimit&&(!enemy.IsMelee||melee<MeleeLimit);
    }
    public bool TryAttack(NormalEnemyStateMachine enemy)
    {
        if(enemy==null||enemy.IsDead||WeaponAction.CombatPaused)return false;
        if(attackers.Contains(enemy))return true;
        if(attackers.Count>=AttackLimit)return false;
        int melee=0;foreach(var e in attackers)if(e.IsMelee)melee++;
        if(enemy.IsMelee&&melee>=MeleeLimit)return false;
        attackers.Add(enemy);return true;
    }
    public void ReleaseAttack(NormalEnemyStateMachine enemy){attackers.Remove(enemy);}
    public bool Reserve(NormalEnemyStateMachine enemy,Vector3 point,float separation)
    {
        foreach(var pair in destinations)
            if(pair.Key!=enemy&&!pair.Key.IsDead&&Vector3.Distance(pair.Value,point)<separation)return false;
        destinations[enemy]=point;return true;
    }
    public void ReleaseDestination(NormalEnemyStateMachine enemy){destinations.Remove(enemy);}
    public void ClearCombat(){attackers.Clear();destinations.Clear();}
    // Retained for scene/script compatibility. Slots are now reserved by tactical navigation.
    public void RebuildSquads(){members.RemoveAll(m=>(m as UnityEngine.Object)==null||m is NormalEnemyStateMachine e&&e.IsDead);}
    public void UpdateSquadsOnce(){}
}
public static class EnemyEvents
{
    public static Action<IDamageable> OnDeath;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset(){OnDeath=null;}
}
