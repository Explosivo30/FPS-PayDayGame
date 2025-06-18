using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class SquadManager : MonoBehaviour
{

    [Tooltip("Color to tint the squad leaders")]
    [SerializeField] private Color leaderColor = Color.yellow;

    [Tooltip("Radio base de formaci�n")]
    [SerializeField] private float baseRadius = 2f;
    [Tooltip("Tama�o de cada escuadr�n")]
    [SerializeField] private int squadSize = 4;

    // list gets populated by each enemy in Awake()
    private readonly List<ISquadMember> _allMembers = new List<ISquadMember>();
    private readonly List<Squad> _squads = new List<Squad>();

    public static SquadManager Instance { get; private set; }

    public void Register(ISquadMember member)
    {
        if (!_allMembers.Contains(member))
            _allMembers.Add(member);
    }

    private void Awake()
    {
        if (Instance != null) { return; }
        Instance = this;
        
    }

    private void Start()
    {
        Debug.Log($"[SquadManager] Building squads from {_allMembers.Count} members");
        for (int i = 0; i < _allMembers.Count; i += squadSize)
        {
            var group = _allMembers
                .GetRange(i, Mathf.Min(squadSize, _allMembers.Count - i));
            _squads.Add(new Squad(
                group,
                baseRadius,
                GameManager.Instance.GetPlayerTransforms()[0]
            ));
        }

        foreach (var squad in _squads)
        {
            var leaderT = squad.Leader.Transform;
            var rend = leaderT.GetComponent<Renderer>();
            if (rend != null)
            {
                // To avoid changing the shared material on all instances,
                // instantiate a fresh material first:
                rend.material = new Material(rend.material);
                rend.material.color = leaderColor;
            }
        }
    }

    private void LateUpdate()
    {
        foreach (var squad in _squads)
            squad.UpdateSquad();

        Debug.Log(_squads.Count);
    }

    public void UpdateSquadsOnce()
    {
        foreach (var s in _squads) s.UpdateSquad();
    }

}
public static class EnemyEvents
{
    public static Action<IDamageable> OnDeath;
}