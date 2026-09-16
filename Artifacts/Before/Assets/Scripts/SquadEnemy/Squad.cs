using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Squad
{
    public readonly List<ISquadMember> Members;
    public ISquadMember Leader { get; private set; }
    private readonly float _radius;
    private readonly Transform _player;

    public Squad(List<ISquadMember> members, float radius, Transform player)
    {
        Members = members;
        _radius = radius;
        _player = player;

        //Debug.Log(members.Count);
        // Initial, permanent leader: pick the first one
        if (Members.Count > 0)
            Leader = Members[0];

        // Subscribe to death on each member:
        foreach (var m in Members)
            if (m is IDamageable dmg)
                // when they die, OnMemberDeath will fire
                EnemyEvents.OnDeath += (dead) => OnMemberDeath(dead);
    }

    private void OnMemberDeath(IDamageable dead)
    {
        // Only react if the dead one is in my squad
        if (!Members.Contains(dead as ISquadMember)) return;

        Members.Remove(dead as ISquadMember);

        if (dead as ISquadMember == Leader)
        {
            // pick a new leader: first surviving member
            Leader = Members.FirstOrDefault();
        }
    }

    public void UpdateSquad()
    {
        if (Members.Count == 0) return;

        // 1) Leader chases player:
        Leader.MoveToFormationPosition(GameManager.Instance.GetPlayerTransforms()[0].position);

        // 2) Followers form around the fixed Leader:
        int n = Members.Count;
        int idx = 0;
        foreach (var member in Members)
        {
            if (member == Leader) continue;
            float angle = idx * Mathf.PI * 2f / (n - 1);
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * _radius;
            member.MoveToFormationPosition(Leader.Transform.position + offset);
            idx++;
        }
    }
}
