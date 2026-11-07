using UnityEngine;

public sealed class FoundryObjective : MonoBehaviour, IInteractable
{
    public bool isExit;
    public void Interact()
    {
        var director=Object.FindFirstObjectByType<FoundryArenaDirector>();
        if(director==null)return;
        if(isExit)director.TryExit();else director.CollectCore();
    }
}
