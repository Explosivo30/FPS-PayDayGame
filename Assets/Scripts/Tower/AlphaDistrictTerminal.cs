using UnityEngine;

public sealed class AlphaDistrictTerminal : MonoBehaviour, IInteractable
{
    public AlphaDistrictDirector director;
    // 0 workshop power, 1 root core, 2 extraction radio, 3 boarding, 4 medical supply.
    public int kind, zoneIndex;
    public void Interact() { if (director) director.TryUse(kind); }
}
