using UnityEngine;
public class ElevatorPanel : MonoBehaviour, IInteractable
{
    public TowerElevator elevator;
    public void Interact() { TowerSession.Instance?.RequestTravel(elevator); }
}
