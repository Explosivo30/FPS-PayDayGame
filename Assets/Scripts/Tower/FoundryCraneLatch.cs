using UnityEngine;
public sealed class FoundryCraneLatch : MonoBehaviour, IRaycastHitHandler, IInteractable
{
    public FoundryCrane crane;
    public int index;
    public void HandleRaycastHit(RaycastHit hit,float damage) { if(damage>0)crane.CutLatch(index); }
    public void Interact() { crane.CutLatch(index); }
}
