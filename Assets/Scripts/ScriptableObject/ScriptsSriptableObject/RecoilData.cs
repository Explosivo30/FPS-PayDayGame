using UnityEngine;

[CreateAssetMenu(menuName = "Weapons/RecoilData")]
public class RecoilData : ScriptableObject
{
    [Header("Camera recoil (degrees per shot)")]
    public float recoilKickUp = 1f;
    public float recoilKickSide = 0.25f;
    [Range(0f, 0.5f)] public float verticalKickVariation = 0.1f;
    [Min(0f)] public float maxVerticalRecoil = 6f;
    [Min(0f)] public float maxHorizontalRecoil = 1.25f;
    [Min(1f)] public float recoilSnappiness = 22f;
    [Min(0f)] public float recoilReturnDelay = 0.075f;
    [Min(0.1f)] public float recoilReturnSpeed = 10f;

    [Header("Accuracy (degrees)")]
    public float spreadHip = 1.5f;
    public float spreadADS = 0.25f;

    [Header("Weapon animation")]
    [Min(1f)] public float returnSpeed = 14f;
    [Min(0f)] public float kickbackDistance = 0.055f;
    [Min(0f)] public float weaponKickUp = 2f;
    [Min(0f)] public float weaponKickSide = 0.35f;
    [Min(0f)] public float weaponKickRoll = 0.25f;

    [Header("Player impulse")]
    [Tooltip("Physical impulse applied to the player. Keep this subtle for firearms.")]
    public float playerImpulseForce;
}
