using UnityEngine;

public sealed class AlphaDistrictZone : MonoBehaviour
{
    public int index;
    public string label;
    public Bounds bounds;
    public int[] neighbors = new int[0];
    public Transform[] entries = new Transform[0];
    public FoundryCombatSector rangedSector;
    public bool Contains(Vector3 point) => bounds.Contains(point);
}
