using UnityEngine;

public interface IWeapon
{
    void Use();
    bool IsAutomatic { get; } // true for auto weapons like machine guns
}
