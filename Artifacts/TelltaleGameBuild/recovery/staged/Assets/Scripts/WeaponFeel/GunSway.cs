using UnityEngine;
/// <summary>Legacy holder kept neutral; WeaponPresentation composes all weapon offsets.</summary>
public class GunSway : MonoBehaviour
{
    private void OnEnable() { transform.localRotation = Quaternion.identity; }
}
