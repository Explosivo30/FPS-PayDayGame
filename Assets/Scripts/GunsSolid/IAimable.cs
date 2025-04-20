using UnityEngine;

public interface IAimable
{
    void StartAiming();
    void StopAiming();
    bool IsAiming { get; }
}
