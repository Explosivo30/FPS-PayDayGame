using UnityEngine;
[CreateAssetMenu(menuName = "Weapons/AimData")]
public class AimData : ScriptableObject
{
    public Vector3 positionOffset;
    public Vector3 rotationOffset;
    public float transitionSpeed = 10f;
    public float fov = 40f;

}
