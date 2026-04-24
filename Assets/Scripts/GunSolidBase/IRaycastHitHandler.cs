using UnityEngine;

public interface IRaycastHitHandler
{
    void HandleRaycastHit(RaycastHit hit, float damage);
}
