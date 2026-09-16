using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(WeaponAction))]
public class Knife : BaseMelee
{
    public float range = 1.8f;
    public float contactTime = .18f;
    public LayerMask hitMask = ~(1 << 7 | 1 << 8);
    private WeaponAction action;
    private Camera aimCamera;
    private void Awake()
    {
        action = GetComponent<WeaponAction>();
        foreach (var cam in transform.root.GetComponentsInChildren<Camera>(true))
            if (cam.gameObject.layer != LayerMask.NameToLayer("Weapon")) aimCamera = cam;
    }
    public override void Use()
    {
        if (!action.RequestFire()) return;
        action.BeginAttack(attackCooldown);
        action.NotifyShot();
        StartCoroutine(Strike());
    }
    private IEnumerator Strike()
    {
        yield return new WaitForSeconds(contactTime);
        ResolveStrike();
    }
    public int ResolveStrike()
    {
        Transform view = aimCamera != null ? aimCamera.transform : transform;
        var damaged = new HashSet<IDamageable>();
        // Sweep a fan of short rays; every ray terminates at the first obstacle.
        for (int i = -3; i <= 3; i++)
        {
            Vector3 dir = Quaternion.AngleAxis(i * 5f, view.up) * view.forward;
            if (!Physics.Raycast(view.position, dir, out var hit, range, hitMask, QueryTriggerInteraction.Ignore)) continue;
            var target = hit.collider.GetComponentInParent<IDamageable>();
            if (target == null || !damaged.Add(target)) continue;
            target.TakeDamage(damage);
            hit.collider.GetComponentInParent<IHitReactable>()?.ReactToHit(hit, dir, damage);
            CombatFeedback.ReportHit();
        }
        return damaged.Count;
    }
    private void OnDisable() { StopAllCoroutines(); action?.Cancel(); }
}
