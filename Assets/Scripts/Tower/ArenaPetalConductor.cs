using UnityEngine;

/// <summary>Only the marked conductor accepts interaction; hitting the panel does not toggle it.</summary>
public sealed class ArenaPetalConductor : MonoBehaviour, IInteractable, IRaycastHitHandler
{
    public ArenaMechanicalPetal petal;
    public void Interact() { petal?.Activate(); }
    public void HandleRaycastHit(RaycastHit hit, float damage) { if (damage > 0) petal?.Activate(); }
}
