using UnityEngine;
[CreateAssetMenu(menuName = "Weapons/RecoilData")]
public class RecoilData : ScriptableObject
{

    public float recoilKickUp = 2f;
    public float recoilKickSide = 1f;
    public float returnSpeed = 10f;
    public float spreadHip = 2f;
    public float spreadADS = 0.5f;
}
