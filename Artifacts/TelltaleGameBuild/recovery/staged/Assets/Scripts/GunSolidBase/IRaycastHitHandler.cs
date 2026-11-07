using UnityEngine;

public interface IRaycastHitHandler
{
    void HandleRaycastHit(RaycastHit hit, float damage);
}

public interface IHitReactable
{
    void ReactToHit(RaycastHit hit, Vector3 shotDirection, float damage);
}
