using System.Collections.Generic;
using UnityEngine;

public class Squad
{
    public readonly List<ISquadMember> Members;
    public ISquadMember Leader => Members.Count>0?Members[0]:null;
    private readonly float radius;
    private readonly Transform player;
    public Squad(List<ISquadMember> members,float radius,Transform player) { Members=members;this.radius=radius;this.player=player; }
    public void UpdateSquad()
    {
        if(player==null||Leader==null) return;
        Leader.MoveToFormationPosition(player.position);
        for(int i=1;i<Members.Count;i++)
        {
            float angle=i*Mathf.PI*2/Mathf.Max(1,Members.Count-1);
            Members[i].MoveToFormationPosition(player.position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius);
        }
    }
}
