using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class SquadManager : MonoBehaviour
{

    [Tooltip("Radio base de formación")]
    [SerializeField] private float baseRadius = 2f;
    [Tooltip("Tamaño de cada escuadrón")]
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
    }

    private void LateUpdate()
    {
        foreach (var squad in _squads)
            squad.UpdateSquad();
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